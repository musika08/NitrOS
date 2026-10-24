using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

/// <summary>
/// In-memory registry. Keys and names compare case-insensitively. FailOn matches a key path or a value name.
/// RefusedKeys are keys Windows refuses to change: writes to them (or to a missing key whose nearest existing parent is
/// refused) throw UnauthorizedAccessException, reads still work, and CanWrite reports false.
/// </summary>
public sealed class FakeRegistry : IRegistry
{
    public Dictionary<(LiveHive Hive, string Key, string Name, RegView View), RegValue> Values { get; } = new(new Cmp());
    public HashSet<(LiveHive Hive, string Key, RegView View)> Keys { get; } = new(new KeyCmp());
    public string? FailOn { get; set; }
    public HashSet<(LiveHive Hive, string Key)> RefusedKeys { get; } = new(new RefusedCmp());
    /// <summary>Every write that reached the store (SetValue, DeleteValue, DeleteKey, ImportKey), as "Op key|name".</summary>
    public List<string> Writes { get; } = [];

    private void Check(string key, string? name = null)
    {
        if (FailOn is not null && (string.Equals(FailOn, key, StringComparison.OrdinalIgnoreCase)
            || (name is not null && string.Equals(FailOn, name, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException($"FakeRegistry failure on {FailOn}");
    }

    public RegValue? GetValue(LiveHive h, string key, string name, RegView v)
    {
        Check(key, name);
        return Values.TryGetValue((h, key, name, v), out var val) ? val : null;
    }

    public void SetValue(LiveHive h, string key, string name, RegValue value, RegView v)
    {
        Check(key, name);
        Guard(h, key, v);
        Writes.Add($"Set {key}|{name}");
        AddWithAncestors(h, key, v);
        Values[(h, key, name, v)] = value;
    }

    public void DeleteValue(LiveHive h, string key, string name, RegView v)
    {
        Check(key, name);
        Guard(h, key, v);
        Writes.Add($"DeleteValue {key}|{name}");
        Values.Remove((h, key, name, v));
    }

    public IReadOnlyList<string> GetValueNames(LiveHive h, string key, RegView v)
    {
        Check(key);
        return Values.Keys.Where(k => k.Hive == h && k.View == v && Eq(k.Key, key)).Select(k => k.Name).ToList();
    }

    public RegTree? ExportKey(LiveHive h, string key, RegView v)
    {
        Check(key);
        if (!Exists(h, key, v)) return null;
        var values = Values.Where(p => p.Key.Hive == h && p.Key.View == v && Eq(p.Key.Key, key))
            .Select(p => new RegNamedValue(p.Key.Name, p.Value)).ToList();
        var prefix = key + "\\";
        var childPaths = new List<string>();
        foreach (var path in Keys.Where(k => k.Hive == h && k.View == v).Select(k => k.Key)
                     .Concat(Values.Keys.Where(k => k.Hive == h && k.View == v).Select(k => k.Key)))
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var end = path.IndexOf('\\', prefix.Length);
            var child = end < 0 ? path : path[..end];
            if (!childPaths.Contains(child, StringComparer.OrdinalIgnoreCase)) childPaths.Add(child);
        }
        return new RegTree(key, values, childPaths.Select(c => ExportKey(h, c, v)!).ToList());
    }

    public void ImportKey(LiveHive h, RegTree tree, RegView v)
    {
        Check(tree.Key);
        Guard(h, tree.Key, v);
        Writes.Add($"Import {tree.Key}");
        AddWithAncestors(h, tree.Key, v);
        foreach (var nv in tree.Values) Values[(h, tree.Key, nv.Name, v)] = nv.Value;
        foreach (var sub in tree.SubKeys) ImportKey(h, sub, v);
    }

    private void AddWithAncestors(LiveHive h, string key, RegView v)
    {
        var idx = -1;
        while ((idx = key.IndexOf('\\', idx + 1)) >= 0) Keys.Add((h, key[..idx], v));
        Keys.Add((h, key, v));
    }

    /// <summary>A key exists if it was created, or any value or descendant key sits under it.</summary>
    private bool Exists(LiveHive h, string key, RegView v)
    {
        if (Keys.Contains((h, key, v))) return true;
        var prefix = key + "\\";
        return Values.Keys.Any(k => k.Hive == h && k.View == v && (Eq(k.Key, key) || k.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            || Keys.Any(k => k.Hive == h && k.View == v && k.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public void DeleteKey(LiveHive h, string key, RegView v)
    {
        Check(key);
        Guard(h, key, v);
        Writes.Add($"DeleteKey {key}");
        var prefix = key + "\\";
        bool Under(string k) => Eq(k, key) || k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        foreach (var k in Keys.Where(k => k.Hive == h && k.View == v && Under(k.Key)).ToList()) Keys.Remove(k);
        foreach (var k in Values.Keys.Where(k => k.Hive == h && k.View == v && Under(k.Key)).ToList()) Values.Remove(k);
    }

    public bool KeyExists(LiveHive h, string key, RegView v)
    {
        Check(key);
        return Exists(h, key, v);
    }

    public bool CanWrite(LiveHive h, string key, RegView v)
    {
        Check(key);
        return !IsRefused(h, key, v);
    }

    /// <summary>The key and, while it is missing, each parent up to the nearest existing one are checked against RefusedKeys.</summary>
    private bool IsRefused(LiveHive h, string key, RegView v)
    {
        for (var k = key; k.Length > 0;)
        {
            if (RefusedKeys.Contains((h, k))) return true;
            if (Exists(h, k, v)) return false;
            var cut = k.LastIndexOf('\\');
            k = cut < 0 ? "" : k[..cut];
        }
        return false;
    }

    private void Guard(LiveHive h, string key, RegView v)
    {
        if (IsRefused(h, key, v)) throw new UnauthorizedAccessException($"Access to the registry key '{key}' is denied.");
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private sealed class Cmp : IEqualityComparer<(LiveHive Hive, string Key, string Name, RegView View)>
    {
        public bool Equals((LiveHive Hive, string Key, string Name, RegView View) x, (LiveHive Hive, string Key, string Name, RegView View) y) =>
            x.Hive == y.Hive && x.View == y.View && Eq(x.Key, y.Key) && Eq(x.Name, y.Name);
        public int GetHashCode((LiveHive Hive, string Key, string Name, RegView View) o) =>
            HashCode.Combine(o.Hive, o.View, o.Key.ToLowerInvariant(), o.Name.ToLowerInvariant());
    }

    private sealed class RefusedCmp : IEqualityComparer<(LiveHive Hive, string Key)>
    {
        public bool Equals((LiveHive Hive, string Key) x, (LiveHive Hive, string Key) y) => x.Hive == y.Hive && Eq(x.Key, y.Key);
        public int GetHashCode((LiveHive Hive, string Key) o) => HashCode.Combine(o.Hive, o.Key.ToLowerInvariant());
    }

    private sealed class KeyCmp : IEqualityComparer<(LiveHive Hive, string Key, RegView View)>
    {
        public bool Equals((LiveHive Hive, string Key, RegView View) x, (LiveHive Hive, string Key, RegView View) y) =>
            x.Hive == y.Hive && x.View == y.View && Eq(x.Key, y.Key);
        public int GetHashCode((LiveHive Hive, string Key, RegView View) o) =>
            HashCode.Combine(o.Hive, o.View, o.Key.ToLowerInvariant());
    }
}
