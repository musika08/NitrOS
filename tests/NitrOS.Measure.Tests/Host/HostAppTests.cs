using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Journal;
using NitrOS.Measure.Campaign;
using NitrOS.Measure.Guest;
using NitrOS.Measure.Host;

namespace NitrOS.Measure.Tests.Host;

public class HostAppTests : IDisposable
{
    private readonly string _campaignDir;
    private readonly string _catalogDir;

    public HostAppTests()
    {
        _campaignDir = Path.Combine(Path.GetTempPath(), "nitros-host-campaign-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_campaignDir);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        _catalogDir = Path.Combine(dir!.FullName, "catalog");
    }

    public void Dispose()
    {
        if (Directory.Exists(_campaignDir))
        {
            try { Directory.Delete(_campaignDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Host_PlanThenNextThenCompleteFlow()
    {
        using var sw = new StringWriter();
        var env = new Dictionary<string, string?>();

        // Plan
        var planExit = HostApp.Run(
            ["plan", "--campaign", _campaignDir, "--catalog", _catalogDir, "--build", "26100.1742", "--cli-commit", "abc1234"],
            sw,
            env);
        Assert.Equal(0, planExit);

        // Next
        using var swNext = new StringWriter();
        var nextExit = HostApp.Run(["next", "--campaign", _campaignDir], swNext, env);
        Assert.Equal(0, nextExit);
        var spec = JsonSerializer.Deserialize<RunSpec>(swNext.ToString(), CatalogJson.Options);
        Assert.NotNull(spec);
        Assert.Equal("baseline-1", spec.RunId);

        // Complete
        var dummyResult = new RunResult(
            RunId: "baseline-1",
            WindowsBuild: "26100.1742",
            VisibleMb: 16000,
            Samples: [new MemorySample(DateTimeOffset.UtcNow, 2000, 100, 50)],
            Median: new RunSummary(2000, 100, 50),
            ApplyExitCode: 0,
            SwitchResults: new Dictionary<string, string>(),
            Slept: false,
            Started: DateTimeOffset.UtcNow.AddMinutes(-5),
            Finished: DateTimeOffset.UtcNow);

        var resFile = Path.Combine(_campaignDir, "b1.result.json");
        File.WriteAllText(resFile, CatalogJson.Write(dummyResult));

        using var swComp = new StringWriter();
        var compExit = HostApp.Run(["complete", "--campaign", _campaignDir, "--run", "baseline-1", "--result", resFile], swComp, env);
        Assert.Equal(0, compExit);
    }

    [Fact]
    public void Host_NextExit4WhenDone()
    {
        var dummyStore = new CampaignStore(_campaignDir);
        dummyStore.Create("c1", "26100.1742", "sha", Array.Empty<RunSpec>());

        using var sw = new StringWriter();
        var exit = HostApp.Run(["next", "--campaign", _campaignDir], sw, new Dictionary<string, string?>());
        Assert.Equal(4, exit);
    }

    [Fact]
    public void Host_SummarizeRunMapsJournalOutcomes()
    {
        var sampleRun = new SampleRun(
            Samples:
            [
                new MemorySample(DateTimeOffset.UtcNow, 2000, 100, 50),
                new MemorySample(DateTimeOffset.UtcNow, 2000, 100, 50),
                new MemorySample(DateTimeOffset.UtcNow, 2000, 100, 50),
                new MemorySample(DateTimeOffset.UtcNow, 2000, 100, 50),
                new MemorySample(DateTimeOffset.UtcNow, 2000, 100, 50),
            ],
            VisibleMb: 16000,
            WindowsBuild: "26100.1742",
            Slept: false,
            Started: DateTimeOffset.UtcNow.AddMinutes(-5),
            Finished: DateTimeOffset.UtcNow);

        var samplesFile = Path.Combine(_campaignDir, "samples.json");
        File.WriteAllText(samplesFile, CatalogJson.Write(sampleRun));

        var journal = new RunJournal(
            RunId: "test-run",
            UserSid: "S-1-5-21-1",
            WindowsBuild: "26100.1742",
            SwitchIds: ["switch-applied", "switch-skipped", "switch-failed"],
            Entries:
            [
                new JournalEntry("switch-applied", 0, new TaskDisable("t1"), new TaskPrior(true), ActionOutcome.Applied, null, DateTimeOffset.UtcNow),
                new JournalEntry("switch-skipped", 0, new TaskDisable("t2"), new TaskPrior(false), ActionOutcome.Skipped, null, DateTimeOffset.UtcNow),
                new JournalEntry("switch-failed", 0, new TaskDisable("t3"), new TaskPrior(true), ActionOutcome.Failed, "denied", DateTimeOffset.UtcNow),
            ]);

        var journalFile = Path.Combine(_campaignDir, "journal.json");
        File.WriteAllText(journalFile, CatalogJson.Write(journal));

        var outFile = Path.Combine(_campaignDir, "out.result.json");
        using var sw = new StringWriter();

        var exit = HostApp.Run(
            ["summarize-run", "--run", "test-run", "--samples", samplesFile, "--journal", journalFile, "--apply-exit", "0", "--out", outFile],
            sw,
            new Dictionary<string, string?>());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(outFile));

        var result = JsonSerializer.Deserialize<RunResult>(File.ReadAllText(outFile), CatalogJson.Options);
        Assert.NotNull(result);
        Assert.Equal("Applied", result.SwitchResults["switch-applied"]);
        Assert.Equal("Skipped", result.SwitchResults["switch-skipped"]);
        Assert.Equal("Failed", result.SwitchResults["switch-failed"]);
    }

    [Fact]
    public void Host_ResultsWithoutWriteLeavesCatalogUntouched()
    {
        // Copy catalog to temp dir
        var catCopy = Path.Combine(_campaignDir, "cat-copy");
        Directory.CreateDirectory(catCopy);
        foreach (var file in Directory.GetFiles(_catalogDir, "*.json"))
        {
            File.Copy(file, Path.Combine(catCopy, Path.GetFileName(file)));
        }

        var store = new CampaignStore(_campaignDir);
        var cat = NitrOS.Core.Loading.CatalogLoader.Load(catCopy).Catalog!;
        var plan = CampaignPlanner.Build(cat, DeviceProfile.Desktop);
        store.Create("c1", "26100.1742", "sha", plan);

        // Add 3 baseline runs
        for (int i = 1; i <= 3; i++)
        {
            var res = new RunResult($"baseline-{i}", "26100.1742", 16000,
                [new MemorySample(DateTimeOffset.UtcNow, 2000, 100, 50)],
                new RunSummary(2000, 100, 50), 0, new Dictionary<string, string>(), false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            store.Complete($"baseline-{i}", res);
        }

        var origCardsText = File.ReadAllText(Path.Combine(catCopy, "cards.json"));

        using var sw = new StringWriter();
        var exit = HostApp.Run(["results", "--campaign", _campaignDir, "--catalog", catCopy], sw, new Dictionary<string, string?>());

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_campaignDir, "report.md")));

        var afterCardsText = File.ReadAllText(Path.Combine(catCopy, "cards.json"));
        Assert.Equal(origCardsText, afterCardsText);
    }

    [Fact]
    public void Host_AnswerIsoRefusesWithoutPasswordVariable()
    {
        using var sw = new StringWriter();
        var exit = HostApp.Run(
            ["answer-iso", "--template", "dummy.xml", "--out", "dummy.iso", "--image-index", "1", "--user", "test"],
            sw,
            new Dictionary<string, string?>()); // No NITROS_MEASURE_PASSWORD

        Assert.Equal(1, exit);
        Assert.Contains("Refused", sw.ToString());
    }

    [Fact]
    public void Host_BadArgumentsExit2()
    {
        using var sw = new StringWriter();
        var exit = HostApp.Run(["invalid-command"], sw, new Dictionary<string, string?>());
        Assert.Equal(2, exit);
    }

    [Fact]
    public void Password_NeverInOutput()
    {
        var secretPassword = "SuperSecretPassword!9876";
        var env = new Dictionary<string, string?> { ["NITROS_MEASURE_PASSWORD"] = secretPassword };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        var templatePath = Path.Combine(dir!.FullName, "measure", "unattend", "autounattend.template.xml");
        var outIso = Path.Combine(_campaignDir, "out-password-test.iso");

        using var sw = new StringWriter();
        var exit = HostApp.Run(
            ["answer-iso", "--template", templatePath, "--out", outIso, "--image-index", "1", "--user", "nitros-test"],
            sw,
            env);

        Assert.Equal(0, exit);
        Assert.DoesNotContain(secretPassword, sw.ToString());
    }
}
