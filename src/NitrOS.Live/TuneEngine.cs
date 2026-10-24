using System.Globalization;
using System.Runtime.CompilerServices;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

[assembly: InternalsVisibleTo("NitrOS.Live.Tests")]

namespace NitrOS.Live;

public sealed record TuneOptions(bool AllowWithoutRestorePoint, string UserSid, string WindowsBuild);

/// <summary>PartlyApplied: some actions applied, others skipped. Skipped: every action was skipped.</summary>
public enum SwitchResult { Applied, PartlyApplied, Failed, Skipped }

/// <summary>Reported after each action. Done and Total count actions across the whole run.</summary>
public sealed record TuneProgress(string SwitchId, int Done, int Total);

/// <summary>
/// The outcome of a run. Switches holds only the switches the run reached; a cancelled run leaves the rest out.
/// JournalPath is empty when the run was refused.
/// </summary>
public sealed record TuneResult(
    string JournalPath,
    RestorePointOutcome RestorePoint,
    IReadOnlyDictionary<string, SwitchResult> Switches,
    bool Refused,
    string? RefusedReason);

public sealed record UndoResult(IReadOnlyList<string> Undone, IReadOnlyList<(string SwitchId, string Reason)> CannotUndo);

/// <summary>
/// Applies a selection to this PC with a journal that is saved before and after every action, so the PC can
/// always be put back. A failing switch is rolled back on its own and the run carries on.
/// </summary>
public sealed class TuneEngine
{
    public const string NoRestorePointReason = "System Restore is off on this PC.";
    public const string RestorePointDescription = "NitrOS: before tuning this PC";

    /// <summary>Detail on an entry saved before its Apply ran. A crash leaves it; Undo treats it as applied, which is safe because the prior was read first.</summary>
    public const string PendingDetail = "Not finished: the run stopped before this action reported back.";

    private readonly LiveSystems _s;
    private readonly JournalStore _store;
    private readonly Func<RunJournal, string> _save;

    public TuneEngine(LiveSystems systems, JournalStore store) : this(systems, store, store.Save) { }

    /// <summary>Test seam: every journal save goes through <paramref name="save"/>, so a test can count saves or simulate a crash.</summary>
    internal TuneEngine(LiveSystems systems, JournalStore store, Func<RunJournal, string> save)
    {
        _s = systems ?? throw new ArgumentNullException(nameof(systems));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _save = save ?? throw new ArgumentNullException(nameof(save));
    }

    public TuneResult Apply(Selection selection, TuneOptions options, IProgress<TuneProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(options);
        if (selection.Mode != TargetMode.TunePc)
            throw new ArgumentException("The tune engine only applies selections made for tuning this PC.", nameof(selection));

        var restorePoint = CreateRestorePoint();
        if (restorePoint == RestorePointOutcome.Unavailable && !options.AllowWithoutRestorePoint)
            return new TuneResult("", restorePoint, new Dictionary<string, SwitchResult>(), true, NoRestorePointReason);

        var plan = Order(selection);
        var entries = new List<JournalEntry>();
        var journal = new RunJournal(NewRunId(), options.UserSid, options.WindowsBuild, plan.Select(d => d.Id).ToList(), []);
        string Save()
        {
            journal = journal with { Entries = entries.ToList() };
            return _save(journal);
        }

        var path = Save();
        var results = new Dictionary<string, SwitchResult>();
        var total = plan.Sum(d => d.Live.Count);
        var done = 0;

        foreach (var def in plan)
        {
            if (ct.IsCancellationRequested) break;
            var first = entries.Count;
            var failed = false;
            var cancelled = false;

            for (var i = 0; i < def.Live.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                var action = def.Live[i];
                IActionHandler handler;
                PriorState prior;
                try
                {
                    handler = Handlers.Handlers.For(action);
                    prior = handler.Read(action, _s); // read right before apply, so earlier actions are seen
                }
                catch (Exception ex)
                {
                    entries.Add(new JournalEntry(def.Id, i, action, new MissingPrior(), ActionOutcome.Failed, ex.Message, Now()));
                    path = Save();
                    Report(progress, def.Id, ++done, total);
                    failed = true;
                    break;
                }

                entries.Add(new JournalEntry(def.Id, i, action, prior, ActionOutcome.Applied, PendingDetail, Now()));
                var index = entries.Count - 1;
                path = Save();

                ApplyResult result;
                try
                {
                    result = handler.Apply(action, _s);
                }
                catch (Exception ex)
                {
                    result = ApplyResult.Failed(ex.Message);
                }

                var outcome = result.Outcome is ActionOutcome.Applied or ActionOutcome.Skipped ? result.Outcome : ActionOutcome.Failed;
                entries[index] = entries[index] with { Outcome = outcome, Detail = result.Detail, At = Now() };
                path = Save();
                Report(progress, def.Id, ++done, total);

                if (outcome == ActionOutcome.Failed)
                {
                    // The failed action may have half-applied (for example AC written, DC not): put its prior back too.
                    TryUndo(handler, action, prior);
                    failed = true;
                    break;
                }
            }

            if (failed)
            {
                RollBack(entries, first, Save);
                path = Save();
                results[def.Id] = SwitchResult.Failed;
                continue;
            }

            var mine = entries.Skip(first).ToList();
            var applied = mine.Count(e => e.Outcome == ActionOutcome.Applied);
            if (cancelled)
            {
                if (mine.Count > 0) results[def.Id] = applied > 0 ? SwitchResult.PartlyApplied : SwitchResult.Skipped;
                break;
            }

            results[def.Id] = applied == mine.Count && mine.Count > 0 ? SwitchResult.Applied
                : applied == 0 ? SwitchResult.Skipped
                : SwitchResult.PartlyApplied;
        }

        return new TuneResult(path, restorePoint, results, false, null);
    }

