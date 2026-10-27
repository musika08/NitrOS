namespace NitrOS.Measure.Tests;

public class StatsTests
{
    [Fact]
    public void Median_OfFiveIsTheThirdSorted()
    {
        var values = new[] { 5, 1, 4, 2, 3 };
        var median = Stats.Median(values);
        Assert.Equal(3, median);
    }

    [Fact]
    public void Median_EvenCountThrows()
    {
        var values = new[] { 1, 2, 3, 4 };
        Assert.Throws<ArgumentException>(() => Stats.Median(values));
    }

    [Fact]
    public void Summary_TakesMediansIndependently()
    {
        // 5 samples where medians of inUse, proc, svchost come from different samples
        var samples = new[]
        {
            new MemorySample(DateTimeOffset.UtcNow, 1000, 150, 40),
            new MemorySample(DateTimeOffset.UtcNow, 2000, 110, 80),
            new MemorySample(DateTimeOffset.UtcNow, 3000, 130, 60), // InUse median is 3000 (middle of 1000, 2000, 3000, 4000, 5000)
            new MemorySample(DateTimeOffset.UtcNow, 4000, 120, 70), // Proc median is 130 (middle of 110, 120, 130, 140, 150)
            new MemorySample(DateTimeOffset.UtcNow, 5000, 140, 50), // Svchost median is 60 (middle of 40, 50, 60, 70, 80)
        };

        var summary = Stats.Summarize(samples);

        Assert.Equal(3000, summary.InUseMb);
        Assert.Equal(130, summary.Processes);
        Assert.Equal(60, summary.Svchost);
    }

    [Fact]
    public void Summary_NeedsExactlyFiveSamples()
    {
        var four = new[]
        {
            new MemorySample(DateTimeOffset.UtcNow, 100, 10, 5),
            new MemorySample(DateTimeOffset.UtcNow, 200, 20, 10),
            new MemorySample(DateTimeOffset.UtcNow, 300, 30, 15),
            new MemorySample(DateTimeOffset.UtcNow, 400, 40, 20),
        };

        Assert.Throws<ArgumentException>(() => Stats.Summarize(four));
    }
}
