using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Launch;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class TuneRunnerTests
{
    private sealed class FakeWindowsVersion : IWindowsVersion
    {
        public string DisplayVersion => "24H2";
        public string FullBuild => "26100.1";
    }

    private sealed class FakeTuneBackend : ITuneBackend
    {
        public Func<Selection, TuneOptions, IProgress<TuneProgress>?, CancellationToken, TuneResult>? Handler { get; set; }

        public TuneResult Apply(Selection selection, TuneOptions options, IProgress<TuneProgress>? progress, CancellationToken ct) =>
            Handler != null
                ? Handler(selection, options, progress, ct)
                : new TuneResult("", RestorePointOutcome.Unavailable, new Dictionary<string, SwitchResult>(), false, null, []);
    }

    private sealed class FakeTuneBackendFactory(FakeTuneBackend backend) : ITuneBackendFactory
    {
        public int CreateCount { get; private set; }
        public ITuneBackend Create(JournalStore store)
        {
            CreateCount++;
            return backend;
        }
    }

    private sealed class DirectProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private static (TuneRunner runner, FakeTuneBackend backend, FakeTuneBackendFactory factory, AppStateStore store, FakeRegistry reg, FakeMeter meter, string tempDir) Create(
        FakeSession? session = null,
        FakeLockProbe? probe = null)
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var journalDir = Path.Combine(temp, "backups");
        var appDir = Path.Combine(temp, "app");

        var s = session ?? new FakeSession();
        var p = probe ?? new FakeLockProbe();
        var v = new FakeWindowsVersion();
        var meter = new FakeMeter();

        var ja = new JournalAccess(journalDir, s, _ => FolderState.Secure, _ => Directory.CreateDirectory(journalDir));
        var appStore = new AppStateStore(appDir, _ => FolderState.Secure, _ => Directory.CreateDirectory(appDir), s);

        var reg = new FakeRegistry();
        var afterRestart = new AfterRestartLaunch(reg);

        var backend = new FakeTuneBackend();
        var factory = new FakeTuneBackendFactory(backend);

        var runner = new TuneRunner(
            factory,
            s,
            v,
            p,
            ja,
            meter,
            appStore,
            afterRestart,
            @"C:\app\nitros.exe",
            _ => { });

        return (runner, backend, factory, appStore, reg, meter, temp);
    }

    [Fact]
    public async Task TuneRunner_RefusedSessionCreatesNothing()
    {
        var badSession = new FakeSession(processSid: "S-1-5-21-1", consoleSid: "S-1-5-21-2"); // mismatch
        var (runner, _, factory, _, _, meter, temp) = Create(session: badSession);

        try
        {
            var repo = TestCatalog.LoadRepo();
            var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

            var res = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);

            Assert.True(res.Refused);
            Assert.Equal("NitrOS is running as a different user than the one signed in, so it would change the wrong account's settings.", res.RefusedReason);
            Assert.Equal(0, factory.CreateCount);
            Assert.Equal(0, meter.ReadCalls);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task TuneRunner_BusyLockRefuses()
    {
        var probe = new FakeLockProbe { IsInProgress = true };
        var (runner, _, factory, _, _, _, temp) = Create(probe: probe);

        try
        {
            var repo = TestCatalog.LoadRepo();
            var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

            var res = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);

            Assert.True(res.Refused);
            Assert.Equal(TuneEngine.BusyReason, res.RefusedReason);
            Assert.Equal(0, factory.CreateCount);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task TuneRunner_MapsResultAndMarksHealthPending()
    {
        var (runner, backend, _, store, reg, _, temp) = Create();

        try
        {
            var repo = TestCatalog.LoadRepo();
            var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

            backend.Handler = (s, o, p, ct) => new TuneResult(
                JournalPath: Path.Combine(temp, "backups", "20261028-120000.json"),
                RestorePoint: RestorePointOutcome.Created,
                Switches: new Dictionary<string, SwitchResult>
                {
                    ["telemetry-off"] = SwitchResult.Applied,
                    ["widgets-off"] = SwitchResult.Failed
                },
                Refused: false,
                RefusedReason: null,
                MayBeChanged: ["edge-startup-off"])
            {
                Problems = [("widgets-off", "permission denied")]
            };

            var res = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);

            Assert.False(res.Refused);
            Assert.Equal(SwitchOutcome.Applied, res.Switches["telemetry-off"]);
            Assert.Equal(SwitchOutcome.Failed, res.Switches["widgets-off"]);
            Assert.Single(res.Problems);
            Assert.Equal("widgets-off", res.Problems[0].SwitchId);
            Assert.Contains("edge-startup-off", res.MayBeChanged);
            Assert.True(res.NeedsRestart);

            var state = store.Read();
            Assert.NotNull(state.Health);
            Assert.Equal("20261028-120000", state.Health.RunId);

            var regVal = reg.GetValue(LiveHive.LocalMachine, AfterRestartLaunch.Key, AfterRestartLaunch.ValueName, RegView.Default);
            Assert.NotNull(regVal);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task TuneRunner_NoRestorePointOffersRetry()
    {
        var (runner, backend, _, _, _, _, temp) = Create();

        try
        {
            var repo = TestCatalog.LoadRepo();
            var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

            backend.Handler = (s, o, p, ct) => new TuneResult(
                JournalPath: "",
                RestorePoint: RestorePointOutcome.Unavailable,
                Switches: new Dictionary<string, SwitchResult>(),
                Refused: true,
                RefusedReason: TuneEngine.NoRestorePointReason,
                MayBeChanged: []);

            var res = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);

            Assert.True(res.Refused);
            Assert.True(res.CanRetryWithoutRestorePoint);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task TuneRunner_ProgressLogsEachSwitchOnce()
    {
        var (runner, backend, _, _, _, _, temp) = Create();

        try
        {
            var repo = TestCatalog.LoadRepo();
            var sel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Tune, sel, CardId.Recommended, false, null);

            var sw1 = sel.On.First();
            var sw2 = sel.On.Skip(1).First();

            backend.Handler = (s, o, progress, ct) =>
            {
                progress?.Report(new TuneProgress(sw1, 1, 3));
                progress?.Report(new TuneProgress(sw1, 2, 3));
                progress?.Report(new TuneProgress(sw2, 3, 3));
                return new TuneResult("", RestorePointOutcome.Unavailable, new Dictionary<string, SwitchResult>(), false, null, []);
            };

            var reported = new List<RunProgress>();
            var prog = new DirectProgress<RunProgress>(reported.Add);

            await runner.RunAsync(req, prog, CancellationToken.None);

            Assert.Equal(3, reported.Count);
            Assert.NotNull(reported[0].LogLine);
            Assert.StartsWith("Changing: ", reported[0].LogLine!);
            Assert.Null(reported[1].LogLine);
            Assert.NotNull(reported[2].LogLine);
            Assert.StartsWith("Changing: ", reported[2].LogLine!);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
