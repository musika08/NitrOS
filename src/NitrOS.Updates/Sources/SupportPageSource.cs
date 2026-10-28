using NitrOS.Updates.Model;

namespace NitrOS.Updates.Sources;

public sealed class SupportPageSource(MakerId maker) : IUpdateSource
{
    public MakerId Maker => maker;

    public string Id => $"page-{maker.ToString().ToLowerInvariant()}";
    public string Name => maker.ToString();

    public bool AppliesTo(DeviceInfo device)
    {
        // Applies when maker matches and maker has no listing source (MSI, Lenovo, Dell, HP)
        // Never applies to ASUS or Other
        return device.Maker == maker &&
               maker != MakerId.Asus &&
               maker != MakerId.Other;
    }

    public Task<SourceResult> CheckAsync(DeviceInfo device, CancellationToken ct)
    {
        if (!AppliesTo(device))
        {
            return Task.FromResult(new SourceResult(SourceStatus.NotForThisDevice, Array.Empty<UpdateItem>(), null, null));
        }

        var page = SupportPages.For(maker, device.Model);
        return Task.FromResult(new SourceResult(SourceStatus.Ok, Array.Empty<UpdateItem>(), null, page));
    }
}
