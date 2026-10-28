namespace NitrOS.Image.Building;

using System;
using System.IO;
using System.Linq;
using NitrOS.Image.Systems;

public static class ServicingVersion
{
    public static Version? FromMount(string mountDir, IImageFiles files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mountDir);
        ArgumentNullException.ThrowIfNull(files);

        var servicingDir = Path.Combine(mountDir, "Windows", "servicing", "Version");
        var subFolders = files.SubFolders(servicingDir);

        Version? highest = null;
        foreach (var folder in subFolders)
        {
            var name = Path.GetFileName(folder);
            if (Version.TryParse(name, out var v))
            {
                if (highest is null || v > highest)
                {
                    highest = v;
                }
            }
        }

        return highest;
    }

    public static string? Check(Version hostDism, Version imageServicing)
    {
        ArgumentNullException.ThrowIfNull(hostDism);
        ArgumentNullException.ThrowIfNull(imageServicing);

        if (hostDism.Build < imageServicing.Build)
        {
            return $"This ISO needs a newer Windows image tool (DISM {imageServicing.Build} or later) than this PC has ({hostDism.Build}). Update Windows, or build on a PC with a newer Windows.";
        }

        return null;
    }
}
