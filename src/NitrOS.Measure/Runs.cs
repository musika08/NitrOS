namespace NitrOS.Measure;

public record MemorySample(DateTimeOffset At, int InUseMb, int Processes, int Svchost);

public record RunSummary(int InUseMb, int Processes, int Svchost);

public enum RunKind
{
    Baseline,
    Switch,
    Card,
    Variant,
}

public record RunSpec(
    string RunId,
    RunKind Kind,
    IReadOnlyList<string> Ids,
    string? Card,
    string DefinitionHash);

public record RunResult(
    string RunId,
    string WindowsBuild,
    int VisibleMb,
    IReadOnlyList<MemorySample> Samples,
    RunSummary Median,
    int? ApplyExitCode,
    IReadOnlyDictionary<string, string> SwitchResults,
    bool Slept,
    DateTimeOffset Started,
    DateTimeOffset Finished);
