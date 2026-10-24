using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live;

/// <summary>Finds switches from a run that Windows (or the user) has since turned back.</summary>
public sealed class DriftChecker(LiveSystems systems)
{
    /// <summary>
    /// Switch ids, in journal order, where an Applied, reversible action no longer holds the state it set. Skipped, failed and
    /// rolled-back entries, irreversible entries and scripts (which have no state to inspect) are ignored. An action whose
    /// state cannot be read is not reported.
    /// </summary>
    public IReadOnlyList<string> Check(RunJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        var drifted = new List<string>();
        foreach (var e in journal.Entries)
        {
            if (e.Outcome != ActionOutcome.Applied || e.Prior is IrreversiblePrior || e.Action is Script) continue;
            if (drifted.Contains(e.SwitchId)) continue;
            bool held;
            try
            {
                held = Handlers.Handlers.For(e.Action).IsInEffect(e.Action, systems);
            }
            catch (Exception)
            {
                continue;
            }
            if (!held) drifted.Add(e.SwitchId);
        }
        return drifted;
    }
}
