namespace NitrOS.Image.Windows;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NitrOS.Image.Systems;

public static partial class WinImageDismParse
{
    [GeneratedRegex(@"Index\s*:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex IndexRegex();

    [GeneratedRegex(@"Name\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"Edition\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex EditionRegex();

    [GeneratedRegex(@"Architecture\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ArchRegex();

    [GeneratedRegex(@"Version\s*:\s*(\d+\.\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"Size\s*:\s*([\d,]+)\s*bytes", RegexOptions.IgnoreCase)]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"Mount Dir\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex MountDirRegex();

    [GeneratedRegex(@"Image File\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ImageFileRegex();

    [GeneratedRegex(@"Image Index\s*:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ImageIndexRegex();

    [GeneratedRegex(@"Status\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex StatusRegex();

    [GeneratedRegex(@"Display name\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex DisplayNameRegex();

    [GeneratedRegex(@"Package name\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex PackageNameRegex();

    [GeneratedRegex(@"State\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex StateRegex();

    [GeneratedRegex(@"Package Identity\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex PackageIdentityRegex();

    public static IReadOnlyList<int> ImageIndices(string output)
    {
        var list = new List<int>();
        var matches = IndexRegex().Matches(output);
        foreach (Match m in matches)
        {
            if (int.TryParse(m.Groups[1].Value, out var idx))
            {
                list.Add(idx);
            }
        }
        return list;
    }

    public static ImageEdition SingleEdition(string output, int index)
    {
        // Skip header lines (like tool version) before looking for fields
        var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var bodyLines = lines.SkipWhile(l => !l.TrimStart().StartsWith("Index", StringComparison.OrdinalIgnoreCase) && !l.TrimStart().StartsWith("Name", StringComparison.OrdinalIgnoreCase)).ToList();
        var body = string.Join("\n", bodyLines);

        var nameMatch = NameRegex().Match(body);
        var name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Windows";

        var editionMatch = EditionRegex().Match(body);
        var edition = editionMatch.Success ? editionMatch.Groups[1].Value.Trim() : name;

        var archMatch = ArchRegex().Match(body);
        var arch = archMatch.Success ? archMatch.Groups[1].Value.Trim() : "x64";

        var versionMatch = VersionRegex().Match(body);
        var version = versionMatch.Success && Version.TryParse(versionMatch.Groups[1].Value.Trim(), out var v)
            ? v
            : new Version(10, 0, 26100);

        var sizeMatch = SizeRegex().Match(body);
        long size = 0;
        if (sizeMatch.Success)
        {
            var raw = sizeMatch.Groups[1].Value.Replace(",", "").Trim();
            _ = long.TryParse(raw, out size);
        }

        return new ImageEdition(index, name, edition, arch, version, size);
    }

    public static IReadOnlyList<MountedImage> Mounted(string output)
    {
        var list = new List<MountedImage>();
        var blocks = output.Split(["Mounted images:", "Mounted image"], StringSplitOptions.RemoveEmptyEntries);

        var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        string? mountDir = null;
        string? imageFile = null;
        var imageIndex = 1;
        var status = MountStatus.Ok;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Mount Dir", StringComparison.OrdinalIgnoreCase))
            {
                if (mountDir != null && imageFile != null)
                {
                    list.Add(new MountedImage(mountDir, imageFile, imageIndex, status));
                    imageFile = null;
                    status = MountStatus.Ok;
                }
                var m = MountDirRegex().Match(line);
                mountDir = m.Success ? m.Groups[1].Value.Trim() : null;
            }
            else if (line.StartsWith("Image File", StringComparison.OrdinalIgnoreCase))
            {
                var m = ImageFileRegex().Match(line);
                imageFile = m.Success ? m.Groups[1].Value.Trim() : null;
            }
            else if (line.StartsWith("Image Index", StringComparison.OrdinalIgnoreCase))
            {
                var m = ImageIndexRegex().Match(line);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var idx)) imageIndex = idx;
            }
            else if (line.StartsWith("Status", StringComparison.OrdinalIgnoreCase))
            {
                var m = StatusRegex().Match(line);
                if (m.Success)
                {
                    var st = m.Groups[1].Value.Trim();
                    if (st.Contains("Invalid", StringComparison.OrdinalIgnoreCase)) status = MountStatus.Invalid;
                    else if (st.Contains("Remount", StringComparison.OrdinalIgnoreCase)) status = MountStatus.NeedsRemount;
                    else status = MountStatus.Ok;
                }
            }
        }

        if (mountDir != null && imageFile != null)
        {
            list.Add(new MountedImage(mountDir, imageFile, imageIndex, status));
        }

        return list;
    }

    public static IReadOnlyList<ProvisionedApp> ProvisionedApps(string output)
    {
        var list = new List<ProvisionedApp>();
        var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        string? displayName = null;

        foreach (var line in lines)
        {
            var dm = DisplayNameRegex().Match(line);
            if (dm.Success)
            {
                displayName = dm.Groups[1].Value.Trim();
                continue;
            }

            var pm = PackageNameRegex().Match(line);
            if (pm.Success && displayName != null)
            {
                var pkgName = pm.Groups[1].Value.Trim();
                list.Add(new ProvisionedApp(displayName, pkgName));
                displayName = null;
            }
        }

        return list;
    }

    public static string? FeatureOrCapabilityState(string output)
    {
        var m = StateRegex().Match(output);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public static IReadOnlyList<string> PackageIdentities(string output)
    {
        var list = new List<string>();
        var matches = PackageIdentityRegex().Matches(output);
        foreach (Match m in matches)
        {
            list.Add(m.Groups[1].Value.Trim());
        }
        return list;
    }

    public static bool IsAccessDenied(string output)
    {
        return output.Contains("0x80070005", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPending(string output)
    {
        return output.Contains("pending", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNotPresent(string output)
    {
        return output.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("is not recognized", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("0x800f0805", StringComparison.OrdinalIgnoreCase);
    }
}
