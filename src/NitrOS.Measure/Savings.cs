using NitrOS.Core.Model;

namespace NitrOS.Measure;

public static class Savings
{
    public static Measurement For(RunResult run, Baseline baseline, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(baseline);

        var memDiff = baseline.Value.InUseMb - run.Median.InUseMb;
        var procDiff = baseline.Value.Processes - run.Median.Processes;

        var memoryMb = Math.Abs(memDiff) <= baseline.MemoryNoiseMb ? 0 : memDiff;
        var processes = Math.Abs(procDiff) <= baseline.ProcessNoise ? 0 : procDiff;

        return new Measurement(memoryMb, processes, run.WindowsBuild, date);
    }

    public static bool IsFlagged(RunResult run, Baseline baseline)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(baseline);

        var memDiff = baseline.Value.InUseMb - run.Median.InUseMb;
        var procDiff = baseline.Value.Processes - run.Median.Processes;

        return memDiff < -baseline.MemoryNoiseMb || procDiff < -baseline.ProcessNoise;
    }
}
