using NitrOS.Measure.Guest;

namespace NitrOS.Measure.Tests.Guest;

public class SamplerTests
{
    private sealed class FakeGuestFacts : IGuestFacts
    {
        public long TotalPhys { get; set; } = 16L * 1024 * 1024 * 1024; // 16 GB
        public long AvailPhys { get; set; } = 14L * 1024 * 1024 * 1024; // 14 GB -> 2 GB in-use = 2048 MB
        public List<(int Pid, string Name)> Procs { get; set; } = [];
        public TimeSpan Unbiased { get; set; } = TimeSpan.FromHours(1);
        public DateTimeOffset CurrentTime { get; set; } = DateTimeOffset.UtcNow;
        public string Build { get; set; } = "26100.1742";
        public int OwnPid { get; set; } = 1234;

        public (long TotalPhys, long AvailPhys) Memory() => (TotalPhys, AvailPhys);
        public IReadOnlyList<(int Pid, string Name)> Processes() => Procs;
        public TimeSpan UnbiasedTime() => Unbiased;
        public DateTimeOffset Now() => CurrentTime;
        public string WindowsBuild() => Build;
    }

    [Fact]
    public void Sampler_TakesCountSamplesAfterIdle()
    {
        var facts = new FakeGuestFacts();
        var sleeps = new List<TimeSpan>();
        Action<TimeSpan> sleep = span =>
        {
            sleeps.Add(span);
            facts.CurrentTime += span;
            facts.Unbiased += span;
        };

        var sampler = new Sampler(facts, sleep);
        var options = new SamplerOptions(TimeSpan.FromMinutes(10), 5, TimeSpan.FromSeconds(30));

        var run = sampler.Run(options);

        Assert.Equal(5, run.Samples.Count);
        Assert.Equal(5, sleeps.Count);
        Assert.Equal(TimeSpan.FromMinutes(10), sleeps[0]);
        for (int i = 1; i < 5; i++)
        {
            Assert.Equal(TimeSpan.FromSeconds(30), sleeps[i]);
        }
    }

    [Fact]
    public void Sampler_ExcludesOwnProcessAndIdle()
    {
        var facts = new FakeGuestFacts
        {
            OwnPid = 999,
            Procs =
            [
                (0, "Idle"),
                (999, "nitros-probe"),
                (4, "System"),
                (100, "svchost"),
            ]
        };

        var sampler = new Sampler(facts, _ => { });
        var run = sampler.Run(new SamplerOptions(TimeSpan.Zero, 1, TimeSpan.Zero));

        var sample = run.Samples[0];
        Assert.Equal(2, sample.Processes); // System and svchost (Idle and own PID excluded)
    }

    [Fact]
    public void Sampler_CountsSvchost()
    {
        var facts = new FakeGuestFacts
        {
            OwnPid = 999,
            Procs =
            [
                (1, "svchost"),
                (2, "SVCHOST"),
                (3, "Svchost"),
                (4, "System"),
            ]
        };

        var sampler = new Sampler(facts, _ => { });
        var run = sampler.Run(new SamplerOptions(TimeSpan.Zero, 1, TimeSpan.Zero));

        var sample = run.Samples[0];
        Assert.Equal(3, sample.Svchost);
    }

    [Fact]
    public void Sampler_InUseIsTotalMinusAvailableInMib()
    {
        var facts = new FakeGuestFacts
        {
            TotalPhys = 16L * 1024 * 1024 * 1024,
            AvailPhys = 12L * 1024 * 1024 * 1024, // 4 GB in-use = 4096 MiB
        };

        var sampler = new Sampler(facts, _ => { });
        var run = sampler.Run(new SamplerOptions(TimeSpan.Zero, 1, TimeSpan.Zero));

        Assert.Equal(4096, run.Samples[0].InUseMb);
        Assert.Equal(16384, run.VisibleMb);
    }

    [Fact]
    public void Sampler_FlagsSleep()
    {
        var facts = new FakeGuestFacts();
        // Wall clock advances 120s, but unbiased time only advances 30s (fell 90s behind > 30s threshold)
        Action<TimeSpan> sleep = _ =>
        {
            facts.CurrentTime += TimeSpan.FromSeconds(120);
            facts.Unbiased += TimeSpan.FromSeconds(30);
        };

        var sampler = new Sampler(facts, sleep);
        var run = sampler.Run(new SamplerOptions(TimeSpan.Zero, 1, TimeSpan.Zero));

        Assert.True(run.Slept);
    }

    [Fact]
    public void WinGuestFacts_ReadsThisPc()
    {
        var facts = new WinGuestFacts();
        Assert.True(facts.Processes().Count > 50);
        var buildStr = facts.WindowsBuild().Split('.')[0];
        Assert.True(int.TryParse(buildStr, out int build) && build >= 26100);
        Assert.True(facts.OwnPid > 0);
    }
}
