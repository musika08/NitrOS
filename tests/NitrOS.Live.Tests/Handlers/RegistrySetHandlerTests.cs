using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Handlers;

public class RegistrySetHandlerTests
{
    private const string Key = "SOFTWARE\\NitrOS\\Test";
    private static readonly RegistrySet Action = new("HKLM", Key, "Flag", RegistryKind.Dword, "1");

    private static IActionHandler Handler(SwitchAction? a = null) => NitrOS.Live.Handlers.Handlers.For(a ?? Action);

    private static RegValue? Current(FakeSystems f, LiveHive h = LiveHive.LocalMachine) =>
        f.Registry.GetValue(h, Key, "Flag", RegView.Default);

    [Fact]
    public void RegistrySet_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Registry.SetValue(LiveHive.LocalMachine, Key, "Flag", new RegValue(RegistryKind.Dword, "0"), RegView.Default);
        Assert.Equal(new RegistryPrior(new RegValue(RegistryKind.Dword, "0")), Handler().Read(Action, f.ToLive()));
    }

    [Fact]
    public void RegistrySet_ReadsPriorWhenValueAbsent()
    {
        var f = new FakeSystems();
        Assert.Equal(new RegistryPrior(null), Handler().Read(Action, f.ToLive()));
    }

    [Fact]
    public void RegistrySet_Applies()
    {
        var f = new FakeSystems();
        Assert.Equal(ActionOutcome.Applied, Handler().Apply(Action, f.ToLive()));
        Assert.Equal(new RegValue(RegistryKind.Dword, "1"), Current(f));
    }

    [Fact]
    public void RegistrySet_AppliesToCurrentUserHive()
    {
        var f = new FakeSystems();
        Handler().Apply(Action with { Hive = "HKCU" }, f.ToLive());
        Assert.NotNull(Current(f, LiveHive.CurrentUser));
        Assert.Null(Current(f, LiveHive.LocalMachine));
    }

    [Fact]
    public void RegistrySet_UndoRestoresPrior()
    {
        var f = new FakeSystems();
        var old = new RegValue(RegistryKind.Dword, "0");
        f.Registry.SetValue(LiveHive.LocalMachine, Key, "Flag", old, RegView.Default);
        var h = Handler();
        var prior = h.Read(Action, f.ToLive());
        h.Apply(Action, f.ToLive());
        h.Undo(Action, prior, f.ToLive());
        Assert.Equal(old, Current(f));
    }

    [Fact]
    public void RegistrySet_UndoDeletesValueThatDidNotExist()
    {
        var f = new FakeSystems();
        var h = Handler();
        var prior = h.Read(Action, f.ToLive());
        h.Apply(Action, f.ToLive());
        h.Undo(Action, prior, f.ToLive());
        Assert.Null(Current(f));
    }

    [Fact]
    public void RegistrySet_UndoOfMissingPriorDoesNothing()
    {
        var f = new FakeSystems();
        f.Registry.SetValue(LiveHive.LocalMachine, Key, "Flag", new RegValue(RegistryKind.Dword, "5"), RegView.Default);
        Handler().Undo(Action, new MissingPrior(), f.ToLive());
        Assert.Equal("5", Current(f)!.Data);
    }

    [Fact]
    public void RegistrySet_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        var h = Handler();
        Assert.False(h.IsInEffect(Action, f.ToLive()));
        var prior = h.Read(Action, f.ToLive());
        h.Apply(Action, f.ToLive());
        Assert.True(h.IsInEffect(Action, f.ToLive()));
        h.Undo(Action, prior, f.ToLive());
        Assert.False(h.IsInEffect(Action, f.ToLive()));
    }

    [Fact]
    public void RegistrySet_UnknownHiveThrows()
    {
        var f = new FakeSystems();
        Assert.Throws<ArgumentException>(() => Handler().Apply(Action with { Hive = "HKXX" }, f.ToLive()));
    }
}
