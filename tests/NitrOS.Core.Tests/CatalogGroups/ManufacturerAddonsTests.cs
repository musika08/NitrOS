using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class ManufacturerAddonsTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "manufacturer-addons");
    }

    [Fact]
    public void ManufacturerAddons_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "armoury-crate-remove", "myasus-remove", "dolby-access-remove",
            "realtek-audio-console-remove", "amd-panel-background-off",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void ManufacturerAddons_SpotCheck()
    {
        var sw = Group().Switches.Single(s => s.Id == "armoury-crate-remove");
        var expected = new SwitchAction[]
        {
            new AppxRemove("B9ECED6F.ArmouryCrateSE"),
            new AppxRemove("B9ECED6F.ArmouryCrate"),
            new AppxRemove("B9ECED6F.ASUSCommandCenter"),
            new AppxRemove("ASUSAmbientHAL64"),
            new ProgramUninstall("Armoury Crate SE Service", "ASUS"),
            new ProgramUninstall("ASUS Framework Service", "ASUSTeK Computer Inc."),
            new ProgramUninstall("ROG Live Service", "ASUSTek COMPUTER INC."),
            new ProgramUninstall("ASUS Ambient HAL", "ASUSTeK COMPUTER INC."),
            new ProgramUninstall("ROG Screen Manager", "ASUSTek COMPUTER INC."),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Empty(sw.Image);
    }
}
