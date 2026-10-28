using System.Globalization;
using NitrOS.Core.Model;
using NitrOS.Live.Journal;

namespace NitrOS.App.Logic.Screens;

public enum SwitchUndoState { CanUndo, Undone, CannotUndo, NothingToUndo }

public sealed record SwitchSummary(string Id, string Name, SwitchUndoState State, string? Hint);

public sealed record RunSummary(string Path, string RunId, DateTimeOffset When, IReadOnlyList<SwitchSummary> Switches)
{
    public static RunSummary Build(string path, RunJournal journal, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(catalog);

        DateTimeOffset when;
        if (journal.Entries.Count > 0)
        {
            when = journal.Entries[0].At;
        }
        else
        {
            var rawRunId = journal.RunId;
            var parts = rawRunId.Split('-');
            string timePart = parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : rawRunId;

            if (DateTimeOffset.TryParseExact(timePart, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                when = parsed;
            }
            else
            {
                when = DateTimeOffset.Now;
            }
        }

        var summaries = new List<SwitchSummary>();
        var switchIds = journal.SwitchIds.Count > 0
            ? journal.SwitchIds
            : journal.Entries.Select(e => e.SwitchId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var id in switchIds)
        {
            var sw = catalog.Switches.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            var name = sw?.Name ?? id;

            var entries = journal.Entries.Where(e => string.Equals(e.SwitchId, id, StringComparison.OrdinalIgnoreCase)).ToList();

            SwitchUndoState state;
            string? hint = null;

            if (entries.Count == 0)
            {
                state = SwitchUndoState.NothingToUndo;
            }
            else
            {
                var appliedIrr = entries.FirstOrDefault(e => e.Outcome == ActionOutcome.Applied && e.Prior is IrreversiblePrior);
                if (appliedIrr is not null)
                {
                    state = SwitchUndoState.CannotUndo;
                    hint = ((IrreversiblePrior)appliedIrr.Prior).ReinstallHint;
                }
                else if (entries.Any(e => e.Outcome == ActionOutcome.Applied))
                {
                    state = SwitchUndoState.CanUndo;
                }
                else if (entries.All(e => e.Outcome == ActionOutcome.RolledBack))
                {
                    state = SwitchUndoState.Undone;
                }
                else
                {
                    state = SwitchUndoState.NothingToUndo;
                }
            }

            summaries.Add(new SwitchSummary(id, name, state, hint));
        }

        return new RunSummary(path, journal.RunId, when, summaries);
    }
}
