using NitrOS.Core.Model;

namespace NitrOS.Live.Journal;

public enum ActionOutcome { Applied, Skipped, Failed, RolledBack }

public sealed record JournalEntry(
    string SwitchId,
    int ActionIndex,
    SwitchAction Action,
    PriorState Prior,
    ActionOutcome Outcome,
    string? Detail,
    DateTimeOffset At);

public sealed record RunJournal(
    string RunId,
    string UserSid,
    string WindowsBuild,
    IReadOnlyList<string> SwitchIds,
    IReadOnlyList<JournalEntry> Entries);
