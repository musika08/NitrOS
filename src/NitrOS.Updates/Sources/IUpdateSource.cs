using NitrOS.Updates.Model;

namespace NitrOS.Updates.Sources;

public enum SourceStatus
{
    Ok,
    Unavailable,
    NotForThisDevice
}

public sealed record SourceResult(
    SourceStatus Status,
    IReadOnlyList<UpdateItem> Items,
    string? Problem,
    Uri? FallbackPage);

public interface IUpdateSource
{
    string Id { get; }
    string Name { get; }
    bool AppliesTo(DeviceInfo device);
    Task<SourceResult> CheckAsync(DeviceInfo device, CancellationToken ct);
}
