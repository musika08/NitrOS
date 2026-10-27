using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Measure.Campaign;

namespace NitrOS.Measure.Tests.Campaign;

public class CampaignPlannerTests
{
    private static Catalog LoadRealCatalog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException("global.json not found above " + AppContext.BaseDirectory);
        }

        var catalogDir = Path.Combine(dir.FullName, "catalog");
        return CatalogLoader.Load(catalogDir).Catalog!;
    }

    [Fact]
    public void Plan_HasBaselinesSwitchesCardsVariantsAndFinalBaselineInOrder()
    {
        var catalog = LoadRealCatalog();
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);

        Assert.Equal(119, plan.Count);

        Assert.Equal("baseline-1", plan[0].RunId);
        Assert.Equal(RunKind.Baseline, plan[0].Kind);
        Assert.Empty(plan[0].Ids);

        Assert.Equal("baseline-2", plan[1].RunId);
        Assert.Equal("baseline-3", plan[2].RunId);

        // Switches
        Assert.StartsWith("switch-", plan[3].RunId);
        Assert.Equal(RunKind.Switch, plan[3].Kind);

        // Check cards towards the end
        var cardRuns = plan.Where(r => r.Kind == RunKind.Card).ToList();
        Assert.Equal(4, cardRuns.Count);
        Assert.Equal("card-recommended", cardRuns[0].RunId);
        Assert.Equal("card-gamer", cardRuns[1].RunId);
        Assert.Equal("card-streamer", cardRuns[2].RunId);
        Assert.Equal("card-max-lean", cardRuns[3].RunId);

        // Variants
        var variantRuns = plan.Where(r => r.Kind == RunKind.Variant).ToList();
        Assert.Equal(2, variantRuns.Count);
        Assert.Equal("variant-gamer-svchost", variantRuns[0].RunId);
        Assert.Equal("variant-streamer-svchost", variantRuns[1].RunId);

        // Last is baseline-final
        Assert.Equal("baseline-final", plan[^1].RunId);
        Assert.Equal(RunKind.Baseline, plan[^1].Kind);
        Assert.Empty(plan[^1].Ids);
    }

    [Fact]
    public void Plan_LeavesOutImageOnlySwitches()
    {
        var catalog = LoadRealCatalog();
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);

        Assert.DoesNotContain(plan, r => r.RunId == "switch-recovery-remove");
        Assert.DoesNotContain(plan, r => r.Ids.Contains("recovery-remove"));
    }

    [Fact]
    public void Plan_CardIdsMatchFromCard()
    {
        var catalog = LoadRealCatalog();
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);

        var gamerRun = plan.Single(r => r.RunId == "card-gamer");
        var expectedGamer = Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, DeviceProfile.Desktop).On
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expectedGamer, gamerRun.Ids);
        Assert.DoesNotContain("svchost-grouping-on", gamerRun.Ids);
    }

    [Fact]
    public void Plan_VariantsAddSvchostToTheCard()
    {
        var catalog = LoadRealCatalog();
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);

        var gamerRun = plan.Single(r => r.RunId == "card-gamer");
        var gamerVariant = plan.Single(r => r.RunId == "variant-gamer-svchost");

        var expectedVariantIds = gamerRun.Ids.Concat(["svchost-grouping-on"])
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expectedVariantIds, gamerVariant.Ids);
        Assert.Equal("gamer", gamerVariant.Card);
    }

    [Fact]
    public void Hash_IgnoresMeasuredButNotActions()
    {
        var switch1 = new SwitchDef
        {
            Id = "test-switch",
            Name = "Test",
            Feature = "Feature",
            Description = "Desc",
            Risk = Risk.Safe,
            Modes = [Mode.Live],
            Reversible = true,
            Cards = new CardDefaults
            {
                Recommended = CardDefault.Off,
                Gamer = CardDefault.On,
                Streamer = CardDefault.Off,
                MaxLean = CardDefault.On,
            },
            Live = [new RegistrySet("HKLM", "Test", "Val", RegistryKind.Dword, "1")],
            Measured = null,
        };

        var switchWithMeasured = switch1 with
        {
            Measured = new Measurement(50, 2, "26100.1", new DateOnly(2026, 10, 28))
        };

        var switchDifferentAction = switch1 with
        {
            Live = [new RegistrySet("HKLM", "Test", "Val", RegistryKind.Dword, "2")]
        };

        var hash1 = DefinitionHash.Of(switch1);
        var hashWithMeasured = DefinitionHash.Of(switchWithMeasured);
        var hashDiffAction = DefinitionHash.Of(switchDifferentAction);

        Assert.Equal(hash1, hashWithMeasured);
        Assert.NotEqual(hash1, hashDiffAction);
    }
}
