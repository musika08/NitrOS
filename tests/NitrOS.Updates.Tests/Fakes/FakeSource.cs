using NitrOS.Updates.Model;
using NitrOS.Updates.Sources;

namespace NitrOS.Updates.Tests.Fakes;

public sealed class FakeSource(string id, string name) : IUpdateSource
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public Func<DeviceInfo, bool>? OnAppliesTo { get; set; }
    public Func<DeviceInfo, CancellationToken, Task<SourceResult>>? OnCheck { get; set; }

    public bool AppliesTo(DeviceInfo device) => OnAppliesTo?.Invoke(device) ?? true;

    public Task<SourceResult> CheckAsync(DeviceInfo device, CancellationToken ct)
    {
        if (OnCheck != null)
        {
            return OnCheck(device, ct);
        }

        return Task.FromResult(new SourceResult(SourceStatus.Ok, Array.Empty<UpdateItem>(), null, null));
    }
}
