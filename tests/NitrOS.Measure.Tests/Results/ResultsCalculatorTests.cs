using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Measure.Campaign;
using NitrOS.Measure.Results;

namespace NitrOS.Measure.Tests.Results;

public class ResultsCalculatorTests
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

    private static RunResult CreateRunResult(
        string runId,
        string build = "26100.1742",
        int inUse = 2000,
        int proc = 100,
        int svc = 50,
        bool slept = false,
        string switchStatus = "Applied")
    {
        var sample = new MemorySample(DateTimeOffset.UtcNow, inUse, proc, svc);
        var switchId = runId.StartsWith("switch-") ? runId["switch-".Length..] : runId;
        return new RunResult(
            RunId: runId,
            WindowsBuild: build,
            VisibleMb: 16000,
            Samples: [sample, sample, sample, sample, sample],
            Median: new RunSummary(inUse, proc, svc),
            ApplyExitCode: 0,
            SwitchResults: new Dictionary<string, string> { [switchId] = switchStatus },
            Slept: slept,
            Started: DateTimeOffset.UtcNow.AddMinutes(-5),
            Finished: DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Compute_UsesMedianBaselineAndNoise()
    {
        var catalog = LoadRealCatalog();
        var date = new DateOnly(2026, 10, 28);
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);
        var state = new CampaignState("c1", "26100.1742", "sha", DateTimeOffset.UtcNow, plan, []);

        var results = new Dictionary<string, RunResult>
        {
            ["baseline-1"] = CreateRunResult("baseline-1", inUse: 2000, proc: 100, svc: 50),
            ["baseline-2"] = CreateRunResult("baseline-2", inUse: 2010, proc: 105, svc: 52),
            ["baseline-3"] = CreateRunResult("baseline-3", inUse: 2030, proc: 110, svc: 54),
            ["switch-fax-off"] = CreateRunResult("switch-fax-off", inUse: 1890, proc: 95, svc: 48),
        };

        var outcome = ResultsCalculator.Compute(state, results, catalog, date, new SvchostChoice(false, false));

        Assert.Equal(2010, outcome.Baseline.Value.InUseMb);
        Assert.Equal(30, outcome.Baseline.MemoryNoiseMb);
        Assert.True(outcome.Switches.ContainsKey("fax-off"));
        Assert.Equal(120, outcome.Switches["fax-off"].MemoryMb);
    }

    [Fact]
    public void Compute_NoNumberForSkippedFailedOrChanged()
    {
        var catalog = LoadRealCatalog();
        var date = new DateOnly(2026, 10, 28);
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);
        var state = new CampaignState("c1", "26100.1742", "sha", DateTimeOffset.UtcNow, plan, []);

        var results = new Dictionary<string, RunResult>
        {
            ["baseline-1"] = CreateRunResult("baseline-1"),
            ["baseline-2"] = CreateRunResult("baseline-2"),
            ["baseline-3"] = CreateRunResult("baseline-3"),
            ["switch-fax-off"] = CreateRunResult("switch-fax-off", switchStatus: "Skipped"),
            ["switch-maps-updates-off"] = CreateRunResult("switch-maps-updates-off", switchStatus: "Failed"),
            ["switch-remote-registry-off"] = CreateRunResult("switch-remote-registry-off", slept: true),
        };

        var outcome = ResultsCalculator.Compute(state, results, catalog, date, new SvchostChoice(false, false));

        Assert.False(outcome.Switches.ContainsKey("fax-off"));
        Assert.Contains(outcome.NotMeasured, nm => nm.Id == "fax-off" && nm.Reason.Contains("not present in the test machine"));

        Assert.False(outcome.Switches.ContainsKey("maps-updates-off"));
        Assert.Contains(outcome.NotMeasured, nm => nm.Id == "maps-updates-off" && nm.Reason.Contains("failed in the test machine"));

        Assert.False(outcome.Switches.ContainsKey("remote-registry-off"));
        Assert.Contains(outcome.NotMeasured, nm => nm.Id == "remote-registry-off" && nm.Reason.Contains("guest slept"));
    }

    [Fact]
    public void Compute_ThrowsOnMixedBuilds()
    {
        var catalog = LoadRealCatalog();
        var date = new DateOnly(2026, 10, 28);
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);
        var state = new CampaignState("c1", "26100.1742", "sha", DateTimeOffset.UtcNow, plan, []);

        var results = new Dictionary<string, RunResult>
        {
            ["baseline-1"] = CreateRunResult("baseline-1", build: "26100.1742"),
            ["baseline-2"] = CreateRunResult("baseline-2", build: "26100.1742"),
            ["baseline-3"] = CreateRunResult("baseline-3", build: "26100.9999"), // different build
        };

        Assert.Throws<InvalidOperationException>(() =>
            ResultsCalculator.Compute(state, results, catalog, date, new SvchostChoice(false, false)));
    }

    [Fact]
    public void Compute_CardsUseVariantWhenSvchostChosen()
    {
        var catalog = LoadRealCatalog();
        var date = new DateOnly(2026, 10, 28);
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);
        var state = new CampaignState("c1", "26100.1742", "sha", DateTimeOffset.UtcNow, plan, []);

        var results = new Dictionary<string, RunResult>
        {
            ["baseline-1"] = CreateRunResult("baseline-1", inUse: 2000),
            ["baseline-2"] = CreateRunResult("baseline-2", inUse: 2000),
            ["baseline-3"] = CreateRunResult("baseline-3", inUse: 2000),
            ["card-gamer"] = CreateRunResult("card-gamer", inUse: 1800),
            ["variant-gamer-svchost"] = CreateRunResult("variant-gamer-svchost", inUse: 1700),
        };

        // When svchost.Gamer is false -> uses card-gamer (saving = 200)
        var outcome1 = ResultsCalculator.Compute(state, results, catalog, date, new SvchostChoice(Gamer: false, Streamer: false));
        Assert.NotNull(outcome1.Cards.Gamer);
        Assert.Equal(200, outcome1.Cards.Gamer.MemoryMb);

        // When svchost.Gamer is true -> uses variant-gamer-svchost (saving = 300)
        var outcome2 = ResultsCalculator.Compute(state, results, catalog, date, new SvchostChoice(Gamer: true, Streamer: false));
        Assert.NotNull(outcome2.Cards.Gamer);
        Assert.Equal(300, outcome2.Cards.Gamer.MemoryMb);
    }

    [Fact]
    public void Compute_FlagsFinalBaselineDrift()
    {
        var catalog = LoadRealCatalog();
        var date = new DateOnly(2026, 10, 28);
        var plan = CampaignPlanner.Build(catalog, DeviceProfile.Desktop);
        var state = new CampaignState("c1", "26100.1742", "sha", DateTimeOffset.UtcNow, plan, []);

        var results = new Dictionary<string, RunResult>
        {
            ["baseline-1"] = CreateRunResult("baseline-1", inUse: 2000),
            ["baseline-2"] = CreateRunResult("baseline-2", inUse: 2010),
            ["baseline-3"] = CreateRunResult("baseline-3", inUse: 2020), // noise = 20, median = 2010
            ["baseline-final"] = CreateRunResult("baseline-final", inUse: 2060), // drift = 2060 - 2010 = 50 > 2 * 20
        };

        var outcome = ResultsCalculator.Compute(state, results, catalog, date, new SvchostChoice(false, false));
        Assert.Equal(50, outcome.FinalBaselineDriftMb);
    }
}
