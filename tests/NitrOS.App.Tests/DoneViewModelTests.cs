using NitrOS.App.Logic;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;

namespace NitrOS.App.Tests;

public sealed class DoneViewModelTests
{
    private sealed class FakeRestarter : IRestarter
    {
        public int RestartCount { get; private set; }
        public void Restart() => RestartCount++;
    }

    private sealed class FakeXboxRestorer(bool isAvailable = true) : IXboxRestorer
    {
        public bool IsAvailable => isAvailable;
        public string? UnavailableReason => isAvailable ? null : "Unavailable";
    }

    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public object? LastArgument { get; private set; }
        public void OnShown(object? argument) => LastArgument = argument;
    }

    private static (DoneViewModel vm, Navigator nav, FakeDialogs dialogs, FakeRestarter restarter, FakeMeter meter) Create(
        Catalog catalog,
        IXboxRestorer? xboxRestorer = null,
        RunGate? gate = null)
    {
        var g = gate ?? new RunGate();
        var nav = new Navigator(g);
        nav.Register(ScreenKeys.Working, () => new DummyScreen(ScreenKeys.Working));
        nav.Register(ScreenKeys.Start, () => new DummyScreen(ScreenKeys.Start));

        var dialogs = new FakeDialogs();
        var restarter = new FakeRestarter();
        var meter = new FakeMeter();
        var rest = xboxRestorer ?? new FakeXboxRestorer(true);

        var vm = new DoneViewModel(catalog, nav, dialogs, restarter, rest, meter, g);
        return (vm, nav, dialogs, restarter, meter);
    }

    [Fact]
    public void Done_ShowsProblemsAndMayBeChanged()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _, _, _) = Create(repo);

        var sw1 = repo.Switches[0];
        var sw2 = repo.Switches[1];

        var res = new RunResult(
            RunKind.Tune,
            false,
            null,
            false,
            new Dictionary<string, SwitchOutcome> { [sw1.Id] = SwitchOutcome.Applied },
            MayBeChanged: [sw2.Id],
            Problems: [(sw1.Id, "something failed")]);

        vm.OnShown(res);

        Assert.Single(vm.Problems);
        Assert.Equal($"{sw1.Name}: something failed", vm.Problems[0]);

        Assert.Single(vm.MayBeChanged);
        Assert.Equal(sw2.Name, vm.MayBeChanged[0]);
        Assert.Equal(Texts.MayBeChangedHeading, vm.MayBeChangedHeading);
    }

    [Fact]
    public void Done_BeforeAfterSaysAvailableToWindows()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _, _, meter) = Create(repo);

        meter.CurrentMemory = new MemoryInfo(16384, 15974, 6000);

        var before = new MachineSnapshot(6144, 182, DateTimeOffset.Now.AddMinutes(-5));
        var after = new MachineSnapshot(5427, 141, DateTimeOffset.Now);

        var res = new RunResult(
            RunKind.Tune,
            false,
            null,
            false,
            new Dictionary<string, SwitchOutcome>(),
            [],
            [])
        {
            Before = before,
            After = after
        };

        vm.OnShown(res);

        Assert.NotNull(vm.BeforeAfter);
        Assert.Contains("available to Windows", vm.BeforeAfter);
        Assert.Contains("Memory in use: 6.0 GB before, 5.3 GB now", vm.BeforeAfter);
        Assert.Contains("Processes: 182 before, 141 now.", vm.BeforeAfter);
        Assert.Contains("Measured on this PC just now. The numbers settle after a restart; NitrOS measures again then.", vm.BeforeAfter);
    }

    [Fact]
    public async Task Done_RestartAsksFirst()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, dialogs, restarter, _) = Create(repo);

        var res = new RunResult(
            RunKind.Tune,
            false,
            null,
            false,
            new Dictionary<string, SwitchOutcome>(),
            [],
            [])
        {
            NeedsRestart = true
        };

        vm.OnShown(res);
        Assert.True(vm.Restart.CanExecute(null));

        // Answering no -> not restarted
        dialogs.NextConfirmResult = false;
        await vm.Restart.ExecuteAsync(null);
        Assert.Equal(0, restarter.RestartCount);

        // Answering yes -> restarted once
        dialogs.NextConfirmResult = true;
        await vm.Restart.ExecuteAsync(null);
        Assert.Equal(1, restarter.RestartCount);
    }

    [Fact]
    public async Task Done_RetryWithoutRestorePointOnlyWhenOffered()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, nav, dialogs, _, _) = Create(repo);

        var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

        var refusedWithoutRetry = new RunResult(
            RunKind.Tune,
            true,
            "Busy",
            false,
            new Dictionary<string, SwitchOutcome>(),
            [],
            [])
        {
            CanRetryWithoutRestorePoint = false,
            Request = req
        };

        vm.OnShown(refusedWithoutRetry);
        Assert.False(vm.CanRetryWithoutRestorePoint);
        Assert.False(vm.RetryWithoutRestorePoint.CanExecute(null));

        var refusedWithRetry = new RunResult(
            RunKind.Tune,
            true,
            "No restore point",
            false,
            new Dictionary<string, SwitchOutcome>(),
            [],
            [])
        {
            CanRetryWithoutRestorePoint = true,
            Request = req
        };

        vm.OnShown(refusedWithRetry);
        Assert.True(vm.CanRetryWithoutRestorePoint);
        Assert.True(vm.RetryWithoutRestorePoint.CanExecute(null));

        dialogs.NextConfirmResult = true;
        await vm.RetryWithoutRestorePoint.ExecuteAsync(null);
        Assert.Equal(ScreenKeys.Working, nav.Current?.Key);
    }

    [Fact]
    public void Done_AddXboxBackHiddenUntilRestorerAvailable()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromCard(repo, CardId.MaxLean, TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.Tune, sel, CardId.MaxLean, false, null);

        var res = new RunResult(
            RunKind.Tune,
            false,
            null,
            false,
            new Dictionary<string, SwitchOutcome>(),
            [],
            [])
        {
            Request = req
        };

        // Hidden with UnprovenXboxRestorer
        var unproven = new UnprovenXboxRestorer();
        var (vmUnproven, _, _, _, _) = Create(repo, unproven);
        vmUnproven.OnShown(res);
        Assert.False(vmUnproven.ShowAddXboxBack);

        // Shown with available restorer
        var available = new FakeXboxRestorer(true);
        var (vmAvailable, _, _, _, _) = Create(repo, available);
        vmAvailable.OnShown(res);
        Assert.True(vmAvailable.ShowAddXboxBack);
    }

    [Fact]
    public void Done_IsoResultShowsPathAndVmReminder()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _, _, _) = Create(repo);

        var res = new RunResult(
            RunKind.Iso,
            false,
            null,
            false,
            new Dictionary<string, SwitchOutcome>(),
            [],
            [])
        {
            OutputPath = @"D:\Iso\nitros-slim.iso"
        };

        vm.OnShown(res);

        Assert.Equal("Your slim ISO is ready.", vm.Headline);
        Assert.NotNull(vm.IsoLine);
        Assert.Equal(@"Your ISO is at D:\Iso\nitros-slim.iso. Test it in a virtual machine before you install it.", vm.IsoLine);
    }
}
