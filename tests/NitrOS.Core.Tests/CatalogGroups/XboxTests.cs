using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class XboxTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "xbox");
    }

    private static SwitchDef Sw(string id) => Group().Switches.Single(s => s.Id == id);

    [Fact]
    public void Xbox_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "xbox-app-remove", "game-bar-remove", "xbox-sign-in-remove",
            "gaming-services-remove", "xbox-services-off",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Xbox_MultiSentenceBreaksOnePerSentence()
    {
        Assert.Equal(2, Sw("xbox-app-remove").Breaks.Count);
        Assert.Equal(2, Sw("game-bar-remove").Breaks.Count);
    }

    [Fact]
    public void Xbox_AppRemoveRequiresStoreGamingServicesAndSignIn()
    {
        Assert.Equal(
            new[] { "store-remove", "gaming-services-remove", "xbox-sign-in-remove", "xbox-services-off" },
            Sw("xbox-app-remove").Requires);
    }

    [Fact]
    public void Xbox_SpotCheck()
    {
        var sw = Sw("xbox-app-remove");
        var expected = new SwitchAction[] { new AppxRemove("Microsoft.GamingApp") };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
        Assert.Contains("gaming-services-remove", sw.Requires);
        Assert.Contains("xbox-sign-in-remove", sw.Requires);
    }
}
