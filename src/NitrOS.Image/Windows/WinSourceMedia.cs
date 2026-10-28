namespace NitrOS.Image.Windows;

using System;
using System.IO;
using System.Text.RegularExpressions;
using NitrOS.Image.Systems;
using NitrOS.Live.Windows;

public sealed partial class WinSourceMedia : ISourceMedia
{
    [GeneratedRegex(@"DriveLetter\s*:\s*([A-Za-z])", RegexOptions.IgnoreCase)]
    private static partial Regex DriveLetterRegex();

    public IMountedSource Open(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (Directory.Exists(source))
        {
            return new DirectoryMediaMount(Path.GetFullPath(source));
        }

        if (File.Exists(source))
        {
            var fullPath = Path.GetFullPath(source);
            // Run powershell to mount disk image read-only
            var escaped = fullPath.Replace("'", "''");
            var cmd = $"Mount-DiskImage -ImagePath '{escaped}' -Access ReadOnly -PassThru | Get-Volume | Format-List DriveLetter";
            var (exitCode, stdOut, stdErr) = ProcessRunner.Run("powershell.exe", ["-NoProfile", "-Command", cmd], TimeSpan.FromMinutes(2));

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Mount-DiskImage failed for '{source}': {stdErr}");
            }

            var m = DriveLetterRegex().Match(stdOut);
            if (!m.Success)
            {
                throw new InvalidOperationException($"Could not detect drive letter for mounted ISO '{source}'. Output: {stdOut}");
            }

            var letter = m.Groups[1].Value.ToUpperInvariant();
            var rootDir = $"{letter}:\\";
            return new IsoMediaMount(fullPath, rootDir);
        }

        throw new FileNotFoundException($"Source path does not exist: {source}", source);
    }

    private sealed class DirectoryMediaMount(string rootDir) : IMountedSource
    {
        public string Root => rootDir;
        public void Dispose() { }
    }

    private sealed class IsoMediaMount(string imagePath, string rootDir) : IMountedSource
    {
        public string Root => rootDir;

        public void Dispose()
        {
            try
            {
                var escaped = imagePath.Replace("'", "''");
                ProcessRunner.Run("powershell.exe", ["-NoProfile", "-Command", $"Dismount-DiskImage -ImagePath '{escaped}'"], TimeSpan.FromMinutes(2));
            }
            catch
            {
                // ignore on cleanup
            }
        }
    }
}
