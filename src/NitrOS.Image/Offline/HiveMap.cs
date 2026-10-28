namespace NitrOS.Image.Offline;

using System;
using System.IO;

public enum OfflineHive
{
    Software,
    System,
    DefaultUser
}

public sealed record HiveTarget(OfflineHive Hive, string SubKey);

public static class HiveMap
{
    public const string SelectKey = "Select";
    public const string SelectValue = "Current";

    public static string FileFor(OfflineHive h, string mountRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mountRoot);
        var trimmed = mountRoot.TrimEnd('\\');

        return h switch
        {
            OfflineHive.Software => Path.Combine(trimmed, "Windows", "System32", "config", "SOFTWARE"),
            OfflineHive.System => Path.Combine(trimmed, "Windows", "System32", "config", "SYSTEM"),
            OfflineHive.DefaultUser => Path.Combine(trimmed, "Users", "Default", "NTUSER.DAT"),
            _ => throw new ArgumentOutOfRangeException(nameof(h), h, null)
        };
    }

    public static HiveTarget Map(string catalogHive, string key, int currentControlSet, bool registry32 = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogHive);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedHive = catalogHive.Trim().ToUpperInvariant();
        var trimmedKey = key.Trim().TrimStart('\\');

        if (normalizedHive is "HKCU" or "HKEY_CURRENT_USER")
        {
            return new HiveTarget(OfflineHive.DefaultUser, trimmedKey);
        }

        if (normalizedHive is "HKLM" or "HKEY_LOCAL_MACHINE")
        {
            if (trimmedKey.StartsWith("SOFTWARE\\", StringComparison.OrdinalIgnoreCase))
            {
                var subKey = trimmedKey["SOFTWARE\\".Length..];
                if (registry32)
                {
                    if (!subKey.StartsWith("WOW6432Node\\", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(subKey, "WOW6432Node", StringComparison.OrdinalIgnoreCase))
                    {
                        subKey = $"WOW6432Node\\{subKey}";
                    }
                }
                return new HiveTarget(OfflineHive.Software, subKey);
            }

            if (trimmedKey.StartsWith("SYSTEM\\CurrentControlSet\\", StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmedKey["SYSTEM\\CurrentControlSet\\".Length..];
                var ccsName = $"ControlSet{currentControlSet:D3}";
                return new HiveTarget(OfflineHive.System, $"{ccsName}\\{rest}");
            }

            if (trimmedKey.StartsWith("SYSTEM\\", StringComparison.OrdinalIgnoreCase))
            {
                var rest = trimmedKey["SYSTEM\\".Length..];
                return new HiveTarget(OfflineHive.System, rest);
            }

            throw new NotSupportedException($"Unsupported HKLM root key: {key}");
        }

        throw new NotSupportedException($"Unsupported hive: {catalogHive}");
    }
}
