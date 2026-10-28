namespace NitrOS.Image.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;

public sealed class FakeOfflineRegistry : IOfflineRegistry
{
    public Dictionary<string, Dictionary<string, Dictionary<string, RegValue>>> Hives { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, FakeHive> Loaded { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> RefuseWrite { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string? FailOn { get; set; }
    public string? CrashPoint { get; set; }

    public void Crash(string callName)
    {
        throw new SimulatedCrashException($"Simulated crash on {callName}");
    }

    public void CrashOn(string callName)
    {
        Crash(callName);
    }

    public void CrashIf(string callName)
    {
        if (string.Equals(CrashPoint, callName, StringComparison.OrdinalIgnoreCase))
        {
            Crash(callName);
        }
    }

    private void Check(string callName)
    {
        if (string.Equals(FailOn, callName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated failure on {callName}");
        }
        CrashIf(callName);
    }

    public IOfflineHive Load(string hiveFile, string mountName)
    {
        Check(nameof(Load));

        if (!Hives.TryGetValue(hiveFile, out var data))
        {
            data = new Dictionary<string, Dictionary<string, RegValue>>(StringComparer.OrdinalIgnoreCase);
            Hives[hiveFile] = data;
        }

        var hive = new FakeHive(this, mountName, data);
        Loaded[mountName] = hive;
        return hive;
    }

    public IReadOnlyList<string> LoadedNitrOSHives()
    {
        Check(nameof(LoadedNitrOSHives));
        return Loaded.Keys.Where(k => k.StartsWith("NitrOS.Image.", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public void Unload(string mountName)
    {
        Check(nameof(Unload));
        Loaded.Remove(mountName);
    }

    public sealed class FakeHive : IOfflineHive
    {
        private readonly FakeOfflineRegistry _registry;
        private readonly Dictionary<string, Dictionary<string, RegValue>> _data;

        public FakeHive(FakeOfflineRegistry registry, string mountName, Dictionary<string, Dictionary<string, RegValue>> data)
        {
            _registry = registry;
            MountName = mountName;
            _data = data;
        }

        public string MountName { get; }

        public WriteCheck CanWrite(string key)
        {
            var cur = key;
            while (!string.IsNullOrEmpty(cur))
            {
                if (_registry.RefuseWrite.Contains(cur))
                {
                    return WriteCheck.Refused;
                }
                var slash = cur.LastIndexOf('\\');
                if (slash < 0) break;
                cur = cur[..slash];
            }
            return WriteCheck.Writable;
        }

        public bool DeleteKey(string key)
        {
            var prefix = key.TrimEnd('\\') + "\\";
            var toRemove = _data.Keys
                .Where(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase) ||
                            k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var k in toRemove)
            {
                _data.Remove(k);
            }
            return toRemove.Count > 0;
        }

        public bool DeleteValue(string key, string name)
        {
            if (_data.TryGetValue(key, out var vals))
            {
                return vals.Remove(name);
            }
            return false;
        }

        public RegValue? GetValue(string key, string name)
        {
            if (_data.TryGetValue(key, out var vals) && vals.TryGetValue(name, out var val))
            {
                return val;
            }
            return null;
        }

        public bool KeyExists(string key)
        {
            return _data.ContainsKey(key);
        }

        public void SetValue(string key, string name, RegValue value)
        {
            if (!_data.TryGetValue(key, out var vals))
            {
                vals = new Dictionary<string, RegValue>(StringComparer.OrdinalIgnoreCase);
                _data[key] = vals;
            }
            vals[name] = value;
        }

        public IReadOnlyList<string> ValueNames(string key)
        {
            if (_data.TryGetValue(key, out var vals))
            {
                return vals.Keys.ToList();
            }
            return [];
        }

        public void Dispose()
        {
            _registry.Loaded.Remove(MountName);
        }
    }
}
