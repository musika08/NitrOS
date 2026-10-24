using System.Globalization;
using Microsoft.Win32;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

public sealed class WinRegistry : IRegistry
{
    private static RegistryKey Base(LiveHive h, RegView v) => RegistryKey.OpenBaseKey(
        h == LiveHive.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
        v == RegView.Registry32 ? Microsoft.Win32.RegistryView.Registry32 : Microsoft.Win32.RegistryView.Registry64);

    public RegValue? GetValue(LiveHive h, string key, string name, RegView v)
    {
        using var b = Base(h, v);
        using var k = b.OpenSubKey(key);
        return k is null ? null : Read(k, key, name);
    }

    public void SetValue(LiveHive h, string key, string name, RegValue value, RegView v)
    {
        using var b = Base(h, v);
        using var k = b.CreateSubKey(key, writable: true);
        Write(k, name, value);
    }

    public void DeleteValue(LiveHive h, string key, string name, RegView v)
    {
        using var b = Base(h, v);
        using var k = b.OpenSubKey(key, writable: true);
        k?.DeleteValue(name, throwOnMissingValue: false);
    }

    public IReadOnlyList<string> GetValueNames(LiveHive h, string key, RegView v)
    {
        using var b = Base(h, v);
        using var k = b.OpenSubKey(key);
        return k is null ? Array.Empty<string>() : k.GetValueNames();
    }

    public RegTree? ExportKey(LiveHive h, string key, RegView v)
    {
        using var b = Base(h, v);
        using var k = b.OpenSubKey(key);
        return k is null ? null : Export(k, key);
    }

    public void ImportKey(LiveHive h, RegTree tree, RegView v)
    {
        using var b = Base(h, v);
        Import(b, tree);
    }

    public void DeleteKey(LiveHive h, string key, RegView v)
    {
        using var b = Base(h, v);
        b.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
    }

    public bool KeyExists(LiveHive h, string key, RegView v)
    {
        using var b = Base(h, v);
        using var k = b.OpenSubKey(key);
        return k is not null;
    }

    /// <summary>
    /// Opens the key (or, when it is missing, its nearest existing parent) with SetValue and CreateSubKey rights and closes it
    /// again; nothing is written. Access denied means Windows refuses changes there.
    /// </summary>
    public bool CanWrite(LiveHive h, string key, RegView v)
    {
        using var b = Base(h, v);
        var path = key.Trim('\\');
        while (path.Length > 0)
        {
            bool exists;
            using (var probe = b.OpenSubKey(path)) exists = probe is not null;
            if (exists)
            {
                try
                {
                    using var k = b.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree,
                        System.Security.AccessControl.RegistryRights.SetValue | System.Security.AccessControl.RegistryRights.CreateSubKey);
                    return k is not null;
                }
                catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException)
                {
                    return false;
                }
            }
            var cut = path.LastIndexOf('\\');
            path = cut < 0 ? "" : path[..cut];
        }
        return false; // nothing on the way exists: no top-level key can be created under a hive root
    }

    private static RegTree Export(RegistryKey k, string path)
    {
        var values = new List<RegNamedValue>();
        foreach (var n in k.GetValueNames())
            if (Read(k, path, n) is { } rv) values.Add(new RegNamedValue(n, rv));
        var subs = new List<RegTree>();
        foreach (var s in k.GetSubKeyNames())
        {
            using var sk = k.OpenSubKey(s);
            if (sk is not null) subs.Add(Export(sk, path + "\\" + s));
        }
        return new RegTree(path, values, subs);
    }

    private static void Import(RegistryKey root, RegTree tree)
    {
        using (var k = root.CreateSubKey(tree.Key, writable: true))
            foreach (var nv in tree.Values) Write(k, nv.Name, nv.Value);
        foreach (var sub in tree.SubKeys) Import(root, sub);
    }

    private static RegValue? Read(RegistryKey k, string keyPath, string name)
    {
        if (!k.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
        var kind = k.GetValueKind(name);
        var data = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (data is null) return null;
        return kind switch
        {
            RegistryValueKind.DWord => new RegValue(RegistryKind.Dword, unchecked((uint)(int)data).ToString(CultureInfo.InvariantCulture)),
            RegistryValueKind.QWord => new RegValue(RegistryKind.Qword, unchecked((ulong)(long)data).ToString(CultureInfo.InvariantCulture)),
            RegistryValueKind.String => new RegValue(RegistryKind.String, (string)data),
            RegistryValueKind.ExpandString => new RegValue(RegistryKind.ExpandString, (string)data),
            RegistryValueKind.MultiString => new RegValue(RegistryKind.MultiString, string.Join("\n", (string[])data)),
            RegistryValueKind.Binary => new RegValue(RegistryKind.Binary, Convert.ToHexString((byte[])data)),
            _ => throw new NotSupportedException($"Unsupported registry value kind {kind} at {keyPath}\\{name}"),
        };
    }

    private static void Write(RegistryKey k, string name, RegValue value)
    {
        switch (value.Kind)
        {
            case RegistryKind.Dword:
                k.SetValue(name, unchecked((int)(uint)ParseNumber(value.Data, 32)), RegistryValueKind.DWord); break;
            case RegistryKind.Qword:
                k.SetValue(name, unchecked((long)ParseNumber(value.Data, 64)), RegistryValueKind.QWord); break;
            case RegistryKind.String:
                k.SetValue(name, value.Data, RegistryValueKind.String); break;
            case RegistryKind.ExpandString:
                k.SetValue(name, value.Data, RegistryValueKind.ExpandString); break;
            case RegistryKind.MultiString:
                k.SetValue(name, value.Data.Length == 0 ? Array.Empty<string>() : value.Data.Replace("\r\n", "\n").Split('\n'), RegistryValueKind.MultiString); break;
            case RegistryKind.Binary:
                k.SetValue(name, Convert.FromHexString(value.Data), RegistryValueKind.Binary); break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unknown registry kind");
        }
    }

    /// <summary>Decimal or 0x hex, the same rule as the catalog validator.</summary>
    private static ulong ParseNumber(string s, int bits)
    {
        var ok = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.TryParse(s.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v)
            : ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out v);
        if (!ok || (bits == 32 && v > uint.MaxValue)) throw new FormatException($"'{s}' is not a valid {bits}-bit number");
        return v;
    }
}
