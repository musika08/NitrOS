namespace NitrOS.Measure;

public static class Stats
{
    public static int Median(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0 || values.Count % 2 == 0)
        {
            throw new ArgumentException("Count must be odd.", nameof(values));
        }

        var sorted = values.OrderBy(x => x).ToList();
        return sorted[sorted.Count / 2];
    }

    public static RunSummary Summarize(IReadOnlyList<MemorySample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count != 5)
        {
            throw new ArgumentException("Must have exactly 5 samples.", nameof(samples));
        }

        var inUseMedian = Median(samples.Select(s => s.InUseMb).ToList());
        var procMedian = Median(samples.Select(s => s.Processes).ToList());
        var svchostMedian = Median(samples.Select(s => s.Svchost).ToList());

        return new RunSummary(inUseMedian, procMedian, svchostMedian);
    }
}