    /// <summary>
    /// Undoes a run in reverse journal order. <paramref name="switchIds"/> null means every switch. Applied, reversible entries are
    /// put back to their prior state (even if Windows changed them since) and marked RolledBack; irreversible ones are reported
    /// with their reinstall hint. The journal is saved after each undone entry.
    /// </summary>
    public UndoResult Undo(string journalPath, IReadOnlyCollection<string>? switchIds)
    {
        var journal = _store.Load(journalPath);
        var wanted = switchIds is null ? null : new HashSet<string>(switchIds, StringComparer.Ordinal);
        var entries = journal.Entries.ToList();
        var touched = new List<string>();
        var cannot = new List<(string SwitchId, string Reason)>();

        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (wanted is not null && !wanted.Contains(e.SwitchId)) continue;
            if (!touched.Contains(e.SwitchId)) touched.Add(e.SwitchId);
            if (e.Outcome != ActionOutcome.Applied) continue; // Skipped, Failed and already undone entries are no-ops

            if (e.Prior is IrreversiblePrior irreversible)
            {
                cannot.Add((e.SwitchId, irreversible.ReinstallHint));
                continue;
            }

            try
            {
                Handlers.Handlers.For(e.Action).Undo(e.Action, e.Prior, _s);
            }
            catch (Exception ex)
            {
                cannot.Add((e.SwitchId, ex.Message));
                continue;
            }

            entries[i] = e with { Outcome = ActionOutcome.RolledBack, Detail = "Undone", At = Now() };
            journal = journal with { Entries = entries.ToList() };
            _save(journal);
        }

        if (wanted is not null)
        {
            foreach (var id in switchIds!)
            {
                if (!touched.Contains(id) && !journal.SwitchIds.Contains(id))
                    cannot.Add((id, "Not part of this run."));
            }
        }

        var undone = touched.Where(id => !cannot.Any(c => c.SwitchId == id)).ToList();
        return new UndoResult(undone, cannot);
    }

    /// <summary>Undoes the switch's applied entries from <paramref name="first"/> on, newest first. Irreversible entries stay Applied.</summary>
    private void RollBack(List<JournalEntry> entries, int first, Func<string> save)
    {
        for (var j = entries.Count - 1; j >= first; j--)
        {
            var e = entries[j];
            if (e.Outcome != ActionOutcome.Applied || e.Prior is IrreversiblePrior) continue;
            try
            {
                Handlers.Handlers.For(e.Action).Undo(e.Action, e.Prior, _s);
                entries[j] = e with { Outcome = ActionOutcome.RolledBack, At = Now() };
            }
            catch (Exception ex)
            {
                entries[j] = e with { Detail = "Rollback failed: " + ex.Message, At = Now() };
            }
            save();
        }
    }

    private void TryUndo(IActionHandler handler, SwitchAction action, PriorState prior)
    {
        try { handler.Undo(action, prior, _s); }
        catch (Exception) { /* best effort; the entry already says Failed */ }
    }

    private RestorePointOutcome CreateRestorePoint()
    {
        try { return _s.RestorePoint.Create(RestorePointDescription); }
        catch (Exception) { return RestorePointOutcome.Unavailable; }
    }

    /// <summary>Catalog order, except that switches with a power.scheme move ahead of all others (so power.set writes to the new scheme).</summary>
    private static List<SwitchDef> Order(Selection selection) =>
        selection.Catalog.Switches
            .Where(d => selection.IsOn(d.Id))
            .OrderBy(d => d.Live.Any(a => a is PowerScheme) ? 0 : 1) // OrderBy is stable
            .ToList();

    /// <summary>Local time yyyyMMdd-HHmmss, with -2, -3 and so on when a journal with that name already exists.</summary>
    private string NewRunId()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var taken = _store.List().Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var id = stamp;
        for (var n = 2; taken.Contains(id); n++) id = $"{stamp}-{n}";
        return id;
    }

    private static void Report(IProgress<TuneProgress>? progress, string switchId, int done, int total) =>
        progress?.Report(new TuneProgress(switchId, done, total));

    private static DateTimeOffset Now() => DateTimeOffset.Now;
}
