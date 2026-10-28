namespace NitrOS.Image.Systems;

using System;
using System.Collections.Generic;
using NitrOS.Live.Systems;

public sealed record ImageEdition(
    int Index,
    string Name,
    string EditionId,
    string Architecture,
    Version Version,
    long SizeBytes);

public enum MountStatus
{
    Ok,
    NeedsRemount,
    Invalid
}

public sealed record MountedImage(
    string MountDir,
    string ImageFile,
    int Index,
    MountStatus Status);

public sealed record ProvisionedApp(
    string DisplayName,
    string PackageName);

public interface IImageServicing
{
    IReadOnlyList<ImageEdition> Editions(string imageFile);
    void Export(string sourceFile, int sourceIndex, string destinationFile);
    void Mount(string imageFile, int index, string mountDir);
    void Unmount(string mountDir, bool commit);
    void Remount(string mountDir);
    void CleanupMountpoints();
    IReadOnlyList<MountedImage> Mounted();

    IReadOnlyList<ProvisionedApp> ProvisionedApps(string mountDir);
    DismOutcome RemoveProvisionedApp(string mountDir, string packageName);

    string? FeatureState(string mountDir, string name);
    DismOutcome DisableFeature(string mountDir, string name);

    string? CapabilityState(string mountDir, string name);
    DismOutcome RemoveCapability(string mountDir, string name);

    IReadOnlyList<string> Packages(string mountDir);
    DismOutcome RemovePackage(string mountDir, string identity);
}
