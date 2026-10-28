using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;

namespace NitrOS.App.Logic.Running;

public enum RunKind { Tune, Iso, XboxRestore }

/// <summary>Plan 6 fills these from its own source-ISO screen; null for Tune runs.</summary>
public sealed record IsoPaths(string SourceIso, string OutputIso)
{
    public int? EditionIndex { get; init; }
}

public sealed record RunRequest(RunKind Kind, Selection Selection, CardId? Card, bool AllowWithoutRestorePoint, IsoPaths? Iso);

public sealed record RunReadiness(bool Ready, string? Reason);

/// <summary>Done and Total count steps (actions for Tune). LogLine is one plain sentence or null.</summary>
public sealed record RunProgress(string? SwitchId, int Done, int Total, string? LogLine);

public enum SwitchOutcome { Applied, PartlyApplied, Failed, Skipped }

public sealed record RunResult(
    RunKind Kind,
    bool Refused,
    string? RefusedReason,
    bool Cancelled,
    IReadOnlyDictionary<string, SwitchOutcome> Switches,
    IReadOnlyList<string> MayBeChanged,
    IReadOnlyList<(string SwitchId, string Detail)> Problems)
{
    public bool CanRetryWithoutRestorePoint { get; init; }
    public string? JournalPath { get; init; }               // Tune
    public RestorePointOutcome? RestorePoint { get; init; } // Tune
    public MachineSnapshot? Before { get; init; }           // Tune
    public MachineSnapshot? After { get; init; }            // Tune
    public bool NeedsRestart { get; init; }
    public string? OutputPath { get; init; }                // ISO, plan 6
    public RunRequest? Request { get; init; }
}
