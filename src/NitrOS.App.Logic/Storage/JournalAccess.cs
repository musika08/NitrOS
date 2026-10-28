using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Windows;

namespace NitrOS.App.Logic.Storage;

public sealed record JournalListing(
    IReadOnlyList<(string Path, RunJournal Journal)> Mine,
    int OtherAccounts,
    int Unreadable,
    string? FolderProblem);

public sealed class JournalAccess(
    string journalDir,
    ISessionFacts session,
    Func<string, FolderState> check,
    Action<string> ensure)
{
    private readonly string _journalDir = journalDir ?? throw new ArgumentNullException(nameof(journalDir));
    private readonly ISessionFacts _session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly Func<string, FolderState> _check = check ?? throw new ArgumentNullException(nameof(check));
    private readonly Action<string> _ensure = ensure ?? throw new ArgumentNullException(nameof(ensure));

    public JournalStore OpenForRun()
    {
        if (!_session.IsElevated) throw new InvalidOperationException(Texts.NotElevated);
        _ensure(_journalDir);
        return new JournalStore(_journalDir);
    }

    public JournalListing ListMine()
    {
        try
        {
            var state = _check(_journalDir);
            if (state == FolderState.Missing || !Directory.Exists(_journalDir))
                return new JournalListing([], 0, 0, null);
        }
        catch (Exception ex)
        {
            return new JournalListing([], 0, 0, ex.Message);
        }

        var store = new JournalStore(_journalDir);
        var paths = store.List();
        var mine = new List<(string Path, RunJournal Journal)>();
        int other = 0;
        int unreadable = 0;

        foreach (var path in paths)
        {
            try
            {
                var j = store.Load(path);
                if (string.Equals(j.UserSid, _session.ProcessUserSid, StringComparison.OrdinalIgnoreCase))
                {
                    mine.Add((path, j));
                }
                else
                {
                    other++;
                }
            }
            catch
            {
                unreadable++;
            }
        }

        return new JournalListing(mine, other, unreadable, null);
    }
}
