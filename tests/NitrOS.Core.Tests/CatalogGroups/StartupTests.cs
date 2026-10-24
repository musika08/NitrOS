using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class StartupTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "startup");
    }

    [Fact]
    public void Startup_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "background-apps-off", "edge-startup-boost-off", "edge-auto-launch-off",
            "onedrive-setup-startup-off", "steam-big-picture-at-boot", "playnite-fullscreen-at-boot",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Startup_SpotCheck()
    {
        var sw = Group().Switches.Single(s => s.Id == "background-apps-off");
        var expected = new SwitchAction[]
        {
            new RegistrySet("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                "LetAppsRunInBackground", RegistryKind.Dword, "2"),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }
}
