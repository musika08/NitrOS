using System.Text.RegularExpressions;

namespace NitrOS.Live;

/// <summary>Case-insensitive match where the star is the only wildcard.</summary>
public static class Glob
{
    public static bool IsMatch(string pattern, string text) =>
        Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
}
