using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Journal;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class DriftTests
{
    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public void OnShown(object? argument) { }
    }

    [Fact]
    public void Drift_NeverRunsWithoutGo()
    {
        var repo = TestCatalog.LoadRepo();
        var nav = new Navigator(new RunGate());
        nav.Register(ScreenKeys.Review, () => new DummyScreen(ScreenKeys.Review));
        nav.Register(ScreenKeys.Start, () => new DummyScreen(ScreenKeys.Start));

        var draft = new Draft(repo, DeviceProfile.Desktop);
        var vm = new DriftViewModel(repo, draft, nav);

        var findings = new DriftFindings(["fax-off", "consumer-features-off"], [], null);
        vm.OnShown(findings);

        Assert.Equal(2, vm.DriftedNames.Count);

        vm.TurnOffAgain.Execute(null);

        Assert.Equal(ScreenKeys.Review, nav.Current?.Key);
        Assert.NotNull(draft.Selection);
        Assert.Contains("fax-off", draft.Selection.On);
        Assert.Contains("consumer-features-off", draft.Selection.On);
    }

    [Fact]
    public void Drift_ShowsErrorsFromReport()
    {
        var repo = TestCatalog.LoadRepo();
        var nav = new Navigator(new RunGate());
        var draft = new Draft(repo, DeviceProfile.Desktop);
        var vm = new DriftViewModel(repo, draft, nav);

        var findings = new DriftFindings(["fax-off"], [("consumer-features-off", "Registry access denied")], null);
        vm.OnShown(findings);

        Assert.Single(vm.DriftedNames);
        Assert.Single(vm.CouldNotCheck);
        Assert.Contains("Registry access denied", vm.CouldNotCheck[0]);
    }

    [Fact]
    public async Task DriftService_MergesJournalsNewestFirst()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var session = new FakeSession();
        var ja = new JournalAccess(temp, session, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp));

        try
        {
            var store = new JournalStore(temp);
            var action1 = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val1", RegistryKind.Dword, "1");
            var action2 = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val2", RegistryKind.Dword, "1");

            store.Save(new RunJournal(
                "20261028-100000",
                session.ProcessUserSid,
                "26100.1",
                ["sw1"],
                [new JournalEntry("sw1", 0, action1, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now.AddHours(-1))]));

            store.Save(new RunJournal(
                "20261028-120000",
                session.ProcessUserSid,
                "26100.1",
                ["sw2"],
                [new JournalEntry("sw2", 0, action2, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now)]));

            var f = new FakeSystems();
            // Val1 and Val2 are missing in FakeSystems registry, but their prior was MissingPrior, so they drifted back to prior!
            var service = new LiveDriftService(ja, () => f.ToLive(), session);

            var findings = await service.CheckAsync(TestContext.Current.CancellationToken);

            Assert.Null(findings.FolderProblem);
            Assert.Equal(2, findings.Drifted.Count);
            // Newest first order
            Assert.Equal("sw2", findings.Drifted[0]);
            Assert.Equal("sw1", findings.Drifted[1]);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task DriftService_MissingFolderFindsNothingAndCreatesNothing()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        var session = new FakeSession();
        var ja = new JournalAccess(temp, session, _ => FolderState.Missing, _ => { });
        var f = new FakeSystems();

        var service = new LiveDriftService(ja, () => f.ToLive(), session);
        var findings = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Empty(findings.Drifted);
        Assert.Empty(findings.Errors);
        Assert.Null(findings.FolderProblem);
        Assert.False(Directory.Exists(temp));
    }
}
