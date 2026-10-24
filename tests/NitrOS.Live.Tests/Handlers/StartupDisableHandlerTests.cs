using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Handlers;

public class StartupDisableHandlerTests
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string Approved = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\";
    private const string Enabled = "020000000000000000000000";
    private const string Disabled = "030000000000000000000000";
    private const string AppModel = "Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\AppModel\\SystemAppData";

    private static IActionHandler Handler(SwitchAction a) => NitrOS.Live.Handlers.Handlers.For(a);
    private static StartupDisable RunAction(string name = "Foo", string hive = "HKCU") => new(StartupSource.Run, name, hive);

    private static void Run(FakeSystems f, LiveHive h, string name, RegView v = RegView.Default) =>
        f.Registry.SetValue(h, RunKey, name, new RegValue(RegistryKind.String, "\"C:\\x.exe\""), v);

    private static void Approve(FakeSystems f, LiveHive h, string sub, string name, string hex) =>
        f.Registry.SetValue(h, Approved + sub, name, new RegValue(RegistryKind.Binary, hex), RegView.Default);

    private static string? Hex(FakeSystems f, LiveHive h, string sub, string name) =>
        f.Registry.GetValue(h, Approved + sub, name, RegView.Default)?.Data;

    private static void AppxState(FakeSystems f, string pkg, string task, string state)
    {
        f.Registry.ImportKey(LiveHive.CurrentUser, new RegTree(AppModel, [], []), RegView.Default);
        f.Registry.ImportKey(LiveHive.CurrentUser, new RegTree(AppModel + "\\" + pkg, [], []), RegView.Default);
        f.Registry.SetValue(LiveHive.CurrentUser, AppModel + "\\" + pkg + "\\" + task, "State",
            new RegValue(RegistryKind.Dword, state), RegView.Default);
    }

    private static string? State(FakeSystems f, string pkg, string task) =>
        f.Registry.GetValue(LiveHive.CurrentUser, AppModel + "\\" + pkg + "\\" + task, "State", RegView.Default)?.Data;

    [Fact]
    public void StartupDisable_ReadsPrior()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.CurrentUser, "Foo");
        Approve(f, LiveHive.CurrentUser, "Run", "Foo", Enabled);
        var a = RunAction();
        var prior = Assert.IsType<StartupPrior>(Handler(a).Read(a, f.ToLive()));
        var e = Assert.Single(prior.Entries);
        Assert.Equal(Enabled, e.Data);
        Assert.Contains("Foo", e.Location);
    }

    [Fact]
    public void StartupDisable_Applies()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.CurrentUser, "Foo");
        Approve(f, LiveHive.CurrentUser, "Run", "Foo", Enabled);
        var a = RunAction();
        Assert.Equal(ActionOutcome.Applied, Handler(a).Apply(a, f.ToLive()).Outcome);
        Assert.Equal(Disabled, Hex(f, LiveHive.CurrentUser, "Run", "Foo"));
        Assert.Equal(RegistryKind.Binary, f.Registry.GetValue(LiveHive.CurrentUser, Approved + "Run", "Foo", RegView.Default)!.Kind);
    }

    [Fact]
    public void StartupDisable_WritesFullValueWhenApprovedEntryAbsent()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.CurrentUser, "Foo");
        var a = RunAction();
        var h = Handler(a);
        var prior = h.Read(a, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, h.Apply(a, f.ToLive()).Outcome);
        Assert.Equal("030000000000000000000000", Hex(f, LiveHive.CurrentUser, "Run", "Foo"));
        h.Undo(a, prior, f.ToLive());
        Assert.Null(Hex(f, LiveHive.CurrentUser, "Run", "Foo"));
    }

    [Fact]
    public void StartupDisable_UndoRestoresPrior()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.CurrentUser, "Foo");
        Approve(f, LiveHive.CurrentUser, "Run", "Foo", Enabled);
        var a = RunAction();
        var h = Handler(a);
        var prior = h.Read(a, f.ToLive());
        h.Apply(a, f.ToLive());
        h.Undo(a, prior, f.ToLive());
        Assert.Equal(Enabled, Hex(f, LiveHive.CurrentUser, "Run", "Foo"));
    }

    [Fact]
    public void StartupDisable_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        foreach (var a in new SwitchAction[]
        {
            RunAction(), new StartupDisable(StartupSource.StartupFolder, "Nope.lnk"),
            new StartupDisable(StartupSource.AppxTask, "Pkg_1!Task"), new StartupDisable(StartupSource.Task, "\\Nope"),
        })
        {
            Assert.IsType<MissingPrior>(Handler(a).Read(a, f.ToLive()));
            var skip = Handler(a).Apply(a, f.ToLive());
            Assert.Equal(ActionOutcome.Skipped, skip.Outcome);
            Assert.Equal("no matching start-up entry", skip.Detail);
            Handler(a).Undo(a, new MissingPrior(), f.ToLive());
            Assert.False(Handler(a).IsInEffect(a, f.ToLive()));
        }
    }

    [Fact]
    public void StartupDisable_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.CurrentUser, "Foo");
        Approve(f, LiveHive.CurrentUser, "Run", "Foo", Enabled);
        var a = RunAction();
        var h = Handler(a);
        Assert.False(h.IsInEffect(a, f.ToLive()));
        var prior = h.Read(a, f.ToLive());
        h.Apply(a, f.ToLive());
        Assert.True(h.IsInEffect(a, f.ToLive()));
        h.Undo(a, prior, f.ToLive());
        Assert.False(h.IsInEffect(a, f.ToLive()));
    }

    [Fact]
    public void StartupDisable_NeverTouchesOtherFirstBytes()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.CurrentUser, "Foo");
        Approve(f, LiveHive.CurrentUser, "Run", "Foo", "060000000000000000000000");
        var a = RunAction();
        var h = Handler(a);
        Assert.IsType<MissingPrior>(h.Read(a, f.ToLive()));
        Assert.Equal(ActionOutcome.Skipped, h.Apply(a, f.ToLive()).Outcome);
        Assert.Equal("060000000000000000000000", Hex(f, LiveHive.CurrentUser, "Run", "Foo"));
        Assert.False(h.IsInEffect(a, f.ToLive()));
    }

    [Fact]
    public void StartupDisable_SetsOnlyFirstByte()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.CurrentUser, "Foo");
        Approve(f, LiveHive.CurrentUser, "Run", "Foo", "02AABBCCDD00000000000000");
        var a = RunAction();
        Handler(a).Apply(a, f.ToLive());
        Assert.Equal("03AABBCCDD00000000000000", Hex(f, LiveHive.CurrentUser, "Run", "Foo"));
    }

    [Fact]
    public void StartupDisable_HklmDefaultViewUsesRunKey()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.LocalMachine, "Foo");
        var a = RunAction(hive: "HKLM");
        Assert.Equal(ActionOutcome.Applied, Handler(a).Apply(a, f.ToLive()).Outcome);
        Assert.Equal(Disabled, Hex(f, LiveHive.LocalMachine, "Run", "Foo"));
        Assert.Null(Hex(f, LiveHive.LocalMachine, "Run32", "Foo"));
    }

    [Fact]
    public void StartupDisable_Hklm32BitViewUsesRun32Key()
    {
        var f = new FakeSystems();
        Run(f, LiveHive.LocalMachine, "Foo", RegView.Registry32);
        Approve(f, LiveHive.LocalMachine, "Run32", "Foo", Enabled);
        var a = RunAction(hive: "HKLM");
        var h = Handler(a);
        var prior = h.Read(a, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, h.Apply(a, f.ToLive()).Outcome);
        Assert.Equal(Disabled, Hex(f, LiveHive.LocalMachine, "Run32", "Foo"));
        Assert.Null(Hex(f, LiveHive.LocalMachine, "Run", "Foo"));
        h.Undo(a, prior, f.ToLive());
        Assert.Equal(Enabled, Hex(f, LiveHive.LocalMachine, "Run32", "Foo"));
    }

    [Fact]
    public void StartupDisable_WildcardChangesEveryMatchAndSavesEachOldValue()
    {
        var f = new FakeSystems();
        foreach (var n in new[] { "Vendor Helper", "Vendor Updater", "Other" })
        {
            Run(f, LiveHive.CurrentUser, n);
            Approve(f, LiveHive.CurrentUser, "Run", n, Enabled);
        }
        var a = RunAction("Vendor*");
        var h = Handler(a);
        var prior = Assert.IsType<StartupPrior>(h.Read(a, f.ToLive()));
        Assert.Equal(2, prior.Entries.Count);
        h.Apply(a, f.ToLive());
        Assert.Equal(Disabled, Hex(f, LiveHive.CurrentUser, "Run", "Vendor Helper"));
        Assert.Equal(Disabled, Hex(f, LiveHive.CurrentUser, "Run", "Vendor Updater"));
        Assert.Equal(Enabled, Hex(f, LiveHive.CurrentUser, "Run", "Other"));
        h.Undo(a, prior, f.ToLive());
        Assert.Equal(Enabled, Hex(f, LiveHive.CurrentUser, "Run", "Vendor Helper"));
        Assert.Equal(Enabled, Hex(f, LiveHive.CurrentUser, "Run", "Vendor Updater"));
    }

    [Fact]
    public void StartupDisable_StartupFolderUsesItsApprovedKey()
    {
        var f = new FakeSystems();
        Approve(f, LiveHive.CurrentUser, "StartupFolder", "Thing.lnk", Enabled);
        var a = new StartupDisable(StartupSource.StartupFolder, "Thing.lnk");
        var h = Handler(a);
        var prior = h.Read(a, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, h.Apply(a, f.ToLive()).Outcome);
        Assert.Equal(Disabled, Hex(f, LiveHive.CurrentUser, "StartupFolder", "Thing.lnk"));
        Assert.True(h.IsInEffect(a, f.ToLive()));
        h.Undo(a, prior, f.ToLive());
        Assert.Equal(Enabled, Hex(f, LiveHive.CurrentUser, "StartupFolder", "Thing.lnk"));
    }

    [Fact]
    public void StartupDisable_StartupFolderFileWithoutApprovedEntry()
    {
        var f = new FakeSystems();
        var path = Path.Combine(Environment.ExpandEnvironmentVariables("%APPDATA%"),
            "Microsoft\\Windows\\Start Menu\\Programs\\Startup", "New.lnk");
        f.Files.Existing.Add(path);
        var a = new StartupDisable(StartupSource.StartupFolder, "New.lnk");
        var h = Handler(a);
        var prior = h.Read(a, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, h.Apply(a, f.ToLive()).Outcome);
        Assert.Equal(Disabled, Hex(f, LiveHive.CurrentUser, "StartupFolder", "New.lnk"));
        h.Undo(a, prior, f.ToLive());
        Assert.Null(Hex(f, LiveHive.CurrentUser, "StartupFolder", "New.lnk"));
    }

    [Fact]
    public void StartupDisable_AppxTaskSetsStateToDisabledByUser()
    {
        var f = new FakeSystems();
        AppxState(f, "Pkg_8wekyb3d8bbwe", "StartupId", "2");
        var a = new StartupDisable(StartupSource.AppxTask, "Pkg_8wekyb3d8bbwe!StartupId");
        var h = Handler(a);
        var prior = h.Read(a, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, h.Apply(a, f.ToLive()).Outcome);
        Assert.Equal("1", State(f, "Pkg_8wekyb3d8bbwe", "StartupId"));
        Assert.True(h.IsInEffect(a, f.ToLive()));
        h.Undo(a, prior, f.ToLive());
        Assert.Equal("2", State(f, "Pkg_8wekyb3d8bbwe", "StartupId"));
        Assert.False(h.IsInEffect(a, f.ToLive()));
    }

    [Fact]
    public void StartupDisable_AppxTaskWildcardMatchesPackages()
    {
        var f = new FakeSystems();
        foreach (var pkg in new[] { "Vendor.A_x", "Vendor.B_x", "Other_x" }) AppxState(f, pkg, "T", "2");
        var a = new StartupDisable(StartupSource.AppxTask, "Vendor.*!T");
        Assert.Equal(ActionOutcome.Applied, Handler(a).Apply(a, f.ToLive()).Outcome);
        Assert.Equal("1", State(f, "Vendor.A_x", "T"));
        Assert.Equal("1", State(f, "Vendor.B_x", "T"));
        Assert.Equal("2", State(f, "Other_x", "T"));
    }

    [Fact]
    public void StartupDisable_TaskDelegatesToScheduler()
    {
        var f = new FakeSystems();
        f.Tasks.Enabled["\\Vendor\\Updater"] = true;
        var a = new StartupDisable(StartupSource.Task, "\\Vendor\\Updater");
        var h = Handler(a);
        var prior = h.Read(a, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, h.Apply(a, f.ToLive()).Outcome);
        Assert.False(f.Tasks.Enabled["\\Vendor\\Updater"]);
        Assert.True(h.IsInEffect(a, f.ToLive()));
        h.Undo(a, prior, f.ToLive());
        Assert.True(f.Tasks.Enabled["\\Vendor\\Updater"]);
        Assert.False(h.IsInEffect(a, f.ToLive()));
    }
}
