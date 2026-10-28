namespace NitrOS.Image.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;

public sealed class FakeImageServicing : IImageServicing
{
    public List<string> Calls { get; } = [];
    public List<MountedImage> Mounts { get; } = [];
    public List<ProvisionedApp> Apps { get; } = [];
    public Dictionary<string, string> Features { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Capabilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> PackagesList { get; } = [];
    public List<ImageEdition> EditionsList { get; } = [];

    public string? FailOn { get; set; }
    public string? CrashOn { get; set; }
    public MountStatus DefaultMountStatus { get; set; } = MountStatus.Ok;

    private void Check(string callName)
    {
        Calls.Add(callName);
        if (string.Equals(FailOn, callName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated failure on {callName}");
        }
        if (string.Equals(CrashOn, callName, StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedCrashException($"Simulated crash on {callName}");
        }
    }

    public IReadOnlyList<ImageEdition> Editions(string imageFile)
    {
        Check(nameof(Editions));
        return EditionsList;
    }

    public void Export(string sourceFile, int sourceIndex, string destinationFile)
    {
        Check(nameof(Export));
    }

    public void Mount(string imageFile, int index, string mountDir)
    {
        Check(nameof(Mount));
        Mounts.Add(new MountedImage(mountDir, imageFile, index, DefaultMountStatus));
    }

    public void Unmount(string mountDir, bool commit)
    {
        Check(nameof(Unmount));
        Mounts.RemoveAll(m => string.Equals(m.MountDir, mountDir, StringComparison.OrdinalIgnoreCase));
    }

    public void Remount(string mountDir)
    {
        Check(nameof(Remount));
    }

    public void CleanupMountpoints()
    {
        Check(nameof(CleanupMountpoints));
    }

    public IReadOnlyList<MountedImage> Mounted()
    {
        Check(nameof(Mounted));
        return Mounts;
    }

    public IReadOnlyList<ProvisionedApp> ProvisionedApps(string mountDir)
    {
        Check(nameof(ProvisionedApps));
        return Apps;
    }

    public DismOutcome RemoveProvisionedApp(string mountDir, string packageName)
    {
        Check(nameof(RemoveProvisionedApp));
        var removed = Apps.RemoveAll(a => string.Equals(a.PackageName, packageName, StringComparison.OrdinalIgnoreCase));
        return removed > 0 ? DismOutcome.Done : DismOutcome.NotPresent;
    }

    public string? FeatureState(string mountDir, string name)
    {
        Check(nameof(FeatureState));
        return Features.GetValueOrDefault(name);
    }

    public DismOutcome DisableFeature(string mountDir, string name)
    {
        Check(nameof(DisableFeature));
        if (!Features.ContainsKey(name)) return DismOutcome.NotPresent;
        Features[name] = "Disabled";
        return DismOutcome.Done;
    }

    public string? CapabilityState(string mountDir, string name)
    {
        Check(nameof(CapabilityState));
        return Capabilities.GetValueOrDefault(name);
    }

    public DismOutcome RemoveCapability(string mountDir, string name)
    {
        Check(nameof(RemoveCapability));
        if (!Capabilities.ContainsKey(name)) return DismOutcome.NotPresent;
        Capabilities[name] = "NotPresent";
        return DismOutcome.Done;
    }

    public IReadOnlyList<string> Packages(string mountDir)
    {
        Check(nameof(Packages));
        return PackagesList;
    }

    public DismOutcome RemovePackage(string mountDir, string identity)
    {
        Check(nameof(RemovePackage));
        var removed = PackagesList.RemoveAll(p => string.Equals(p, identity, StringComparison.OrdinalIgnoreCase));
        return removed > 0 ? DismOutcome.Done : DismOutcome.NotPresent;
    }
}
