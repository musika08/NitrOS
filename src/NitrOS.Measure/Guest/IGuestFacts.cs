namespace NitrOS.Measure.Guest;

public interface IGuestFacts
{
    (long TotalPhys, long AvailPhys) Memory();
    IReadOnlyList<(int Pid, string Name)> Processes();
    TimeSpan UnbiasedTime();
    DateTimeOffset Now();
    string WindowsBuild();
    int OwnPid { get; }
}

public record SamplerOptions(TimeSpan Idle, int Count, TimeSpan Interval)
{
    public static SamplerOptions Default { get; } =
        new(TimeSpan.FromMinutes(10), 5, TimeSpan.FromSeconds(30));
}

public record SampleRun(
    IReadOnlyList<MemorySample> Samples,
    int VisibleMb,
    string WindowsBuild,
    bool Slept,
    DateTimeOffset Started,
    DateTimeOffset Finished);
