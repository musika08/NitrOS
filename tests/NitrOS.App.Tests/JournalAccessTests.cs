using NitrOS.App.Logic;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Live.Journal;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class JournalAccessTests
{
    [Fact]
    public void JournalAccess_ListDoesNotEnsureFolder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        int ensureCount = 0;
        var session = new FakeSession();

        try
        {
            var ja = new JournalAccess(temp, session, _ => FolderState.Missing, _ => ensureCount++);
            var listing = ja.ListMine();

            Assert.Equal(0, ensureCount);
            Assert.Empty(listing.Mine);
            Assert.Null(listing.FolderProblem);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void JournalAccess_OpenForRunRefusesWhenNotElevated()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        var nonElevatedSession = new FakeSession(elevated: false);

        try
        {
            var ja = new JournalAccess(temp, nonElevatedSession, _ => FolderState.Secure, _ => { });
            var ex = Assert.Throws<InvalidOperationException>(() => ja.OpenForRun());
            Assert.Equal(Texts.NotElevated, ex.Message);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void JournalAccess_ListsOnlyThisAccount()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var mySid = "S-1-5-21-MY-ACCOUNT";
        var otherSid = "S-1-5-21-OTHER-ACCOUNT";
        var session = new FakeSession(processSid: mySid);

        try
        {
            var store = new JournalStore(temp);
            store.Save(new RunJournal("20261028-100000", mySid, "26100.1", ["telemetry-off"], []));
            store.Save(new RunJournal("20261028-110000", otherSid, "26100.1", ["widgets-off"], []));

            var ja = new JournalAccess(temp, session, _ => FolderState.Secure, _ => { });
            var listing = ja.ListMine();

            Assert.Single(listing.Mine);
            Assert.Equal("20261028-100000", listing.Mine[0].Journal.RunId);
            Assert.Equal(1, listing.OtherAccounts);
            Assert.Equal(0, listing.Unreadable);
            Assert.Null(listing.FolderProblem);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void JournalAccess_InsecureFolderReportsProblem()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        var session = new FakeSession();

        try
        {
            var ja = new JournalAccess(temp, session, _ => throw new InvalidOperationException("Insecure folder!"), _ => { });
            var listing = ja.ListMine();

            Assert.Empty(listing.Mine);
            Assert.Equal("Insecure folder!", listing.FolderProblem);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
