namespace NitrOS.Image.Building;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NitrOS.Image.Systems;

public sealed record LeftoverReport(
    IReadOnlyList<string> UnloadedHives,
    IReadOnlyList<string> DiscardedMounts,
    bool CleanedMountpoints,
    MountStatus? ResumeMount);

public static class Leftovers
{
    public static LeftoverReport Clear(ImageSystems s, BuildState? resumable, IProgress<BuildProgress>? progress)
    {
        ArgumentNullException.ThrowIfNull(s);

        var unloadedHives = new List<string>();
        var discardedMounts = new List<string>();
        var cleanedMountpoints = false;
        MountStatus? resumeMount = null;

        // 1. Unload hives
        var loadedHives = s.Registry.LoadedNitrOSHives();
        foreach (var hiveName in loadedHives)
        {
            var unloaded = false;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    s.Registry.Unload(hiveName);
                    unloaded = true;
                    unloadedHives.Add(hiveName);
                    break;
                }
                catch
                {
                    if (attempt < 3) Thread.Sleep(200);
                }
            }

            if (!unloaded)
            {
                throw new InvalidOperationException("A registry hive from an earlier build is still in use. Restart the PC, then try again.");
            }
        }

        // 2. Mounted images
        string? resumableMountDir = null;
        if (resumable is not null)
        {
            resumableMountDir = Path.Combine(resumable.WorkFolder, "mount");
        }

        var mounted = s.Dism.Mounted().ToList();
        var hadInvalidOrFailed = false;

        foreach (var m in mounted)
        {
            if (resumableMountDir is not null && string.Equals(m.MountDir, resumableMountDir, StringComparison.OrdinalIgnoreCase))
            {
                resumeMount = m.Status;
                if (m.Status == MountStatus.Invalid)
                {
                    hadInvalidOrFailed = true;
                }
                continue;
            }

            // Check if folder is named "mount" inside a "NitrOS-work-*" folder
            var dirName = Path.GetFileName(m.MountDir);
            var parentDir = Path.GetDirectoryName(m.MountDir);
            var parentName = parentDir is not null ? Path.GetFileName(parentDir) : "";

            if (string.Equals(dirName, "mount", StringComparison.OrdinalIgnoreCase) &&
                parentName.StartsWith("NitrOS-work-", StringComparison.OrdinalIgnoreCase))
            {
                if (m.Status == MountStatus.Invalid)
                {
                    hadInvalidOrFailed = true;
                }

                try
                {
                    s.Dism.Unmount(m.MountDir, commit: false);
                    discardedMounts.Add(m.MountDir);
                }
                catch
                {
                    hadInvalidOrFailed = true;
                }
            }
        }

        // 3. Cleanup mountpoints if invalid or unmount failed
        if (hadInvalidOrFailed)
        {
            try
            {
                s.Dism.CleanupMountpoints();
                cleanedMountpoints = true;
            }
            catch
            {
                // ignore
            }
        }

        return new LeftoverReport(unloadedHives, discardedMounts, cleanedMountpoints, resumeMount);
    }
}
