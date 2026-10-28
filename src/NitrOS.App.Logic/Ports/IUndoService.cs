namespace NitrOS.App.Logic.Ports;

public sealed record UndoOutcome(
    IReadOnlyList<string> Undone,
    IReadOnlyList<(string SwitchId, string Reason)> CannotUndo,
    IReadOnlyList<string> NothingToUndo,
    string? Refused);

public interface IUndoService
{
    Task<UndoOutcome> UndoAsync(string journalPath, IReadOnlyCollection<string>? switchIds, CancellationToken ct);
}
