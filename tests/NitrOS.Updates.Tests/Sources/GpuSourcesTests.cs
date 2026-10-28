using System.Text.Json;
using NitrOS.Updates.Model;
using NitrOS.Updates.Sources;
using Xunit;

namespace NitrOS.Updates.Tests.Sources;

public sealed class GpuSourcesTests
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
    public void GpuVendors_FromHardwareId()
    {
        Assert.Equal(GpuVendor.Amd, GpuVendors.FromHardwareId("PCI\\VEN_1002&DEV_150E"));
        Assert.Equal(GpuVendor.Nvidia, GpuVendors.FromHardwareId("PCI\\VEN_10DE&DEV_2484"));
        Assert.Equal(GpuVendor.Intel, GpuVendors.FromHardwareId("PCI\\VEN_8086&DEV_7D55"));
        Assert.Null(GpuVendors.FromHardwareId("PCI\\VEN_14C3&DEV_7925"));
        Assert.Null(GpuVendors.FromHardwareId(null));
    }

    [Fact]
    public async Task GpuPage_OneItemPerGpuWithPage()
    {
        var ally = LoadAllyFixture();
        var source = new GpuPageSource(GpuVendor.Amd);

        Assert.True(source.AppliesTo(ally));
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(UpdateKind.Driver, item.Kind);
        Assert.Equal("32.0.32015.7029", item.InstalledVersion);
        Assert.Null(item.LatestVersion);
        Assert.Equal(InstallMethod.PageOnly, item.Method);
        Assert.NotNull(item.Page);
        Assert.Contains("amd.com", item.Page.ToString());
        Assert.Contains("AMD", item.Note);
    }

    [Fact]
    public async Task GpuPage_NotForAbsentVendor()
    {
        var ally = LoadAllyFixture();
        var source = new GpuPageSource(GpuVendor.Nvidia);

        Assert.False(source.AppliesTo(ally));
        var result = await source.CheckAsync(ally, CancellationToken.None);
        Assert.Equal(SourceStatus.NotForThisDevice, result.Status);
        Assert.Empty(result.Items);
    }
}
