using NitrOS.Cli;
using NitrOS.Live;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests;

[Collection(TuneLockCollection.Name)]
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

    private readonly string _out = Path.Combine(Path.GetTempPath(), "nitros-cli-out-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_journals)) Directory.Delete(_journals, true);
        if (Directory.Exists(_out)) Directory.Delete(_out, true);
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
    private sealed class FakeMemory(MemoryInfo info) : IMemoryFacts
    {
        public MemoryInfo Read() => info;
    }

    [Fact]
    public void Detect_PrintsMemoryLinesWhenSet()
    {
        var env = Env() with { Memory = new FakeMemory(new MemoryInfo(24576, 16384, 8192)) };
        var (code, text) = Run(env, "detect");
        Assert.Equal(0, code);
        Assert.Contains("Memory installed: 24576 MB", text);
        Assert.Contains("Memory available to Windows: 16384 MB", text);
        Assert.Contains("Reserved for hardware (mostly the GPU): 8192 MB", text);
    }

    [Fact]
    public void JournalCommands_RefuseWhenTheFolderCannotBeSecured()
    {
        var env = Env() with { SecureJournalDir = () => throw new UnauthorizedAccessException("not elevated") };
        foreach (var args in new[] { new[] { "journals" }, new[] { "drift", "x" }, new[] { "undo", "x", "--yes" } })
        {
            var (code, text) = Run(env, args);
            Assert.Equal(1, code);
            Assert.Contains("could not be secured", text);
        }
        Assert.Equal(0, _systemsCalls);
        // health still runs, without journals.
        var (hCode, hText) = Run(env, "health");
        Assert.Contains("journals not read", hText);
        Assert.Equal(0, hCode);
    }

    [Fact]
    public void JournalCommands_SecureFirstAndReportSkippedHardLinks()
    {
        var calls = 0;
        var env = Env() with { SecureJournalDir = () => { calls++; return [@"C:\x\linked.json"]; } };
        var (code, text) = Run(env, "journals");
        Assert.Equal(0, code);
        Assert.Equal(1, calls);
        Assert.Contains(@"Skipped (hard-linked file, left unchanged): C:\x\linked.json", text);
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
        // The output is kept only inside this test's own temp folder, which Dispose deletes.
        Directory.CreateDirectory(_out);
        File.WriteAllText(Path.Combine(_out, "nitros-plan-gamer.txt"), text);
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "nitros-plan-gamer.txt")));
    }

    [Fact]
    public void PlanCard_ListsCardSwitchesDroppedOnThisDevice()
    {
        // On a battery device the gamer card's high-performance-plan is left off, and plan says so.
        var (code, text) = Run(HandheldEnv(), "plan", "--card", "gamer");
        Assert.Equal(0, code);
        Assert.Contains("Dropped:", text);
        Assert.Contains("high-performance-plan: Kept on this device because it runs on battery.", text);
    }

    [Fact]
    public void Apply_PrintsBreaksWarningsBeforeRunningAndFailureDetailsAfter()
    {
        var catalog = NitrOS.Core.Loading.CatalogLoader.Load(RepoCatalog()).Catalog!;
        var breaking = catalog.Switches.First(s => s.Breaks.Count > 0 && s.SkipOn.Count == 0
            && s.Modes.Contains(NitrOS.Core.Model.Mode.Live));
        _fake.Registry.FailOn = "AutoGameModeEnabled"; // game-mode-on's only action fails

        var (code, text) = Run(Env(), "apply", "--ids", $"{breaking.Id},game-mode-on", "--yes");

        Assert.Equal(1, code);
        var warnings = text.IndexOf("Warnings:", StringComparison.Ordinal);
        Assert.True(warnings >= 0, text);
        Assert.Contains($"{breaking.Id}: {breaking.Breaks[0]}", text);
        Assert.True(warnings < text.IndexOf("Restore point:", StringComparison.Ordinal), text);
        Assert.Contains("game-mode-on: Failed", text);
        Assert.Contains("FakeRegistry failure on AutoGameModeEnabled", text);
        Assert.True(text.IndexOf("game-mode-on: Failed", StringComparison.Ordinal)
            < text.LastIndexOf("FakeRegistry failure on AutoGameModeEnabled", StringComparison.Ordinal), text);
    }

    [Fact]
    public void Undo_And_Drift_RefuseJournalOfAnotherAccount_Exit2()
    {
        var (aCode, _) = Run(Env(), "apply", "--ids", "game-mode-on", "--yes");
        Assert.Equal(0, aCode);
        var path = Directory.GetFiles(_journals, "*.json").Single();
        var other = Env(session: new Session("S-1-5-21-2", "S-1-5-21-2", true, 26200));

        var (uCode, uText) = Run(other, "undo", path, "--yes");
        Assert.Equal(2, uCode);
        Assert.Contains("This journal belongs to another account.", uText);
        Assert.NotEmpty(_fake.Registry.Values); // nothing was undone

        var (dCode, dText) = Run(other, "drift", path);
        Assert.Equal(2, dCode);
        Assert.Contains("This journal belongs to another account.", dText);
    }

    private CliEnvironment HandheldEnv() => Env() with { Device = new Device(true, 10, "ASUS", "RC73XA") };

    private static string SkippedOnHandheld() =>
        NitrOS.Core.Loading.CatalogLoader.Load(RepoCatalog()).Catalog!.Switches.First(s => s.SkipOn.Count > 0).Id;

    [Fact]
    public void Drift_CorruptJournal_PrintsErrorAndExits1()
    {
        Directory.CreateDirectory(_journals);
        File.WriteAllText(Path.Combine(_journals, "bad.json"), "{ not json");
        var (code, text) = Run(Env(), "drift", "bad");
        Assert.Equal(1, code);
        Assert.StartsWith("Error: ", text);
    }

    [Fact]
    public void Undo_CorruptJournal_PrintsErrorAndExits1()
    {
        Directory.CreateDirectory(_journals);
        File.WriteAllText(Path.Combine(_journals, "bad.json"), "{ not json");
        var (code, text) = Run(Env(), "undo", "bad", "--yes");
        Assert.Equal(1, code);
        Assert.Contains("Error: ", text);
    }

    [Fact]
    public void Apply_SystemsFactoryThrows_PrintsErrorAndExits1()
    {
        var env = Env(systems: () => throw new InvalidOperationException("boom"));
        var (code, text) = Run(env, "apply", "--ids", "game-mode-on", "--yes");
        Assert.Equal(1, code);
        Assert.Contains("Error: boom", text);
    }

    [Fact]
    public void Apply_PrintsDroppedIds()
    {
        var skipped = SkippedOnHandheld();
        var (code, text) = Run(HandheldEnv(), "apply", "--ids", $"{skipped},game-mode-on", "--yes");
        Assert.Equal(0, code);
        Assert.Contains("Dropped", text);
        Assert.Contains(skipped, text);
    }

    [Fact]
    public void Apply_EmptySelection_RefusesBeforeRestorePointOrJournal()
    {
        var (code, text) = Run(HandheldEnv(), "apply", "--ids", SkippedOnHandheld(), "--yes");
        Assert.Equal(1, code);
        Assert.Contains("Nothing to apply.", text);
        Assert.Empty(_fake.RestorePoint.Descriptions);
        Assert.Equal(0, _systemsCalls);
        Assert.False(Directory.Exists(_journals));
    }

    [Fact]
    public void Apply_Cancelled_StopsBeforeChangingAnything()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var w = new StringWriter();
        var code = CliApp.Run(["apply", "--ids", "game-mode-on", "--yes"], w, Env(), cts.Token);
        Assert.Equal(1, code);
        Assert.Contains("Stopped", w.ToString());
        Assert.Empty(_fake.Registry.Values);
    }

    [Fact]
    public void Apply_IdsMissingValue_Exit2AndAppliesNothing()
    {
        var (code, _) = Run(Env(), "apply", "--ids", "--yes");
        Assert.Equal(2, code);
        Assert.Equal(0, _systemsCalls);
        Assert.Empty(_fake.Registry.Values);
    }

    [Fact]
    public void AllowWithoutRestorePoint_OnlyChangesApply()
    {
        var (c1, t1) = Run(Env(), "plan", "--card", "gamer");
        var (c2, t2) = Run(Env(), "plan", "--card", "gamer", "--allow-without-restore-point");
        Assert.Equal(c1, c2);
        Assert.Equal(t1, t2);
        Assert.Equal(0, _systemsCalls);

        _fake.RestorePoint.Outcome = NitrOS.Live.Systems.RestorePointOutcome.Unavailable;
        var (refused, _) = Run(Env(), "apply", "--ids", "game-mode-on", "--yes");
        Assert.Equal(1, refused);
        Assert.Empty(_fake.Registry.Values);
        var (allowed, _) = Run(Env(), "apply", "--ids", "game-mode-on", "--yes", "--allow-without-restore-point");
        Assert.Equal(0, allowed);
    }

    [Fact]
    public void Card_IsCaseInsensitive()
    {
        var (code, _) = Run(Env(), "plan", "--card", "Gamer");
        Assert.Equal(0, code);
        var (code2, _) = Run(Env(), "plan", "--card", "MAX-LEAN");
        Assert.Equal(0, code2);
    }

    [Theory]
    [InlineData("undo")]
    [InlineData("drift")]
    public void Journal_OutsideBackupsFolder_IsRefusedWithExit2(string command)
    {
        var other = Path.Combine(Path.GetTempPath(), "nitros-other-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(other);
        try
        {
            var file = Path.Combine(other, "x.json");
            File.WriteAllText(file, "{}");
            var args = command == "undo" ? new[] { command, file, "--yes" } : [command, file];
            var (code, text) = Run(Env(), args);
            Assert.Equal(2, code);
            Assert.Contains("Journals are only read from the NitrOS backups folder.", text);
            Assert.Equal(0, _systemsCalls);
        }
        finally { Directory.Delete(other, true); }
    }

    [Fact]
    public void Journal_DotDotPathEscapingFolder_IsRefused()
    {
        Directory.CreateDirectory(_journals);
        var sneaky = Path.Combine(_journals, "..", "x.json");
        var (code, _) = Run(Env(), "drift", sneaky);
        Assert.Equal(2, code);
    }

    [Fact]
    public void JournalFolder_IsSecuredBeforeJournalIsRead()
    {
        var calls = 0;
        var env = Env() with { SecureJournalDir = () => { calls++; return []; } };
        Run(env, "drift", "missing");
        Assert.Equal(1, calls);
    }
}
