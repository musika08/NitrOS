namespace NitrOS.Updates.Versions;

using System;
using System.Globalization;
using System.Linq;

public static class FirmwareNumber
{
    public static bool TryEsrtHex(string? listingVersion, out uint esrt)
    {
        esrt = 0;
        if (string.IsNullOrWhiteSpace(listingVersion)) return false;

        var trimmed = listingVersion.Trim();
        if (trimmed.StartsWith('V') || trimmed.StartsWith('v'))
        {
            trimmed = trimmed[1..];
        }

        var tokens = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;

        var last = tokens[^1];
        if (string.IsNullOrEmpty(last)) return false;

        return uint.TryParse(last, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out esrt);
    }

    public static bool TryPackageLastField(string? packageVersion, out long last)
    {
        last = 0;
        if (string.IsNullOrWhiteSpace(packageVersion)) return false;

        var tokens = packageVersion.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;

        return long.TryParse(tokens[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out last);
    }
}
