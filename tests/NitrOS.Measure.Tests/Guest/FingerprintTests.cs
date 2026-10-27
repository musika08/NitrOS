using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Measure.Guest;

namespace NitrOS.Measure.Tests.Guest;

public class FingerprintTests
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
    public void Fingerprint_CapturesEveryLiveAction()
    {
        var catalog = LoadRealCatalog();
        var fakeSystems = new FakeSystems();

        var fingerprint = FingerprintTool.Capture(catalog, fakeSystems.ToLive());

        var expectedTotalLiveActions = catalog.Switches.Sum(s => s.Live.Count);
        Assert.Equal(expectedTotalLiveActions, fingerprint.Entries.Count);

        foreach (var s in catalog.Switches)
        {
            for (int i = 0; i < s.Live.Count; i++)
            {
                var key = $"{s.Id}#{i}";
                Assert.True(fingerprint.Entries.ContainsKey(key), $"Missing entry for {key}");
            }
        }
    }

    [Fact]
    public void Compare_ReportsOnlyReversibleDifferences()
    {
        var switchReversible = new SwitchDef
        {
            Id = "rev-switch",
            Name = "Reversible",
            Feature = "F",
            Description = "D",
            Risk = Risk.Safe,
            Modes = [Mode.Live],
            Reversible = true,
            Cards = new CardDefaults { Recommended = CardDefault.On, Gamer = CardDefault.On, Streamer = CardDefault.On, MaxLean = CardDefault.On },
            Live = [new RegistrySet("HKCU", "Software\\Test", "Val", RegistryKind.Dword, "1")]
        };

        var switchIrreversible = new SwitchDef
        {
            Id = "irrev-switch",
            Name = "Irreversible",
            Feature = "F",
            Description = "D",
            Risk = Risk.Safe,
            Modes = [Mode.Live],
            Reversible = false,
            Cards = new CardDefaults { Recommended = CardDefault.On, Gamer = CardDefault.On, Streamer = CardDefault.On, MaxLean = CardDefault.On },
            Live = [new AppxRemove("Microsoft.App")]
        };

        var catalog = new Catalog([new GroupFile("test", "Test", [switchReversible, switchIrreversible])], new CardMeasurements());

        var before = new Fingerprint(new Dictionary<string, string>
        {
            ["rev-switch#0"] = "{\"value\":\"0\"}",
            ["irrev-switch#0"] = "{\"installed\":true}",
        });

        var after = new Fingerprint(new Dictionary<string, string>
        {
            ["rev-switch#0"] = "{\"value\":\"1\"}", // Changed registry value -> unexpected diff
            ["irrev-switch#0"] = "{\"installed\":false}", // Removed app on irreversible switch -> expected diff
        });

        var diffs = FingerprintTool.Compare(before, after, catalog);

        Assert.Equal(2, diffs.Count);

        var revDiff = diffs.Single(d => d.Key == "rev-switch#0");
        Assert.False(revDiff.Expected);

        var irrevDiff = diffs.Single(d => d.Key == "irrev-switch#0");
        Assert.True(irrevDiff.Expected);
    }
}
