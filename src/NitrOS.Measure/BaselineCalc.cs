namespace NitrOS.Measure;

public record Baseline(RunSummary Value, int MemoryNoiseMb, int ProcessNoise);

public static class BaselineCalc
{
    public static Baseline From(IReadOnlyList<RunResult> baselineRuns)
    {
        ArgumentNullException.ThrowIfNull(baselineRuns);
        if (baselineRuns.Count < 3)
        {
            throw new InvalidOperationException($"Needs at least 3 baseline runs, got {baselineRuns.Count}.");
        }

        var firstBuild = baselineRuns[0].WindowsBuild;
        if (baselineRuns.Any(r => r.WindowsBuild != firstBuild))
        {
            throw new InvalidOperationException("Baseline runs have mixed Windows builds.");
        }

        if (baselineRuns.Any(r => r.Slept))
        {
            throw new InvalidOperationException("One or more baseline runs slept.");
        }

        var inUses = baselineRuns.Select(r => r.Median.InUseMb).ToList();
        var processes = baselineRuns.Select(r => r.Median.Processes).ToList();
        var svchosts = baselineRuns.Select(r => r.Median.Svchost).ToList();

        var memNoise = inUses.Max() - inUses.Min();
        var procNoise = processes.Max() - processes.Min();

        var medianInUse = Stats.Median(inUses);
        var medianProcesses = Stats.Median(processes);
        var medianSvchost = Stats.Median(svchosts);

        return new Baseline(
            new RunSummary(medianInUse, medianProcesses, medianSvchost),
            memNoise,
            procNoise);
    }
}
