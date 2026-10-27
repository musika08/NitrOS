using NitrOS.Measure.Campaign;

namespace NitrOS.Measure.Tests.Campaign;

public class CampaignStoreTests : IDisposable
{
    private readonly string _dir;

    public CampaignStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "nitros-campaign-test-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }

    private static RunResult CreateDummyResult(string runId)
    {
        var now = DateTimeOffset.UtcNow;
        var sample = new MemorySample(now, 2000, 100, 50);
        return new RunResult(
            RunId: runId,
            WindowsBuild: "26100.1742",
            VisibleMb: 16000,
            Samples: [sample, sample, sample, sample, sample],
            Median: new RunSummary(2000, 100, 50),
            ApplyExitCode: 0,
            SwitchResults: new Dictionary<string, string>(),
            Slept: false,
            Started: now.AddMinutes(-5),
            Finished: now);
    }

    [Fact]
    public void Create_RefusesExistingCampaign()
    {
        var store = new CampaignStore(_dir);
        var runs = new[] { new RunSpec("r1", RunKind.Baseline, [], null, "") };

        store.Create("c1", "26100.1", "abc", runs);
        Assert.Throws<InvalidOperationException>(() => store.Create("c1", "26100.1", "abc", runs));
    }

    [Fact]
    public void Next_ReturnsInterruptedRunAgain()
    {
        var store = new CampaignStore(_dir);
        var runs = new[]
        {
            new RunSpec("r1", RunKind.Baseline, [], null, ""),
            new RunSpec("r2", RunKind.Baseline, [], null, ""),
        };
        store.Create("c1", "26100.1", "abc", runs);

        var next1 = store.Next();
        Assert.NotNull(next1);
        Assert.Equal("r1", next1.RunId);

        // State now shows r1 is Running, Attempts = 1
        var state1 = store.Load();
        var r1State = state1.States.Single(s => s.RunId == "r1");
        Assert.Equal(RunStatus.Running, r1State.Status);
        Assert.Equal(1, r1State.Attempts);

        // Simulate crash / reopen store and call Next()
        var store2 = new CampaignStore(_dir);
        var next2 = store2.Next();
        Assert.NotNull(next2);
        Assert.Equal("r1", next2.RunId);

        var state2 = store2.Load();
        var r1State2 = state2.States.Single(s => s.RunId == "r1");
        Assert.Equal(RunStatus.Running, r1State2.Status);
        Assert.Equal(2, r1State2.Attempts);
    }

    [Fact]
    public void Next_SkipsDoneAndMarksThirdFailureFailed()
    {
        var store = new CampaignStore(_dir);
        var runs = new[]
        {
            new RunSpec("r1", RunKind.Baseline, [], null, ""),
            new RunSpec("r2", RunKind.Baseline, [], null, ""),
        };
        store.Create("c1", "26100.1", "abc", runs);

        // Run 1: attempt 1
        var n1 = store.Next();
        Assert.Equal("r1", n1!.RunId);

        // Attempt 2
        var n2 = store.Next();
        Assert.Equal("r1", n2!.RunId);

        // Attempt 3
        var n3 = store.Next();
        Assert.Equal("r1", n3!.RunId);

        // 4th call to Next(): r1 has 3 attempts and is still Running, so it becomes Failed, and r2 is returned
        var n4 = store.Next();
        Assert.NotNull(n4);
        Assert.Equal("r2", n4.RunId);

        var state = store.Load();
        var r1State = state.States.Single(s => s.RunId == "r1");
        Assert.Equal(RunStatus.Failed, r1State.Status);

        // Complete r2
        store.Complete("r2", CreateDummyResult("r2"));
        var n5 = store.Next();
        Assert.Null(n5); // Nothing left
    }

    [Fact]
    public void Complete_IsIdempotent()
    {
        var store = new CampaignStore(_dir);
        var runs = new[] { new RunSpec("r1", RunKind.Baseline, [], null, "") };
        store.Create("c1", "26100.1", "abc", runs);

        var run = store.Next();
        Assert.NotNull(run);

        var res = CreateDummyResult("r1");
        store.Complete("r1", res);
        store.Complete("r1", res); // idempotent second call

        var state = store.Load();
        Assert.Equal(RunStatus.Done, state.States.Single(s => s.RunId == "r1").Status);
    }

    [Fact]
    public void Complete_WritesResultBeforeState()
    {
        var store = new CampaignStore(_dir);
        var runs = new[] { new RunSpec("r1", RunKind.Baseline, [], null, "") };
        store.Create("c1", "26100.1", "abc", runs);

        store.Next();

        var res = CreateDummyResult("r1");
        store.Complete("r1", res);

        var resultFilePath = Path.Combine(_dir, "runs", "r1.result.json");
        Assert.True(File.Exists(resultFilePath));
    }

    [Fact]
    public void Store_IgnoresLeftoverTmpFile()
    {
        var store = new CampaignStore(_dir);
        var runs = new[] { new RunSpec("r1", RunKind.Baseline, [], null, "") };
        store.Create("c1", "26100.1", "abc", runs);

        // Simulate leftover tmp file
        File.WriteAllText(Path.Combine(_dir, "campaign.json.tmp"), "corrupt leftover");

        var state = store.Load();
        Assert.Equal("c1", state.CampaignId);
    }
}
