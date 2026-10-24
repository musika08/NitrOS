using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live;

/// <summary>Finds switches from a run that Windows (or the user) has since turned back.</summary>
public sealed class DriftChecker(LiveSystems systems)
{
    /// <summary>Actions from the last <see cref="Check"/> whose state could not be read: drift unknown, not reported as drift.</summary>
    public IReadOnlyList<(string SwitchId, string Error)> LastErrors { get; private set; } = [];

    /// <summary>
    /// Switch ids, in journal order, where an Applied, reversible action no longer holds the state it set. Skipped, failed and
    /// rolled-back entries, irreversible entries and scripts (which have no state to inspect) are ignored. A power.set is checked
    /// on the scheme it wrote to (from its prior), so switching power plans later is not drift. A journal written by another
    /// Windows account than <paramref name="userSid"/> throws <see cref="JournalOwnerException"/>.
    /// </summary>
    public IReadOnlyList<string> Check(RunJournal journal, string userSid)
    {
        ArgumentNullException.ThrowIfNull(journal);
        TuneEngine.RequireOwner(journal, userSid);
        var drifted = new List<string>();
        var errors = new List<(string SwitchId, string Error)>();
        foreach (var e in journal.Entries)
        {
            if (e.Outcome != ActionOutcome.Applied || e.Prior is IrreversiblePrior || e.Action is Script) continue;
            if (drifted.Contains(e.SwitchId)) continue;
            bool held;
            try
            {
                held = e is { Action: PowerSet set, Prior: PowerPrior prior }
                    ? PowerSetHolds(set, prior.Scheme)
                    : Handlers.Handlers.For(e.Action).IsInEffect(e.Action, systems);
            }
            catch (Exception ex)
            {
                errors.Add((e.SwitchId, ex.Message));
                continue;
            }
            if (!held) drifted.Add(e.SwitchId);
        }
        LastErrors = errors;
        return drifted;
    }

    private bool PowerSetHolds(PowerSet a, string scheme)
    {
        if (systems.Power.Get(scheme, a.Subgroup, a.Setting) is not { } cur) return false;
        return (a.Ac is null || cur.Ac == a.Ac) && (a.Dc is null || cur.Dc == a.Dc);
    }
}
