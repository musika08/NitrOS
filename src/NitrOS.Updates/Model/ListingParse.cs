namespace NitrOS.Updates.Model;

using System;
using System.Globalization;

public static class ListingParse
{
    public static long? SizeBytes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();
        var factor = 1L;

        if (trimmed.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            factor = 1024L * 1024L * 1024L;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            factor = 1024L * 1024L;
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            factor = 1024L;
            trimmed = trimmed[..^2].Trim();
        }
        else
        {
            return null;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
        {
            return (long)Math.Round(num * factor);
        }

        return null;
    }

    public static DateOnly? AsusDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateOnly.TryParseExact(text.Trim(), "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }
        return null;
    }
}
