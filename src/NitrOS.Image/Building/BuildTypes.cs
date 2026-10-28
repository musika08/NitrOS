namespace NitrOS.Image.Building;

using System;
using System.Collections.Generic;
using NitrOS.Core.Model;

public enum BuildStep
{
    Prepare,
    CopyMedia,
    ExportImage,
    Mount,
    CheckServicing,
    ApplyDism,
    ApplyFiles,
    ApplyRegistry,
    WriteFirstBoot,
    Unmount,
    ExportFinal,
    WriteIso,
    Cleanup,
    Finished
}

public enum ImagePhase
{
    Dism,
    Files,
    Registry,
    FirstBoot
}

public enum ImageOutcome
{
    Applied,
    Deferred,
    Skipped,
    Failed
}

public sealed record ImageEntry(
    string SwitchId,
    int ActionIndex,
    SwitchAction Action,
    ImagePhase Phase,
    ImageOutcome? Outcome,
    string? Detail,
    DateTimeOffset At);

public sealed record BuildRequest(
    string Source,
    string OutputIso,
    int? EditionIndex,
    IReadOnlyList<string> SwitchIds,
    CardId? Card);

public sealed record SourceFingerprint(
    long Length,
    DateTimeOffset LastWriteUtc);

public enum SwitchImageResult
{
    Applied,
    PartlyApplied,
    Failed,
    Skipped
}

public enum BuildStatus
{
    Done,
    Refused,
    Failed,
    Cancelled,
    StoppedLowBattery
}

public sealed record BuildProgress(
    BuildStep Step,
    string? SwitchId,
    int Done,
    int Total,
    string? LogLine);

public sealed record BuildResult(
    BuildStatus Status,
    string? Reason,
    string? OutputPath,
    IReadOnlyDictionary<string, SwitchImageResult> Switches,
    IReadOnlyList<(string SwitchId, string Detail)> Problems,
    int DeferredCount,
    string? WorkFolder);
