using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;
using NitrOS.Updates.Net;
using NitrOS.Updates.Sources;
using Xunit;

namespace NitrOS.Updates.Tests.LiveNet;

public sealed class AsusLiveTests
{
    [Fact]
    [Trait("Lane", "LiveNet")]
    public async Task AsusLive_ListingStillParses()
    {
        var client = HttpFactory.Create();
        var source = new AsusSource(client, FirmwareMap.Shipped());

        var ally = new DeviceInfo(
            MakerId.Asus,
            "ASUSTeK COMPUTER INC.",
            "ROG Xbox Ally X RC73XA_RC73XA",
            "RC73XA",
            "RC73XA.319",
            null,
            Array.Empty<InstalledDevice>(),
            Array.Empty<FirmwareResource>());

        var result = await source.CheckAsync(ally, CancellationToken.None);
        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, i => i.Kind == UpdateKind.Bios);
        foreach (var item in result.Items)
        {
            if (item.DownloadUrl != null)
            {
                Assert.Equal("dlcdnets.asus.com", item.DownloadUrl.Host);
            }
        }
    }
}
