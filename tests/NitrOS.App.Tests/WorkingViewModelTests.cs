using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;

namespace NitrOS.App.Tests;

public sealed class WorkingViewModelTests
{
    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public object? LastArgument { get; private set; }
        public void OnShown(object? argument) => LastArgument = argument;
    }

    private sealed class TestRunner(Func<RunRequest, IProgress<RunProgress>, CancellationToken, Task<RunResult>> run) : IRunner
    {
        public RunKind Kind => RunKind.Tune;
        public RunReadiness CheckReady(RunRequest request) => new(true, null);
        public Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct) => run(request, progress, ct);
    }

    private static (WorkingViewModel vm, Navigator nav, RunGate gate, DummyScreen doneScreen) Create(
        Catalog catalog,
        IRunner runner,
        RunGate? gate = null)
    {
        var g = gate ?? new RunGate();
        var nav = new Navigator(g);
        var doneScreen = new DummyScreen(ScreenKeys.Done);
        nav.Register(ScreenKeys.Done, () => doneScreen);

        var runners = new RunnerSet([runner]);
        var vm = new WorkingViewModel(catalog, runners, g, nav);
        return (vm, nav, g, doneScreen);
    }

    [Fact]
    public async Task Working_ProgressAndLogFromRunner()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

        var runner = new TestRunner((r, progress, ct) =>
        {
            progress.Report(new RunProgress("sw1", 1, 3, "Line 1"));
            progress.Report(new RunProgress("sw2", 2, 3, "Line 2"));
            progress.Report(new RunProgress("sw3", 3, 3, "Line 3"));

            return Task.FromResult(new RunResult(
                RunKind.Tune,
                false,
                null,
                false,
                new Dictionary<string, SwitchOutcome>
                {
                    ["telemetry-off"] = SwitchOutcome.Applied
                },
                [],
                []));
        });

        var (vm, nav, _, doneScreen) = Create(repo, runner);
        vm.OnShown(req);
        await vm.CurrentRunTask!;

        Assert.Equal(100.0, vm.Percent);
        Assert.Contains("Line 1", vm.Log);
        Assert.Contains("Line 2", vm.Log);
        Assert.Contains("Line 3", vm.Log);
        // Also asserts summary line appended
        Assert.Contains(vm.Log, l => l.StartsWith("Done: "));
        Assert.Equal(ScreenKeys.Done, nav.Current?.Key);
    }

    [Fact]
    public async Task Working_CancelStopsAndShowsStoppingText()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

        var tcs = new TaskCompletionSource<bool>();
        var runner = new TestRunner(async (r, progress, ct) =>
        {
            tcs.SetResult(true);
            try
            {
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                // expected
            }

            return new RunResult(RunKind.Tune, false, null, true, new Dictionary<string, SwitchOutcome>(), [], []);
        });

        var (vm, _, _, _) = Create(repo, runner);
        vm.OnShown(req);

        await tcs.Task;
        vm.Cancel.Execute(null);

        Assert.Equal("Stopping after the current change. That switch is put back.", vm.Status);
        await vm.CurrentRunTask!;
    }

    [Fact]
    public async Task Working_GateHeldDuringRunAndReleasedAfter()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

        var started = new TaskCompletionSource<bool>();
        var proceed = new TaskCompletionSource<bool>();

        var runner = new TestRunner(async (r, progress, ct) =>
        {
            started.SetResult(true);
            await proceed.Task;
            return new RunResult(RunKind.Tune, false, null, false, new Dictionary<string, SwitchOutcome>(), [], []);
        });

        var (vm, _, gate, _) = Create(repo, runner);
        vm.OnShown(req);

        await started.Task;
        Assert.True(gate.IsBusy);

        proceed.SetResult(true);
        await vm.CurrentRunTask!;
        Assert.False(gate.IsBusy);
    }

    [Fact]
    public async Task Working_RunnerExceptionBecomesRefusedDone()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

        var runner = new TestRunner((r, progress, ct) =>
        {
            throw new InvalidOperationException("Catastrophic disk error");
        });

        var (vm, nav, _, doneScreen) = Create(repo, runner);
        vm.OnShown(req);
        await vm.CurrentRunTask!;

        Assert.Equal(ScreenKeys.Done, nav.Current?.Key);
        var result = Assert.IsType<RunResult>(doneScreen.LastArgument);
        Assert.True(result.Refused);
        Assert.Equal("Catastrophic disk error", result.RefusedReason);
    }

    [Fact]
    public void Working_SecondRunWhileBusyGoesToDoneRefused()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

        var runner = new TestRunner((r, progress, ct) => Task.FromResult(new RunResult(RunKind.Tune, false, null, false, new Dictionary<string, SwitchOutcome>(), [], [])));
        var gate = new RunGate();
        var lease = gate.TryEnter(); // hold gate externally
        Assert.NotNull(lease);

        var (vm, nav, _, doneScreen) = Create(repo, runner, gate);
        vm.OnShown(req);

        Assert.Equal(ScreenKeys.Done, nav.Current?.Key);
        var result = Assert.IsType<RunResult>(doneScreen.LastArgument);
        Assert.True(result.Refused);
        Assert.Equal(TuneEngine.BusyReason, result.RefusedReason);

        lease.Dispose();
    }
}
