using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Core.Model;

namespace NitrOS.Measure.Tests;

public class SavingsTests
{
    private static RunResult CreateRun(
        string id = "run-1",
        string build = "26100.1",
        int inUse = 2000,
        int proc = 100,
        int svc = 50,
        bool slept = false)
    {
        var sample = new MemorySample(DateTimeOffset.UtcNow, inUse, proc, svc);
        return new RunResult(
            RunId: id,
            WindowsBuild: build,
            VisibleMb: 16000,
            Samples: [sample, sample, sample, sample, sample],
            Median: new RunSummary(inUse, proc, svc),
            ApplyExitCode: 0,
            SwitchResults: new Dictionary<string, string>(),
            Slept: slept,
            Started: DateTimeOffset.UtcNow.AddMinutes(-5),
            Finished: DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Baseline_IsMedianOfThreeWithSpreadAsNoise()
    {
        var runs = new[]
        {
            CreateRun("b1", inUse: 2000, proc: 100, svc: 50),
            CreateRun("b2", inUse: 2010, proc: 105, svc: 52),
            CreateRun("b3", inUse: 2030, proc: 110, svc: 54),
        };

        var baseline = BaselineCalc.From(runs);

        Assert.Equal(2010, baseline.Value.InUseMb);
        Assert.Equal(105, baseline.Value.Processes);
        Assert.Equal(52, baseline.Value.Svchost);
        Assert.Equal(30, baseline.MemoryNoiseMb); // 2030 - 2000
        Assert.Equal(10, baseline.ProcessNoise);   // 110 - 100
    }

    [Fact]
    public void Baseline_RefusesFewerThanThreeMixedBuildsOrSleptRuns()
    {
        var twoRuns = new[] { CreateRun("b1"), CreateRun("b2") };
        Assert.Throws<InvalidOperationException>(() => BaselineCalc.From(twoRuns));

        var mixedBuilds = new[]
        {
            CreateRun("b1", build: "26100.1"),
            CreateRun("b2", build: "26100.2"),
            CreateRun("b3", build: "26100.1"),
        };
        Assert.Throws<InvalidOperationException>(() => BaselineCalc.From(mixedBuilds));

        var slept = new[]
        {
            CreateRun("b1", slept: false),
            CreateRun("b2", slept: true),
            CreateRun("b3", slept: false),
        };
        Assert.Throws<InvalidOperationException>(() => BaselineCalc.From(slept));
    }

    [Fact]
    public void Savings_IsBaselineMinusRun()
    {
        var baseline = new Baseline(new RunSummary(2010, 100, 50), MemoryNoiseMb: 30, ProcessNoise: 5);
        var run = CreateRun(inUse: 1890, proc: 90);
        var date = new DateOnly(2026, 10, 28);

        var m = Savings.For(run, baseline, date);

        Assert.Equal(120, m.MemoryMb);
        Assert.Equal(10, m.Processes);
        Assert.False(Savings.IsFlagged(run, baseline));
    }

    [Fact]
    public void Savings_InsideNoiseBandIsZero()
    {
        var baseline = new Baseline(new RunSummary(2000, 100, 50), MemoryNoiseMb: 30, ProcessNoise: 10);
        var date = new DateOnly(2026, 10, 28);

        // difference +25 (inUse: 1975) -> inside noise 30 -> 0
        var runPositiveInside = CreateRun(inUse: 1975, proc: 95);
        var mPos = Savings.For(runPositiveInside, baseline, date);
        Assert.Equal(0, mPos.MemoryMb);
        Assert.Equal(0, mPos.Processes);
        Assert.False(Savings.IsFlagged(runPositiveInside, baseline));

        // difference -25 (inUse: 2025) -> inside noise 30 -> 0
        var runNegativeInside = CreateRun(inUse: 2025, proc: 105);
        var mNeg = Savings.For(runNegativeInside, baseline, date);
        Assert.Equal(0, mNeg.MemoryMb);
        Assert.Equal(0, mNeg.Processes);
        Assert.False(Savings.IsFlagged(runNegativeInside, baseline));
    }

    [Fact]
    public void Savings_NegativeOutsideBandIsKeptAndFlagged()
    {
        var baseline = new Baseline(new RunSummary(2000, 100, 50), MemoryNoiseMb: 30, ProcessNoise: 10);
        var date = new DateOnly(2026, 10, 28);

        // Memory increased by 50 (inUse: 2050 -> diff = -50, outside noise 30)
        var run = CreateRun(inUse: 2050, proc: 100);
        var m = Savings.For(run, baseline, date);

        Assert.Equal(-50, m.MemoryMb);
        Assert.True(Savings.IsFlagged(run, baseline));
    }

    [Fact]
    public void Savings_CarriesBuildAndDate()
    {
        var baseline = new Baseline(new RunSummary(2000, 100, 50), MemoryNoiseMb: 30, ProcessNoise: 10);
        var run = CreateRun(build: "26100.1742");
        var date = new DateOnly(2026, 10, 28);

        var m = Savings.For(run, baseline, date);

        Assert.Equal("26100.1742", m.WindowsBuild);
        Assert.Equal(date, m.Date);
    }

    [Fact]
    public void RunResult_RoundTripsThroughCatalogJson()
    {
        var now = DateTimeOffset.UtcNow;
        var sample = new MemorySample(now, 2048, 120, 60);
        var orig = new RunResult(
            RunId: "switch-edge-remove",
            WindowsBuild: "26100.1742",
            VisibleMb: 16384,
            Samples: [sample],
            Median: new RunSummary(2048, 120, 60),
            ApplyExitCode: 0,
            SwitchResults: new Dictionary<string, string> { ["edge-remove"] = "Applied" },
            Slept: false,
            Started: now.AddMinutes(-10),
            Finished: now);

        var json = CatalogJson.Write(orig);
        var deserialized = JsonSerializer.Deserialize<RunResult>(json, CatalogJson.Options);

        Assert.NotNull(deserialized);
        Assert.Equal(orig.RunId, deserialized.RunId);
        Assert.Equal(orig.WindowsBuild, deserialized.WindowsBuild);
        Assert.Equal(orig.VisibleMb, deserialized.VisibleMb);
        Assert.Equal(orig.Median, deserialized.Median);
        Assert.Equal(orig.ApplyExitCode, deserialized.ApplyExitCode);
        Assert.Equal(orig.SwitchResults["edge-remove"], deserialized.SwitchResults["edge-remove"]);
        Assert.Equal(orig.Slept, deserialized.Slept);
        Assert.Equal(orig.Started, deserialized.Started);
        Assert.Equal(orig.Finished, deserialized.Finished);
    }
}
