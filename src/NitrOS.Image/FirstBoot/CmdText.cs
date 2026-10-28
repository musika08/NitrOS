namespace NitrOS.Image.FirstBoot;

using System;
using System.Collections.Generic;
using System.Linq;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;

public sealed class UnsafeTextException : ArgumentException
{
    public UnsafeTextException(string message) : base(message)
    {
    }
}

public static class CmdText
{
    public static bool IsSafe(string s)
    {
        if (string.IsNullOrEmpty(s)) return true;

        foreach (var c in s)
        {
            if (c < 32 || c > 126) return false;
            if (c is '"' or '%' or '^' or '&' or '|' or '<' or '>' or '!') return false;
        }

        return !s.Contains('\r') && !s.Contains('\n');
    }

    private static void EnsureSafe(string? s, string paramName)
    {
        if (s is not null && !IsSafe(s))
        {
            throw new UnsafeTextException($"Text contains unsafe command characters: {s}");
        }
    }

    public static string RegAdd(string fullKey, string name, RegValue v)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullKey);
        ArgumentNullException.ThrowIfNull(v);

        EnsureSafe(fullKey, nameof(fullKey));
        EnsureSafe(name, nameof(name));
        EnsureSafe(v.Data, nameof(v.Data));

        var namePart = string.IsNullOrEmpty(name) ? "/ve" : $"/v \"{name}\"";

        var (typeStr, dataPart) = v.Kind switch
        {
            RegistryKind.Dword => ("REG_DWORD", $"/d {v.Data}"),
            RegistryKind.Qword => ("REG_QWORD", $"/d {v.Data}"),
            RegistryKind.String => ("REG_SZ", $"/d \"{v.Data}\""),
            RegistryKind.ExpandString => ("REG_EXPAND_SZ", $"/d \"{v.Data}\""),
            RegistryKind.MultiString => ("REG_MULTI_SZ", $"/d \"{v.Data}\""),
            RegistryKind.Binary => ("REG_BINARY", $"/d {v.Data}"),
            _ => throw new ArgumentOutOfRangeException(nameof(v.Kind), v.Kind, null)
        };

        return $"reg add \"{fullKey}\" {namePart} /t {typeStr} {dataPart} /f";
    }

    public static string RegDelete(string fullKey, string? name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullKey);
        EnsureSafe(fullKey, nameof(fullKey));
        EnsureSafe(name, nameof(name));

        return name is not null
            ? $"reg delete \"{fullKey}\" /v \"{name}\" /f"
            : $"reg delete \"{fullKey}\" /f";
    }

    public static string TaskOff(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnsureSafe(path, nameof(path));

        return $"schtasks /Change /TN \"{path}\" /DISABLE";
    }

    public static string Power(string sub, string setting, int? ac, int? dc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        ArgumentException.ThrowIfNullOrWhiteSpace(setting);
        EnsureSafe(sub, nameof(sub));
        EnsureSafe(setting, nameof(setting));

        var lines = new List<string>();
        if (ac.HasValue)
        {
            lines.Add($"powercfg /setacvalueindex SCHEME_CURRENT {sub} {setting} {ac.Value}");
        }
        if (dc.HasValue)
        {
            lines.Add($"powercfg /setdcvalueindex SCHEME_CURRENT {sub} {setting} {dc.Value}");
        }
        lines.Add("powercfg /setactive SCHEME_CURRENT");

        return string.Join("\r\n", lines);
    }
}
