namespace NitrOS.Updates.Versions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class VersionText
{
    private static readonly Regex TitleVersionRegex = new(@"\d+(\.\d+){1,3}", RegexOptions.Compiled);

    public static bool TryParse(string? text, out IReadOnlyList<long> parts)
    {
        parts = [];
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();
        if (trimmed.StartsWith('V') || trimmed.StartsWith('v'))
        {
            trimmed = trimmed[1..];
        }

        var dotIndex = trimmed.LastIndexOf('.');
        if (dotIndex > 0)
        {
            var prefix = trimmed[..dotIndex];
            var suffix = trimmed[(dotIndex + 1)..];
            if (prefix.Any(char.IsLetter) && suffix.All(char.IsDigit) && suffix.Length > 0)
            {
                trimmed = suffix;
            }
        }

        var tokens = trimmed.Split('.', StringSplitOptions.None);
        if (tokens.Length == 0) return false;

        var list = new List<long>();
        foreach (var tok in tokens)
        {
            if (string.IsNullOrEmpty(tok) || !tok.All(char.IsDigit)) return false;
            if (!long.TryParse(tok, out var val)) return false;
            list.Add(val);
        }

        parts = list;
        return true;
    }

    public static int? Compare(string? a, string? b)
    {
        if (!TryParse(a, out var partsA) || !TryParse(b, out var partsB)) return null;

        var maxLen = Math.Max(partsA.Count, partsB.Count);
        for (var i = 0; i < maxLen; i++)
        {
            var valA = i < partsA.Count ? partsA[i] : 0L;
            var valB = i < partsB.Count ? partsB[i] : 0L;
            var cmp = valA.CompareTo(valB);
            if (cmp != 0) return cmp;
        }

        return 0;
    }

    public static string? FromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var matches = TitleVersionRegex.Matches(title);
        if (matches.Count == 0) return null;
        return matches[^1].Value;
    }
}
