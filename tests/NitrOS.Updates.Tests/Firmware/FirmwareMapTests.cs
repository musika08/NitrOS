using System.Text.Json;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;
using Xunit;

namespace NitrOS.Updates.Tests.Firmware;

public sealed class FirmwareMapTests
{
    private static DeviceInfo LoadAllyFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ally-x-device-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.Deserialize<DeviceInfo>(json, options)!;
    }

    [Fact]
    public void FirmwareMap_BiosRuleHoldsOnAllyFixture()
    {
        var map = FirmwareMap.Shipped();
        var ally = LoadAllyFixture();

        var link = map.ForListing(ally, "BIOS Update for Windows");
        Assert.NotNull(link);
        Assert.Equal(CompareRule.EsrtHex, link.Rule);
        Assert.True(FirmwareMap.RuleHolds(ally, link));
        Assert.Equal("319", FirmwareMap.InstalledText(link, ally));
    }

    [Fact]
    public void FirmwareMap_PdRuleHolds()
    {
        var map = FirmwareMap.Shipped();
        var ally = LoadAllyFixture();

        var link = map.ForListing(ally, "PD_FW Firmware");
        Assert.NotNull(link);
        Assert.Equal(CompareRule.EsrtHex, link.Rule);
        Assert.True(FirmwareMap.RuleHolds(ally, link));
        Assert.Equal("0015", FirmwareMap.InstalledText(link, ally));
    }

    [Fact]
    public void FirmwareMap_TouchUsesPackageLastField()
    {
        var map = FirmwareMap.Shipped();
        var ally = LoadAllyFixture();

        var link = map.ForListing(ally, "Touch_Panel_FW Firmware");
        Assert.NotNull(link);
        Assert.Equal(CompareRule.PackageLastField, link.Rule);
        Assert.True(FirmwareMap.RuleHolds(ally, link));
        Assert.Equal("2002", FirmwareMap.InstalledText(link, ally));
    }

    [Fact]
    public void FirmwareMap_RuleFailsWhenBiosAndEsrtDisagree()
    {
        var map = FirmwareMap.Shipped();
        var ally = LoadAllyFixture();

        // Mutate ESRT version to 791 (does not match 319 = 0x319 = 793)
        var mutatedFirmware = ally.Firmware.Select(f => f.Type == 1
            ? new FirmwareResource(f.Id, f.Type, 791, f.LastAttemptVersion, f.LastAttemptStatus, f.PackageVersion, f.Name)
            : f).ToList();
        var mutatedAlly = ally with { Firmware = mutatedFirmware };

        var link = map.ForListing(mutatedAlly, "BIOS Update for Windows");
        Assert.NotNull(link);
        Assert.False(FirmwareMap.RuleHolds(mutatedAlly, link));
        Assert.Null(FirmwareMap.InstalledText(link, mutatedAlly));
    }
}
