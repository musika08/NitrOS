using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class SecurityUpdatesTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "security-updates");
    }

    private static SwitchDef Sw(string id) => Group().Switches.Single(s => s.Id == id);

    [Fact]
    public void SecurityUpdates_HasExactlyTheVerifiedIds()
    {
        var expected = new[] { "defender-remove", "windows-update-auto-off", "memory-integrity-off" };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void SecurityUpdates_MultiSentenceBreaksOnePerSentence()
    {
        Assert.Equal(2, Sw("defender-remove").Breaks.Count);
        Assert.Equal(2, Sw("windows-update-auto-off").Breaks.Count);
        Assert.Equal(2, Sw("memory-integrity-off").Breaks.Count);
    }

    [Fact]
    public void SecurityUpdates_WindowsUpdateAutoOffLeavesTheServiceAvailable()
    {
        var sw = Sw("windows-update-auto-off");
        Assert.DoesNotContain(sw.Live, a => a is ServiceStart);
        Assert.DoesNotContain(sw.Image, a => a is ServiceStart);
    }

    [Fact]
    public void SecurityUpdates_MaxLeanOnly()
    {
        foreach (var id in new[] { "defender-remove", "windows-update-auto-off", "memory-integrity-off" })
        {
            var c = Sw(id).Cards;
            Assert.Equal(CardDefault.Off, c.Recommended);
            Assert.Equal(CardDefault.Off, c.Gamer);
            Assert.Equal(CardDefault.Off, c.Streamer);
            Assert.Equal(CardDefault.On, c.MaxLean);
        }
    }

    [Fact]
    public void SecurityUpdates_SpotCheck()
    {
        var sw = Sw("defender-remove");
        var expected = new SwitchAction[]
        {
            new ServiceStart("WinDefend", ServiceStartType.Disabled),
            new ServiceStart("WdNisSvc", ServiceStartType.Disabled),
            new ServiceStart("WdNisDrv", ServiceStartType.Disabled),
            new ServiceStart("WdFilter", ServiceStartType.Disabled),
            new ServiceStart("Sense", ServiceStartType.Disabled),
            new RegistrySet("HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", RegistryKind.Dword, "1"),
            new RegistrySet("HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", RegistryKind.Dword, "1"),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }
}
