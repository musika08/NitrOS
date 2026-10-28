namespace NitrOS.Image.Iso;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NitrOS.Image.Systems;

public static class BootOrderFile
{
    public static IReadOnlyList<string> Lines(string mediaRoot, IImageFiles files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaRoot);
        ArgumentNullException.ThrowIfNull(files);

        var result = new List<string>();
        var root = mediaRoot.TrimEnd('\\') + "\\";

        // Find all files under boot\ and efi\
        var bootDir = Path.Combine(mediaRoot, "boot");
        var efiDir = Path.Combine(mediaRoot, "efi");

        // Helper to collect files
        var allFiles = new List<string>();
        allFiles.AddRange(files.EnumerateFiles(bootDir, "*", true));
        allFiles.AddRange(files.EnumerateFiles(efiDir, "*", true));

        var matching = allFiles
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(k => k.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? k[root.Length..] : k);

        result.AddRange(matching);

        // Add sources\boot.wim
        result.Add(@"sources\boot.wim");

        return result;
    }

    public static void Write(string mediaRoot, string path, IImageFiles files)
    {
        var lines = Lines(mediaRoot, files);
        var text = string.Join("\r\n", lines) + "\r\n";
        files.WriteText(path, text);
    }
}
