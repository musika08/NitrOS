namespace NitrOS.Image.Offline;

using System;
using System.IO;
using System.Text.RegularExpressions;

public static partial class ImagePaths
{
    private static readonly Regex VariableRegex = new(@"%([^%]+)%", RegexOptions.Compiled);

    public static string Expand(string catalogPath, string mountRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountRoot);

        var trimmedMount = mountRoot.TrimEnd('\\');

        var expanded = VariableRegex.Replace(catalogPath, match =>
        {
            var varName = match.Groups[1].Value;
            if (string.Equals(varName, "SystemRoot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(varName, "WinDir", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(trimmedMount, "Windows");
            }
            if (string.Equals(varName, "SystemDrive", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedMount;
            }
            if (string.Equals(varName, "ProgramFiles", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(trimmedMount, "Program Files");
            }
            if (string.Equals(varName, "ProgramFiles(x86)", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(trimmedMount, "Program Files (x86)");
            }
            if (string.Equals(varName, "ProgramData", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(trimmedMount, "ProgramData");
            }

            throw new ArgumentException($"unknown variable: %{varName}%", nameof(catalogPath));
        });

        return expanded;
    }

    public static bool IsUnder(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            var fullPath = Path.GetFullPath(path);

            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
