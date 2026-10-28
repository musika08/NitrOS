using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Measuring;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class HealthTests
{
    private sealed class FakeHealthService(HealthReport report) : IHealthService
    {
        public HealthReport Report { get; set; } = report;
        public IReadOnlySet<string>? LastAppliedIds { get; private set; }

        public Task<HealthReport> RunAsync(IReadOnlySet<string> appliedSwitchIds, CancellationToken ct)
        {
            LastAppliedIds = appliedSwitchIds;
            return Task.FromResult(Report);
        }
    }

    private sealed class FakeUndoService : IUndoService
    {
        public List<(string Path, IReadOnlyCollection<string>? SwitchIds)> Calls { get; } = [];

        public Task<UndoOutcome> UndoAsync(string journalPath, IReadOnlyCollection<string>? switchIds, CancellationToken ct)
        {
            Calls.Add((journalPath, switchIds));
            return Task.FromResult(new UndoOutcome(switchIds?.ToList() ?? [], [], [], null));
        }
    }

    [Fact]
    public async Task Health_UndoOfferedOnlyForAppliedReversibleLikelySwitch()
    {
        var repo = TestCatalog.LoadRepo();
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var session = new FakeSession();
        var ja = new JournalAccess(temp, session, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp));
        var appStore = new AppStateStore(temp, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp), session);
        var meter = new FakeMeter();
        var nav = new Navigator(new RunGate());
        var undo = new FakeUndoService();

        try
        {
            var store = new JournalStore(temp);
            var dummyAction = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val", RegistryKind.Dword, "1");

            // Bluetooth is reversible, Store is irreversible
            store.Save(new RunJournal(
                "20261028-100000",
                session.ProcessUserSid,
                "26100.1",
                ["bluetooth-off", "store-remove"],
                [
                    new JournalEntry("bluetooth-off", 0, dummyAction, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now),
                    new JournalEntry("store-remove", 0, dummyAction, new IrreversiblePrior("store", null, "hint"), ActionOutcome.Applied, null, DateTimeOffset.Now)
                ]));

            var report = new HealthReport([
                new HealthItem("Bluetooth", false, "bluetooth-off", "Service stopped"),
                new HealthItem("Microsoft Store", false, "store-remove", "Package missing")
            ]);

            var healthService = new FakeHealthService(report);

            var vm = new HealthViewModel(ja, healthService, undo, repo, appStore, null, meter, nav);
            await vm.RunCheckAsync();

            Assert.Equal(2, vm.Items.Count);

            var bt = vm.Items.First(i => i.Name == "Bluetooth");
            Assert.NotNull(bt.UndoLikely);

            var storeItem = vm.Items.First(i => i.Name == "Microsoft Store");
            Assert.Null(storeItem.UndoLikely);

            // Execute Bluetooth undo
            await bt.UndoLikely.ExecuteAsync(null);
            Assert.Single(undo.Calls);
            Assert.Equal("bluetooth-off", undo.Calls[0].SwitchIds!.First());
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task Health_ClearsPendingWhenAllOk()
    {
        var repo = TestCatalog.LoadRepo();
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var session = new FakeSession();
        var ja = new JournalAccess(temp, session, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp));
        var appStore = new AppStateStore(temp, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp), session);
        var meter = new FakeMeter();
        var nav = new Navigator(new RunGate());
        var undo = new FakeUndoService();

        try
        {
            appStore.SetHealthPending(new HealthPending("run-1", session.ProcessUserSid, new MachineSnapshot(4000, 100, DateTimeOffset.Now), DateTimeOffset.Now));
            Assert.NotNull(appStore.Read().Health);

            var report = new HealthReport([
                new HealthItem("Bluetooth", true, null),
                new HealthItem("Start menu", true, null)
            ]);

            var healthService = new FakeHealthService(report);

            var vm = new HealthViewModel(ja, healthService, undo, repo, appStore, null, meter, nav);
            await vm.RunCheckAsync();

            Assert.Null(appStore.Read().Health);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
