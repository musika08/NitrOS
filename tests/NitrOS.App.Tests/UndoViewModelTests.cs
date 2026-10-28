using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class UndoViewModelTests
{
    private sealed class FakeRestarter : IRestarter
    {
        public int RestartCount { get; private set; }
        public void Restart() => RestartCount++;
    }

    private sealed class FakeUndoService : IUndoService
    {
        public List<(string Path, IReadOnlyCollection<string>? SwitchIds)> Calls { get; } = [];
        public Func<string, IReadOnlyCollection<string>?, UndoOutcome>? Handler { get; set; }

        public Task<UndoOutcome> UndoAsync(string journalPath, IReadOnlyCollection<string>? switchIds, CancellationToken ct)
        {
            Calls.Add((journalPath, switchIds));
            if (Handler is not null)
            {
                return Task.FromResult(Handler(journalPath, switchIds));
            }
            return Task.FromResult(new UndoOutcome(switchIds?.ToList() ?? ["all"], [], [], null));
        }
    }

    private static (UndoViewModel vm, FakeUndoService undoService, FakeDialogs dialogs, string tempDir) Create(
        Catalog catalog,
        FakeSession? session = null,
        RunGate? gate = null)
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        var s = session ?? new FakeSession();
        var g = gate ?? new RunGate();
        var nav = new Navigator(g);
        var ja = new JournalAccess(temp, s, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp));
        var undo = new FakeUndoService();
        var dialogs = new FakeDialogs();
        var restarter = new FakeRestarter();

        var vm = new UndoViewModel(ja, undo, catalog, dialogs, restarter, g, nav);
        return (vm, undo, dialogs, temp);
    }

    [Fact]
    public void Undo_ListsOnlyThisAccountsJournals()
    {
        var repo = TestCatalog.LoadRepo();
        var mySid = "S-1-5-21-MY-ACCOUNT";
        var otherSid = "S-1-5-21-OTHER-ACCOUNT";
        var session = new FakeSession(processSid: mySid);

        var (vm, _, _, temp) = Create(repo, session: session);

        try
        {
            var store = new JournalStore(temp);
            store.Save(new RunJournal("20261028-100000", mySid, "26100.1", ["telemetry-off"], []));
            store.Save(new RunJournal("20261028-110000", mySid, "26100.1", ["fax-off"], []));
            store.Save(new RunJournal("20261028-120000", otherSid, "26100.1", ["edge-startup-off"], []));

            vm.OnShown(null);

            Assert.Equal(2, vm.Runs.Count);
            Assert.NotNull(vm.HiddenNote);
            Assert.Contains("1", vm.HiddenNote);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task Undo_OwnerExceptionShowsMessage()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, undo, _, temp) = Create(repo);

        try
        {
            var dummyAction = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val", RegistryKind.Dword, "1");
            var store = new JournalStore(temp);
            store.Save(new RunJournal(
                "20261028-100000",
                "S-1-5-21-1",
                "26100.1",
                ["consumer-features-off"],
                [new JournalEntry("consumer-features-off", 0, dummyAction, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now)]));

            vm.OnShown(null);
            var sw = vm.Runs[0].Switches[0];

            undo.Handler = (p, ids) => new UndoOutcome([], [], [], TuneEngine.OtherAccountReason);

            await vm.UndoSwitch.ExecuteAsync(sw);

            Assert.Equal(TuneEngine.OtherAccountReason, vm.LastResult);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task Undo_BusyShowsMessageAndKeepsList()
    {
        var repo = TestCatalog.LoadRepo();
        var gate = new RunGate();
        var (vm, _, _, temp) = Create(repo, gate: gate);

        try
        {
            var dummyAction = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val", RegistryKind.Dword, "1");
            var store = new JournalStore(temp);
            store.Save(new RunJournal(
                "20261028-100000",
                "S-1-5-21-1",
                "26100.1",
                ["consumer-features-off"],
                [new JournalEntry("consumer-features-off", 0, dummyAction, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now)]));

            vm.OnShown(null);
            var sw = vm.Runs[0].Switches[0];

            var lease = gate.TryEnter();
            Assert.NotNull(lease);

            await vm.UndoSwitch.ExecuteAsync(sw);

            Assert.Equal(TuneEngine.BusyReason, vm.LastResult);
            Assert.Single(vm.Runs);

            lease.Dispose();
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task Undo_PerSwitchPassesOnlyThatId()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, undo, _, temp) = Create(repo);

        try
        {
            var dummyAction = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val", RegistryKind.Dword, "1");
            var store = new JournalStore(temp);
            store.Save(new RunJournal(
                "20261028-100000",
                "S-1-5-21-1",
                "26100.1",
                ["consumer-features-off"],
                [new JournalEntry("consumer-features-off", 0, dummyAction, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now)]));

            vm.OnShown(null);
            var sw = vm.Runs[0].Switches[0];

            await vm.UndoSwitch.ExecuteAsync(sw);

            Assert.Single(undo.Calls);
            Assert.NotNull(undo.Calls[0].SwitchIds);
            Assert.Single(undo.Calls[0].SwitchIds!);
            Assert.Equal("consumer-features-off", undo.Calls[0].SwitchIds!.First());
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task Undo_EverythingAsksOnceAndGoesNewestFirst()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, undo, dialogs, temp) = Create(repo);

        try
        {
            var dummyAction = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val", RegistryKind.Dword, "1");
            var store = new JournalStore(temp);
            store.Save(new RunJournal(
                "20261028-100000",
                "S-1-5-21-1",
                "26100.1",
                ["consumer-features-off"],
                [new JournalEntry("consumer-features-off", 0, dummyAction, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now.AddHours(-1))]));

            store.Save(new RunJournal(
                "20261028-120000",
                "S-1-5-21-1",
                "26100.1",
                ["fax-off"],
                [new JournalEntry("fax-off", 0, dummyAction, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now)]));

            vm.OnShown(null);

            dialogs.NextConfirmResult = true;
            await vm.UndoEverything.ExecuteAsync(null);

            Assert.Single(dialogs.ConfirmRequests);
            Assert.Equal("Put back every change NitrOS made on this account?", dialogs.ConfirmRequests[0].Title);

            Assert.Equal(2, undo.Calls.Count);
            // Newest first: 120000 before 100000
            Assert.Contains("20261028-120000", undo.Calls[0].Path);
            Assert.Null(undo.Calls[0].SwitchIds);
            Assert.Contains("20261028-100000", undo.Calls[1].Path);
            Assert.Null(undo.Calls[1].SwitchIds);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void Undo_IrreversibleSwitchHasNoUndoButton()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _, temp) = Create(repo);

        try
        {
            var dummyAction = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val", RegistryKind.Dword, "1");
            var store = new JournalStore(temp);
            store.Save(new RunJournal(
                "20261028-100000",
                "S-1-5-21-1",
                "26100.1",
                ["edge-remove"],
                [new JournalEntry("edge-remove", 0, dummyAction, new IrreversiblePrior("edge", null, "Reinstall Edge"), ActionOutcome.Applied, null, DateTimeOffset.Now)]));

            vm.OnShown(null);

            var sw = vm.Runs[0].Switches[0];
            Assert.Equal(SwitchUndoState.CannotUndo, sw.State);
            Assert.False(vm.UndoSwitch.CanExecute(sw));
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
