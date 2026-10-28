namespace NitrOS.App.Logic.Ports;

public sealed record DriftFindings(
    IReadOnlyList<string> Drifted,
    IReadOnlyList<(string SwitchId, string Error)> Errors,
    string? FolderProblem);

public interface IDriftService
{
    Task<DriftFindings> CheckAsync(CancellationToken ct);
}
