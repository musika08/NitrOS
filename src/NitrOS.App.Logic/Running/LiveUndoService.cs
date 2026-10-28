using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Storage;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;

namespace NitrOS.App.Logic.Running;

public sealed class LiveUndoService(
    ISessionFacts session,
    JournalAccess journals,
    Func<LiveSystems> systems,
    AppStateStore state) : IUndoService
{
    private readonly ISessionFacts _session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly JournalAccess _journals = journals ?? throw new ArgumentNullException(nameof(journals));
    private readonly Func<LiveSystems> _systems = systems ?? throw new ArgumentNullException(nameof(systems));
    private readonly AppStateStore _state = state ?? throw new ArgumentNullException(nameof(state));

    public async Task<UndoOutcome> UndoAsync(string journalPath, IReadOnlyCollection<string>? switchIds, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);

        var check = SessionGuard.Check(_session);
        if (!check.Ok)
        {
            return new UndoOutcome([], [], [], check.Reason);
        }

        JournalStore store;
        try
        {
            store = _journals.OpenForRun();
        }
        catch (Exception ex)
        {
            return new UndoOutcome([], [], [], ex.Message);
        }

        try
        {
            var engine = new TuneEngine(_systems(), store);
            var result = await Task.Run(() => engine.Undo(journalPath, switchIds, _session.ProcessUserSid), ct);

            if (result.Undone.Count > 0)
            {
                var runId = Path.GetFileNameWithoutExtension(journalPath);
                _state.SetHealthPending(new HealthPending(runId, _session.ProcessUserSid, new MachineSnapshot(0, 0, DateTimeOffset.Now), DateTimeOffset.Now));
            }

            return new UndoOutcome(result.Undone, result.CannotUndo, result.NothingToUndo, null);
        }
        catch (JournalOwnerException)
        {
            return new UndoOutcome([], [], [], TuneEngine.OtherAccountReason);
        }
        catch (TuneBusyException)
        {
            return new UndoOutcome([], [], [], TuneEngine.BusyReason);
        }
        catch (Exception ex)
        {
            return new UndoOutcome([], [], [], ex.Message);
        }
    }
}
