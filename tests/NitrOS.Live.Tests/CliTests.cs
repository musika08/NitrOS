using NitrOS.Cli;
using NitrOS.Live;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests;

public class CliTests : IDisposable
{
    private sealed record Device(bool HasBattery, int MaxTouches, string Manufacturer, string Model) : IDeviceFacts;
    private sealed record Session(string ProcessUserSid, string? ConsoleUserSid, bool IsElevated, int WindowsBuild) : ISessionFacts;

    private sealed class Probe : IHealthProbe
    {
        public bool IsProcessRunning(string processName) => true;
        public bool IsServiceRunning(string serviceName) => true;
        public bool IsPackageInstalled(string packageName) => true;
        public string? HttpGet(string url) => HealthCheck.ConnectTestBody;
    }

    private readonly string _journals = Path.Combine(Path.GetTempPath(), "nitros-cli-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSystems _fake = new();
    private int _systemsCalls;

    public void Dispose()
    {
        if (Directory.Exists(_journals)) Directory.Delete(_journals, true);
    }

    private static string RepoCatalog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NitrOS.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("repo root"), "catalog");
    }

    private CliEnvironment Env(Func<LiveSystems>? systems = null, ISessionFacts? session = null) => new(
        systems ?? (() => { _systemsCalls++; return _fake.ToLive(); }),
        new Device(false, 0, "Test", "Desk"),
        session ?? new Session("S-1-5-21-1", "S-1-5-21-1", true, 26200),
        new Probe(),
        RepoCatalog(),
        _journals);

    private static (int Code, string Text) Run(CliEnvironment env, params string[] args)
    {
        var w = new StringWriter();
        var code = CliApp.Run(args, w, env);
        return (code, w.ToString());
    }

    [Fact]
    public void Plan_PrintsActionsAndChangesNothing()
    {
        var env = Env(systems: () => throw new InvalidOperationException("plan must never touch the systems"));
        var (code, text) = Run(env, "plan", "--card", "gamer");
        Assert.Equal(0, code);
        Assert.Contains("game-mode-on", text);
        Assert.Contains("registry.set", text);
        Assert.False(Directory.Exists(_journals));
    }

    [Fact]
    public void Plan_ListsDroppedIdsWithReasons()
    {
        var env = Env();
        var env2 = env with { Device = new Device(true, 10, "ASUS", "RC73XA") };
        var catalog = NitrOS.Core.Loading.CatalogLoader.Load(RepoCatalog()).Catalog!;
        var skipped = catalog.Switches.First(s => s.SkipOn.Count > 0).Id;
        var (code, text) = Run(env2, "plan", "--ids", $"{skipped},game-mode-on");
        Assert.Equal(0, code);
        Assert.Contains("Dropped", text);
        Assert.Contains(skipped, text);
        Assert.Contains("Kept on this device", text);
    }

    [Fact]
    public void Apply_RefusesWithoutYes()
    {
        var (code, text) = Run(Env(), "apply", "--card", "gamer");
        Assert.Equal(1, code);
        Assert.Contains("--yes", text);
        Assert.Equal(0, _systemsCalls);
        Assert.Empty(_fake.Registry.Values);
        Assert.False(Directory.Exists(_journals));
    }

    [Fact]
    public void Apply_RefusesWhenSessionGuardFails()
    {
        var env = Env(session: new Session("S-1-5-21-1", "S-1-5-21-1", false, 26200));
        var (code, text) = Run(env, "apply", "--card", "gamer", "--yes");
        Assert.Equal(1, code);
        Assert.Contains("administrator", text);
        Assert.Equal(0, _systemsCalls);
        Assert.Empty(_fake.Registry.Values);
    }

    [Fact]
    public void Undo_RefusesWithoutYes()
    {
        var (code, text) = Run(Env(), "undo", "some-journal");
        Assert.Equal(1, code);
        Assert.Contains("--yes", text);
        Assert.Equal(0, _systemsCalls);
    }

    [Fact]
    public void Undo_RefusesWhenSessionGuardFails()
    {
        var env = Env(session: new Session("S-1", "S-2", true, 26200));
        var (code, _) = Run(env, "undo", "some-journal", "--yes");
        Assert.Equal(1, code);
        Assert.Equal(0, _systemsCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("frobnicate")]
    [InlineData("plan")]
    [InlineData("plan --card nonsense")]
    [InlineData("plan --card gamer --bogus")]
    [InlineData("plan --card")]
    [InlineData("plan --ids does-not-exist")]
    [InlineData("apply --yes")]
    [InlineData("undo --yes")]
    [InlineData("drift")]
    [InlineData("health extra")]
    public void BadArguments_Exit2(string line)
    {
        var args = line.Length == 0 ? [] : line.Split(' ');
        var (code, _) = Run(Env(), args);
        Assert.Equal(2, code);
        Assert.Equal(0, _systemsCalls);
    }

    [Fact]
    public void Detect_PrintsProfileWithoutSystems()
    {
        var (code, text) = Run(Env(systems: () => throw new InvalidOperationException()), "detect");
        Assert.Equal(0, code);
        Assert.Contains("Battery: no", text);
        Assert.Contains("Handheld: no", text);
    }

    [Fact]
    public void Journals_EmptyWhenFolderMissing()
    {
        var (code, text) = Run(Env(), "journals");
        Assert.Equal(0, code);
        Assert.Contains("No journals", text);
        Assert.False(Directory.Exists(_journals));
    }

    [Fact]
    public void Apply_Drift_Undo_RoundTripOnFakes()
    {
        var env = Env();
        var (aCode, aText) = Run(env, "apply", "--ids", "game-mode-on", "--yes");
        Assert.Equal(0, aCode);
        Assert.NotEmpty(_fake.Registry.Values);

        var (jCode, jText) = Run(env, "journals");
        Assert.Equal(0, jCode);
        var path = Directory.GetFiles(_journals, "*.json").Single();
        Assert.Contains(Path.GetFileNameWithoutExtension(path), jText);

        var (dCode, dText) = Run(env, "drift", path);
        Assert.Equal(0, dCode);
        Assert.Contains("No drift", dText);

        var (hCode, _) = Run(env, "health");
        Assert.Equal(0, hCode);

        var (uCode, uText) = Run(env, "undo", Path.GetFileNameWithoutExtension(path), "--yes");
        Assert.Equal(0, uCode);
        Assert.Contains("game-mode-on", uText);
    }

    /// <summary>Read-only: plan never calls the systems factory, so nothing on this PC is changed and %ProgramData%\NitrOS is not created.</summary>
    [Fact]
    public void Plan_OnThisPcAgainstRealCatalog()
    {
        var real = new CliEnvironment(
            () => throw new InvalidOperationException("plan must never touch the systems"),
            new WinDeviceFacts(), new WinSessionFacts(), new WinHealthProbe(), RepoCatalog(), _journals);
        var (code, text) = Run(real, "plan", "--card", "gamer");
        Assert.Equal(0, code);
        Assert.Contains("Plan", text);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "nitros-plan-gamer.txt"), text);
    }
}
