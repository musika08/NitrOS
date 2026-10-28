namespace NitrOS.Image.Building;

using System;
using System.IO;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;

public static class BuildChecks
{
    public const long MinFreeBytes = 30L * 1024 * 1024 * 1024;
    public static readonly Version MinHostDism = new(10, 0, 26100);
    public static readonly Version MinImage = new(10, 0, 26100);

    public static string? Paths(BuildRequest r, IImageFiles files)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(files);

        if (string.IsNullOrWhiteSpace(r.Source) || string.IsNullOrWhiteSpace(r.OutputIso))
        {
            return BuildTexts.NoSourceOrOutput;
        }

        if (!files.Exists(r.Source))
        {
            return BuildTexts.SourceMissing;
        }

        if (files.Exists(r.OutputIso))
        {
            return BuildTexts.OutputExists;
        }

        string fullSource;
        string fullOutput;
        try
        {
            fullSource = Path.GetFullPath(r.Source);
            fullOutput = Path.GetFullPath(r.OutputIso);
        }
        catch
        {
            return BuildTexts.NoSourceOrOutput;
        }

        if (string.Equals(fullSource, fullOutput, StringComparison.OrdinalIgnoreCase))
        {
            return BuildTexts.OutputInsideSource;
        }

        var sourcePrefix = fullSource.TrimEnd('\\') + "\\";
        if (fullOutput.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return BuildTexts.OutputInsideSource;
        }

        var outDir = Path.GetDirectoryName(fullOutput);
        if (string.IsNullOrWhiteSpace(outDir) || !files.Exists(outDir))
        {
            return BuildTexts.OutputFolderMissing;
        }

        var fs = files.FileSystem(outDir);
        if (!string.Equals(fs, "NTFS", StringComparison.OrdinalIgnoreCase))
        {
            return BuildTexts.NotNtfs;
        }

        var free = files.FreeBytes(outDir);
        if (free < MinFreeBytes)
        {
            return BuildTexts.LowDiskSpace;
        }

        return null;
    }

    public static string? HostDism(IHostFacts host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var v = host.DismVersion();
        if (v.Build < MinHostDism.Build)
        {
            return BuildTexts.HostDismTooOld;
        }
        return null;
    }

    public static string? Power(PowerState p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.HasBattery && p.OnBattery)
        {
            return BuildTexts.OnBattery;
        }
        return null;
    }
}
