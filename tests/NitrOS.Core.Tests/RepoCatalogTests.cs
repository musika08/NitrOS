using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests;

public class RepoCatalogTests
{
    [Fact]
    public void RepoCatalog_LoadsWithoutErrors()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
    }

    [Fact]
    public void RepoCatalog_RecommendedTurnsOnStarterSwitches()
    {
        var catalog = CatalogLoader.Load(RepoPaths.CatalogDir()).Catalog!;
        var on = Selection.FromCard(catalog, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop).On;
        foreach (var id in new[] { "advertising-id-off", "consumer-features-off", "diagtrack-off" })
            Assert.Contains(id, on);
    }
}
