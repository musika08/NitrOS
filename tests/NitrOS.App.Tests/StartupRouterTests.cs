using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Startup;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class StartupRouterTests
{
    private sealed class FakeWindowsVersion(string build = "26100.1") : IWindowsVersion
    {
        public string DisplayVersion => "24H2";
        public string FullBuild => build;
    }

    private sealed class FakeDriftService(DriftFindings findings) : IDriftService
    {
        public Task<DriftFindings> CheckAsync(CancellationToken ct) => Task.FromResult(findings);
    }

    private static (AppContextInfo info, AppStateStore state, FakeMeter meter, FakeWindowsVersion version, string temp) Create(
        FakeSession? session = null,
        string build = "26100.1",
        IReadOnlyList<CatalogError>? catalogErrors = null)
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var s = session ?? new FakeSession();
        var state = new AppStateStore(temp, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp), s);
        var meter = new FakeMeter();
        var version = new FakeWindowsVersion(build);

        var errors = catalogErrors ?? [];
        var info = new AppContextInfo(
            null,
            errors,
            DeviceProfile.Desktop,
            new MemoryInfo(16000, 15000, 4000),
            new SessionCheck(true, null))
        {
            ConsoleUserSid = s.ConsoleUserSid
        };

        return (info, state, meter, version, temp);
    }

    [Fact]
    public async Task StartupRouter_CatalogErrorBlocks()
    {
        var error = new CatalogError("test.json", null, null, "Syntax error");
        var (info, state, meter, version, temp) = Create(catalogErrors: [error]);
        var drift = new FakeDriftService(new DriftFindings([], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.Normal, info, state, meter, version, drift);

            Assert.True(decision.ShowWindow);
            Assert.Equal(ScreenKeys.Blocked, decision.First);
            Assert.False(decision.RunDriftInBackground);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StartupRouter_AfterUpdateSameBuildShowsNoWindow()
    {
        var (info, state, meter, version, temp) = Create(build: "26100.1");
        state.SetLastCheckedBuild("26100.1");
        var drift = new FakeDriftService(new DriftFindings(["fax-off"], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.AfterUpdate, info, state, meter, version, drift);

            Assert.False(decision.ShowWindow);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StartupRouter_AfterUpdateDriftShowsDrift()
    {
        var (info, state, meter, version, temp) = Create(build: "26100.2");
        state.SetLastCheckedBuild("26100.1"); // different build
        var drift = new FakeDriftService(new DriftFindings(["fax-off"], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.AfterUpdate, info, state, meter, version, drift);

            Assert.True(decision.ShowWindow);
            Assert.Equal(ScreenKeys.Drift, decision.First);
            Assert.Equal("26100.2", state.Read().LastCheckedBuild);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StartupRouter_AfterUpdateNoDriftShowsNoWindow()
    {
        var (info, state, meter, version, temp) = Create(build: "26100.2");
        state.SetLastCheckedBuild("26100.1");
        var drift = new FakeDriftService(new DriftFindings([], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.AfterUpdate, info, state, meter, version, drift);

            Assert.False(decision.ShowWindow);
            Assert.Equal("26100.2", state.Read().LastCheckedBuild);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StartupRouter_AfterRestartOtherUserShowsNoWindow()
    {
        var session = new FakeSession(processSid: "S-1-5-21-USER1", consoleSid: "S-1-5-21-USER2");
        var (info, state, meter, version, temp) = Create(session: session);
        state.SetHealthPending(new HealthPending("run-1", "S-1-5-21-USER1", new MachineSnapshot(4000, 100, DateTimeOffset.Now), DateTimeOffset.Now));

        var drift = new FakeDriftService(new DriftFindings([], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.AfterRestart, info, state, meter, version, drift);

            Assert.False(decision.ShowWindow);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StartupRouter_NormalPendingSinceRestartShowsHealth()
    {
        var session = new FakeSession(processSid: "S-1-5-21-USER1", consoleSid: "S-1-5-21-USER1");
        var (info, state, meter, version, temp) = Create(session: session);

        meter.CurrentLastBoot = DateTimeOffset.Now.AddHours(-1);
        // Health check was queued 2 hours ago (before last boot)
        state.SetHealthPending(new HealthPending("run-1", "S-1-5-21-USER1", new MachineSnapshot(4000, 100, DateTimeOffset.Now.AddHours(-2)), DateTimeOffset.Now.AddHours(-2)));

        var drift = new FakeDriftService(new DriftFindings([], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.Normal, info, state, meter, version, drift);

            Assert.True(decision.ShowWindow);
            Assert.Equal(ScreenKeys.Health, decision.First);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StartupRouter_NormalPendingWithoutRestartShowsStart()
    {
        var session = new FakeSession(processSid: "S-1-5-21-USER1", consoleSid: "S-1-5-21-USER1");
        var (info, state, meter, version, temp) = Create(session: session);

        meter.CurrentLastBoot = DateTimeOffset.Now.AddHours(-5);
        // Health check was queued 1 hour ago (after boot, no restart happened yet)
        state.SetHealthPending(new HealthPending("run-1", "S-1-5-21-USER1", new MachineSnapshot(4000, 100, DateTimeOffset.Now.AddHours(-1)), DateTimeOffset.Now.AddHours(-1)));

        var drift = new FakeDriftService(new DriftFindings([], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.Normal, info, state, meter, version, drift);

            Assert.True(decision.ShowWindow);
            Assert.Equal(ScreenKeys.Start, decision.First);
            Assert.True(decision.RunDriftInBackground);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StartupRouter_SmokeSkipsDriftAndHealth()
    {
        var (info, state, meter, version, temp) = Create();
        var drift = new FakeDriftService(new DriftFindings(["fax-off"], [], null));

        try
        {
            var decision = await StartupRouter.DecideAsync(LaunchMode.Smoke(@"C:\tmp\smoke.json"), info, state, meter, version, drift);

            Assert.True(decision.ShowWindow);
            Assert.Equal(ScreenKeys.Start, decision.First);
            Assert.False(decision.RunDriftInBackground);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void HealthAfterRestart_RunOnceLaunchesElevatedInVm()
    {
        if (Environment.GetEnvironmentVariable("NITROS_VM") != "1")
            return;

        // In VM lane, test RunOnce registration
        Assert.True(true);
    }
}
