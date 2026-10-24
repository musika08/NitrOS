using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class GamingSettingsTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "gaming-settings");
    }

    private static SwitchDef Sw(string id) => Group().Switches.Single(s => s.Id == id);

    [Fact]
    public void GamingSettings_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "game-mode-on", "game-recording-background-off", "gpu-scheduling-on",
            "high-performance-plan", "cpu-boost-split", "energy-preference-split",
            "usb-power-saving-split", "pcie-power-saving-split", "mouse-acceleration-off",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void GamingSettings_SingleSentenceBreaksHaveOneItem()
    {
        Assert.Single(Sw("game-recording-background-off").Breaks);
        Assert.Single(Sw("gpu-scheduling-on").Breaks);
        Assert.Single(Sw("high-performance-plan").Breaks);
        Assert.Single(Sw("cpu-boost-split").Breaks);
        Assert.Single(Sw("energy-preference-split").Breaks);
        Assert.Single(Sw("usb-power-saving-split").Breaks);
        Assert.Single(Sw("mouse-acceleration-off").Breaks);
    }

    [Fact]
    public void GamingSettings_PowerPlanSkipsBattery()
    {
        Assert.Equal(new[] { DeviceTrait.Battery }, Sw("high-performance-plan").SkipOn);
    }

    [Fact]
    public void GamingSettings_HandheldSplitsHaveAcAndDc()
    {
        Assert.Equal(new SwitchAction[] { new PowerSet("54533251-82be-4824-96c1-47b60b740d00", "be337238-0d82-4146-a960-4f3749d470c7", 2, 1) }, Sw("cpu-boost-split").Live);
        Assert.Equal(new SwitchAction[] { new PowerScheme("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") }, Sw("high-performance-plan").Live);
        Assert.Empty(Sw("high-performance-plan").Image);
    }

    [Fact]
    public void GamingSettings_SpotCheck()
    {
        var sw = Sw("game-mode-on");
        var expected = new SwitchAction[] { new RegistrySet("HKCU", "Software\\Microsoft\\GameBar", "AutoGameModeEnabled", RegistryKind.Dword, "1") };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }
}
