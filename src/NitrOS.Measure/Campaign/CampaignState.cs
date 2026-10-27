namespace NitrOS.Measure.Campaign;

public enum RunStatus
{
    Pending,
    Running,
    Done,
    Failed,
}

public record RunState(
    string RunId,
    RunStatus Status,
    int Attempts,
    string? ResultFile,
    string? LastError);

public record CampaignState(
    string CampaignId,
    string WindowsBuild,
    string CliCommit,
    DateTimeOffset Created,
    IReadOnlyList<RunSpec> Runs,
    IReadOnlyList<RunState> States);
