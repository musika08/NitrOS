using NitrOS.App.Logic;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;

namespace NitrOS.App.Tests;

public sealed class ReviewViewModelTests
{
    private sealed class RecordingRunner(RunKind kind = RunKind.Tune) : IRunner
    {
        public RunKind Kind => kind;
        public int CheckReadyCalls { get; private set; }
        public int RunAsyncCalls { get; private set; }
        public RunReadiness Readiness { get; set; } = new(true, null);

        public RunReadiness CheckReady(RunRequest request)
        {
            CheckReadyCalls++;
            return Readiness;
        }

        public Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct)
        {
            RunAsyncCalls++;
            return Task.FromResult(new RunResult(kind, false, null, false, new Dictionary<string, SwitchOutcome>(), [], []));
        }
    }

    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public void OnShown(object? argument) { }
    }

    private static (ReviewViewModel vm, RecordingRunner runner, FakeDialogs dialogs, Navigator nav, Draft draft) Create(
        Catalog catalog,
        TargetMode mode = TargetMode.TunePc,
        CardId? card = CardId.Recommended,
        FakeSession? session = null,
        FakeLockProbe? probe = null)
    {
        var gate = new RunGate();
        var nav = new Navigator(gate);
        nav.Register(ScreenKeys.Working, () => new DummyScreen(ScreenKeys.Working));
        nav.Register(ScreenKeys.Switches, () => new DummyScreen(ScreenKeys.Switches));

        var draft = new Draft(catalog, DeviceProfile.Desktop);
        draft.Start(mode);
        if (card is not null) draft.PickCard(card.Value);

        var runner = new RecordingRunner(mode == TargetMode.TunePc ? RunKind.Tune : RunKind.Iso);
        var isoRunner = new UnavailableIsoRunner();
        var runners = new RunnerSet([runner, isoRunner]);

        var s = session ?? new FakeSession();
        var p = probe ?? new FakeLockProbe();
        var d = new FakeDialogs();

        var vm = new ReviewViewModel(catalog, draft, runners, s, p, gate, d, nav);
        return (vm, runner, d, nav, draft);
    }

    [Fact]
    public void Review_NothingHappensUntilGo()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, runner, dialogs, nav, draft) = Create(repo);

        vm.OnShown(draft);

        // Reading every property
        _ = vm.Key;
        _ = vm.Changes;
        _ = vm.StopsWorking;
        _ = vm.DependencyWarnings;
        _ = vm.IrreversibleWarning;
        _ = vm.IrreversibleNames;
        _ = vm.ScriptNames;
        _ = vm.SafetyNote;
        _ = vm.GoUnavailableReason;
        _ = vm.Go.CanExecute(null);

        Assert.Equal(0, runner.RunAsyncCalls);
        Assert.Empty(dialogs.ConfirmRequests);
        Assert.Empty(dialogs.ShowRequests);
        Assert.Null(nav.Current?.Key);
    }

    [Fact]
    public async Task Review_GoAsksOnceForAllIrreversible()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, dialogs, nav, draft) = Create(repo, TargetMode.TunePc, CardId.Recommended);
        vm.OnShown(draft);

        var warnings = WarningEngine.For(draft.Selection!);
        var expectedIrreversibleCount = warnings.Count(w => w.Kind == WarningKind.Irreversible);
        Assert.True(expectedIrreversibleCount > 1);

        // Answering no -> no navigation
        dialogs.NextConfirmResult = false;
        await vm.Go.ExecuteAsync(null);

        Assert.Single(dialogs.ConfirmRequests);
        var req = dialogs.ConfirmRequests[0];
        Assert.Equal("Some changes can't be undone", req.Title);
        Assert.Equal("Go ahead", req.ConfirmText);
        Assert.Equal(expectedIrreversibleCount, req.Items.Count);
        Assert.Null(nav.Current?.Key);

        // Answering yes -> navigates to Working
        dialogs.NextConfirmResult = true;
        await vm.Go.ExecuteAsync(null);
        Assert.Equal(ScreenKeys.Working, nav.Current?.Key);
    }

    [Fact]
    public void Review_GroupsIrreversibleIntoOneWarning()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _, _, draft) = Create(repo, TargetMode.TunePc, CardId.Recommended);
        vm.OnShown(draft);

        var warnings = WarningEngine.For(draft.Selection!);
        var expectedCount = warnings.Count(w => w.Kind == WarningKind.Irreversible);

        Assert.NotNull(vm.IrreversibleWarning);
        Assert.Equal($"{expectedCount} of these changes can't be undone without reinstalling.", vm.IrreversibleWarning);
    }

    [Fact]
    public void Review_IsoGoDisabledWithReason()
    {
        var repo = TestCatalog.LoadRepo();
        var gate = new RunGate();
        var nav = new Navigator(gate);
        var draft = new Draft(repo, DeviceProfile.Desktop);
        draft.Start(TargetMode.BuildIso);
        draft.PickCard(CardId.Recommended);

        var isoRunner = new UnavailableIsoRunner();
        var runners = new RunnerSet([isoRunner]);
        var session = new FakeSession();
        var probe = new FakeLockProbe();
        var dialogs = new FakeDialogs();

        var vm = new ReviewViewModel(repo, draft, runners, session, probe, gate, dialogs, nav);
        vm.OnShown(draft);

        Assert.False(vm.Go.CanExecute(null));
        Assert.Equal(Texts.IsoLater, vm.GoUnavailableReason);
    }

    [Fact]
    public void Review_GoDisabledWhileLockHeldElsewhere()
    {
        var repo = TestCatalog.LoadRepo();
        var probe = new FakeLockProbe { IsInProgress = true };
        var (vm, _, _, _, draft) = Create(repo, TargetMode.TunePc, CardId.Recommended, probe: probe);
        vm.OnShown(draft);

        Assert.False(vm.Go.CanExecute(null));
        Assert.Equal(TuneEngine.BusyReason, vm.GoUnavailableReason);
    }

    [Fact]
    public void Review_ListsScriptSwitches()
    {
        var scriptSwitch = new SwitchDef
        {
            Id = "custom-script",
            Name = "Custom Script Switch",
            Feature = "Script Feature",
            Description = "Description",
            Risk = Risk.Safe,
            Modes = [Mode.Live],
            Reversible = true,
            Cards = new CardDefaults
            {
                Recommended = CardDefault.Off,
                Gamer = CardDefault.Off,
                Streamer = CardDefault.Off,
                MaxLean = CardDefault.Off
            },
            Live = [new Script("Write-Host 'hello'")]
        };

        var group = new GroupFile("grp-1", "Group 1", [scriptSwitch]);
        var catalog = new Catalog([group], new CardMeasurements());

        var (vm, _, _, _, draft) = Create(catalog, TargetMode.TunePc, null);
        draft.UseIds(["custom-script"]);
        vm.OnShown(draft);

        Assert.Contains("Custom Script Switch", vm.ScriptNames);
    }
}
