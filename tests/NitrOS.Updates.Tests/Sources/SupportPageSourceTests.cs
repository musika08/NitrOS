using System.Text.Json;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;
using NitrOS.Updates.Net;
using NitrOS.Updates.Sources;
using NitrOS.Updates.Tests.Fakes;
using Xunit;

namespace NitrOS.Updates.Tests.Sources;

public sealed class SupportPageSourceTests
{
    private static DeviceInfo LoadAllyFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ally-x-device-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.Deserialize<DeviceInfo>(json, options)!;
    }

    [Fact]
    public async Task SupportPage_MsiGetsPageButton()
    {
        var msiDevice = LoadAllyFixture() with { Maker = MakerId.Msi };
        var source = new SupportPageSource(MakerId.Msi);

        Assert.True(source.AppliesTo(msiDevice));
        var result = await source.CheckAsync(msiDevice, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Empty(result.Items);
        Assert.NotNull(result.FallbackPage);
        Assert.Contains("msi.com", result.FallbackPage.ToString());
    }

    [Fact]
    public void SupportPage_NeverForAsus()
    {
        var ally = LoadAllyFixture();
        var source = new SupportPageSource(MakerId.Asus);

        Assert.False(source.AppliesTo(ally));
    }

    [Fact]
    public void SupportPage_OtherMakerHasNone()
    {
        var otherDevice = LoadAllyFixture() with { Maker = MakerId.Other };
        var source = new SupportPageSource(MakerId.Other);

        Assert.False(source.AppliesTo(otherDevice));
    }

    [Fact]
    public void SourceSet_OrderWindowsUpdateFirstThenAsus()
    {
        var sources = SourceSet.Default(new FakeUpdateAgent(), HttpFactory.Create(new FakeHttpHandler()), FirmwareMap.Shipped());
        Assert.Equal("windows-update", sources[0].Id);
        Assert.Equal("asus", sources[1].Id);
    }
}
