namespace NitrOS.Image.Windows;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

public sealed partial class WinImageServicing : IImageServicing
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DismountTimeout = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan RemoveTimeout = TimeSpan.FromMinutes(30);

    private readonly string _dismPath;
    private readonly string _logDir;
    private int _logCounter;

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.~]+$")]
    private static partial Regex SafeNameRegex();

    public WinImageServicing(string? logDir = null)
    {
        _dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");
        _logDir = logDir ?? Path.Combine(Path.GetTempPath(), "nitros-dism-logs");
        Directory.CreateDirectory(_logDir);
    }

    private string NextLogPath()
    {
        var n = System.Threading.Interlocked.Increment(ref _logCounter);
        return Path.Combine(_logDir, $"dism-{n}.log");
    }

    private (int ExitCode, string StdOut, string StdErr) RunDism(IReadOnlyList<string> args, TimeSpan timeout)
    {
        var fullArgs = new List<string> { "/English", $"/LogPath:{NextLogPath()}" };
        fullArgs.AddRange(args);

        var (exitCode, stdOut, stdErr) = ProcessRunner.Run(_dismPath, fullArgs, timeout);
        var combined = $"{stdOut}\n{stdErr}";

        if (WinImageDismParse.IsAccessDenied(combined))
        {
            throw new ImageAccessDeniedException();
        }

        return (exitCode, stdOut, stdErr);
    }

    private static void ValidateSafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !SafeNameRegex().IsMatch(name))
        {
            throw new ArgumentException($"Invalid or unsafe DISM item name: '{name}'", nameof(name));
        }
    }

    public IReadOnlyList<ImageEdition> Editions(string imageFile)
    {
        var (_, stdOut, _) = RunDism(["/Get-ImageInfo", $"/ImageFile:{imageFile}"], QueryTimeout);
        var indices = WinImageDismParse.ImageIndices(stdOut);

        var editions = new List<ImageEdition>();
        foreach (var idx in indices)
        {
            var (_, singleOut, _) = RunDism(["/Get-ImageInfo", $"/ImageFile:{imageFile}", $"/Index:{idx}"], QueryTimeout);
            editions.Add(WinImageDismParse.SingleEdition(singleOut, idx));
        }

        return editions;
    }

    public void Export(string sourceFile, int sourceIndex, string destinationFile)
    {
        RunDism(["/Export-Image", $"/SourceImageFile:{sourceFile}", $"/SourceIndex:{sourceIndex}", $"/DestinationImageFile:{destinationFile}"], DismountTimeout);
    }

    public void Mount(string imageFile, int index, string mountDir)
    {
        RunDism(["/Mount-Image", $"/ImageFile:{imageFile}", $"/Index:{index}", $"/MountDir:{mountDir}"], DismountTimeout);
    }

    public void Unmount(string mountDir, bool commit)
    {
        var action = commit ? "/Commit" : "/Discard";
        RunDism(["/Unmount-Image", $"/MountDir:{mountDir}", action], DismountTimeout);
    }

    public void Remount(string mountDir)
    {
        RunDism(["/Remount-Image", $"/MountDir:{mountDir}"], DismountTimeout);
    }

    public void CleanupMountpoints()
    {
        RunDism(["/Cleanup-Mountpoints"], DismountTimeout);
    }

    public IReadOnlyList<MountedImage> Mounted()
    {
        var (_, stdOut, _) = RunDism(["/Get-MountedImageInfo"], QueryTimeout);
        return WinImageDismParse.Mounted(stdOut);
    }

    public IReadOnlyList<ProvisionedApp> ProvisionedApps(string mountDir)
    {
        var (_, stdOut, _) = RunDism(["/Image:" + mountDir, "/Get-ProvisionedAppxPackages"], QueryTimeout);
        return WinImageDismParse.ProvisionedApps(stdOut);
    }

    public DismOutcome RemoveProvisionedApp(string mountDir, string packageName)
    {
        ValidateSafeName(packageName);
        var (exitCode, stdOut, stdErr) = RunDism(["/Image:" + mountDir, "/Remove-ProvisionedAppxPackage", $"/PackageName:{packageName}"], RemoveTimeout);
        var combined = $"{stdOut}\n{stdErr}";

        if (exitCode == 3010) return DismOutcome.RebootRequired;
        if (WinImageDismParse.IsPending(combined)) return DismOutcome.Deferred;
        if (WinImageDismParse.IsNotPresent(combined)) return DismOutcome.NotPresent;
        if (exitCode != 0) throw new InvalidOperationException($"DISM remove app failed with exit code {exitCode}: {combined}");

        return DismOutcome.Done;
    }

    public string? FeatureState(string mountDir, string name)
    {
        ValidateSafeName(name);
        var (_, stdOut, _) = RunDism(["/Image:" + mountDir, "/Get-FeatureInfo", $"/FeatureName:{name}"], QueryTimeout);
        return WinImageDismParse.FeatureOrCapabilityState(stdOut);
    }

    public DismOutcome DisableFeature(string mountDir, string name)
    {
        ValidateSafeName(name);
        var (exitCode, stdOut, stdErr) = RunDism(["/Image:" + mountDir, "/Disable-Feature", $"/FeatureName:{name}"], RemoveTimeout);
        var combined = $"{stdOut}\n{stdErr}";

        if (exitCode == 3010) return DismOutcome.RebootRequired;
        if (WinImageDismParse.IsPending(combined)) return DismOutcome.Deferred;
        if (WinImageDismParse.IsNotPresent(combined)) return DismOutcome.NotPresent;
        if (exitCode != 0) throw new InvalidOperationException($"DISM disable feature failed with exit code {exitCode}: {combined}");

        return DismOutcome.Done;
    }

    public string? CapabilityState(string mountDir, string name)
    {
        ValidateSafeName(name);
        var (_, stdOut, _) = RunDism(["/Image:" + mountDir, "/Get-CapabilityInfo", $"/CapabilityName:{name}"], QueryTimeout);
        return WinImageDismParse.FeatureOrCapabilityState(stdOut);
    }

    public DismOutcome RemoveCapability(string mountDir, string name)
    {
        ValidateSafeName(name);
        var (exitCode, stdOut, stdErr) = RunDism(["/Image:" + mountDir, "/Remove-Capability", $"/CapabilityName:{name}"], RemoveTimeout);
        var combined = $"{stdOut}\n{stdErr}";

        if (exitCode == 3010) return DismOutcome.RebootRequired;
        if (WinImageDismParse.IsPending(combined)) return DismOutcome.Deferred;
        if (WinImageDismParse.IsNotPresent(combined)) return DismOutcome.NotPresent;
        if (exitCode != 0) throw new InvalidOperationException($"DISM remove capability failed with exit code {exitCode}: {combined}");

        return DismOutcome.Done;
    }

    public IReadOnlyList<string> Packages(string mountDir)
    {
        var (_, stdOut, _) = RunDism(["/Image:" + mountDir, "/Get-Packages"], QueryTimeout);
        return WinImageDismParse.PackageIdentities(stdOut);
    }

    public DismOutcome RemovePackage(string mountDir, string identity)
    {
        ValidateSafeName(identity);
        var (exitCode, stdOut, stdErr) = RunDism(["/Image:" + mountDir, "/Remove-Package", $"/PackageName:{identity}"], RemoveTimeout);
        var combined = $"{stdOut}\n{stdErr}";

        if (exitCode == 3010) return DismOutcome.RebootRequired;
        if (WinImageDismParse.IsPending(combined)) return DismOutcome.Deferred;
        if (WinImageDismParse.IsNotPresent(combined)) return DismOutcome.NotPresent;
        if (exitCode != 0) throw new InvalidOperationException($"DISM remove package failed with exit code {exitCode}: {combined}");

        return DismOutcome.Done;
    }
}
