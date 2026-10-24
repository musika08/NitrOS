using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Handlers;

public class LaunchAtLogonHandlerTests
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "NitrOS.LaunchAtLogon";
    private const string Exe = "C:\\Tools\\app.exe";
    private static readonly LaunchAtLogon Action = new(Exe, "--tray");

    private static IActionHandler Handler() => NitrOS.Live.Handlers.Handlers.For(Action);

    private static FakeSystems WithExe()
    {
        var f = new FakeSystems();
        f.Files.Existing.Add(Exe);
        return f;
    }

    private static RegValue? Current(FakeSystems f) =>
        f.Registry.GetValue(LiveHive.CurrentUser, RunKey, ValueName, RegView.Default);

    [Fact]
    public void LaunchAtLogon_ReadsPrior()
    {
        var f = WithExe();
        Assert.Equal(new LaunchPrior(null), Handler().Read(Action, f.ToLive()));
        var old = new RegValue(RegistryKind.String, "\"old.exe\"");
        f.Registry.SetValue(LiveHive.CurrentUser, RunKey, ValueName, old, RegView.Default);
        Assert.Equal(new LaunchPrior(old), Handler().Read(Action, f.ToLive()));
    }

    [Fact]
    public void LaunchAtLogon_Applies()
    {
        var f = WithExe();
        Assert.Equal(ActionOutcome.Applied, Handler().Apply(Action, f.ToLive()).Outcome);
        Assert.Equal(new RegValue(RegistryKind.String, "\"C:\\Tools\\app.exe\" --tray"), Current(f));
    }

    [Fact]
    public void LaunchAtLogon_AppliesWithoutArgsAndExpandsPath()
    {
        var f = new FakeSystems();
        Environment.SetEnvironmentVariable("NITROS_TEST_DIR", "C:\\Expanded");
        var a = new LaunchAtLogon("%NITROS_TEST_DIR%\\x.exe");
        f.Files.Existing.Add("C:\\Expanded\\x.exe");
        Assert.Equal(ActionOutcome.Applied, NitrOS.Live.Handlers.Handlers.For(a).Apply(a, f.ToLive()).Outcome);
        Assert.Equal("\"C:\\Expanded\\x.exe\"", Current(f)!.Data);
    }

    [Fact]
    public void LaunchAtLogon_UndoRestoresPrior()
    {
        var f = WithExe();
        var old = new RegValue(RegistryKind.String, "\"old.exe\"");
        f.Registry.SetValue(LiveHive.CurrentUser, RunKey, ValueName, old, RegView.Default);
        var h = Handler();
        var prior = h.Read(Action, f.ToLive());
        h.Apply(Action, f.ToLive());
        h.Undo(Action, prior, f.ToLive());
        Assert.Equal(old, Current(f));
    }

    [Fact]
    public void LaunchAtLogon_UndoRemovesValueThatDidNotExist()
    {
        var f = WithExe();
        var h = Handler();
        var prior = h.Read(Action, f.ToLive());
        h.Apply(Action, f.ToLive());
        h.Undo(Action, prior, f.ToLive());
        Assert.Null(Current(f));
    }

    [Fact]
    public void LaunchAtLogon_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        Assert.IsType<MissingPrior>(Handler().Read(Action, f.ToLive()));
        var skip = Handler().Apply(Action, f.ToLive());
        Assert.Equal(ActionOutcome.Skipped, skip.Outcome);
        Assert.Equal("program file not found", skip.Detail);
        Assert.Null(Current(f));
        Handler().Undo(Action, new MissingPrior(), f.ToLive());
        Assert.Null(Current(f));
    }

    [Fact]
    public void LaunchAtLogon_IsInEffectAfterApply()
    {
        var f = WithExe();
        var h = Handler();
        Assert.False(h.IsInEffect(Action, f.ToLive()));
        var prior = h.Read(Action, f.ToLive());
        h.Apply(Action, f.ToLive());
        Assert.True(h.IsInEffect(Action, f.ToLive()));
        h.Undo(Action, prior, f.ToLive());
        Assert.False(h.IsInEffect(Action, f.ToLive()));
    }
}
