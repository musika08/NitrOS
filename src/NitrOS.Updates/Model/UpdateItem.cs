namespace NitrOS.Updates.Model;

using System;

public enum InstallMethod
{
    WindowsUpdate,
    MakerPackage,
    HandOff,
    PageOnly
}

public sealed record UpdateItem(
    string Key,
    string Name,
    UpdateKind Kind,
    string? InstalledVersion,
    string? LatestVersion,
    Uri? DownloadUrl,
    long? SizeBytes,
    DateOnly? ReleaseDate,
    string SourceId,
    string SourceName)
{
    public string? Sha256 { get; init; }
    public InstallMethod Method { get; init; } = InstallMethod.HandOff;
    public string? SilentArgs { get; init; }
    public Uri? Page { get; init; }
    public string? Note { get; init; }
    public string? WindowsUpdateId { get; init; }
    public bool RestartLikely { get; init; }
    public string? CompareRule { get; init; }
}
