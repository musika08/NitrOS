using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Handlers;

public class RegistryDeleteHandlerTests
{
    private const string Key = "SOFTWARE\\NitrOS\\Test";
    private static readonly RegistryDelete ValueAction = new("HKCU", Key, "Flag");
    private static readonly RegistryDelete KeyAction = new("HKCU", Key);
    private static readonly RegValue Val = new(RegistryKind.String, "x");

    private static IActionHandler Handler(SwitchAction a) => NitrOS.Live.Handlers.Handlers.For(a);

    private static FakeSystems WithTree()
    {
        var f = new FakeSystems();
        f.Registry.SetValue(LiveHive.CurrentUser, Key, "Flag", Val, RegView.Default);
        f.Registry.SetValue(LiveHive.CurrentUser, Key, "Other", new RegValue(RegistryKind.Dword, "2"), RegView.Default);
        f.Registry.SetValue(LiveHive.CurrentUser, Key + "\\Sub", "Deep", Val, RegView.Default);
        return f;
    }

    private static RegValue? Get(FakeSystems f, string key, string name) =>
        f.Registry.GetValue(LiveHive.CurrentUser, key, name, RegView.Default);

    [Fact]
    public void RegistryDelete_ReadsPrior()
    {
        var f = WithTree();
        Assert.Equal(new RegistryPrior(Val), Handler(ValueAction).Read(ValueAction, f.ToLive()));

        var whole = Assert.IsType<RegistryTreePrior>(Handler(KeyAction).Read(KeyAction, f.ToLive()));
        Assert.NotNull(whole.Tree);
        Assert.Equal(Key, whole.Tree!.Key);
        Assert.Equal(2, whole.Tree.Values.Count);
        Assert.Single(whole.Tree.SubKeys);
    }

    [Fact]
    public void RegistryDelete_Applies()
    {
        var f = WithTree();
        Assert.Equal(ActionOutcome.Applied, Handler(ValueAction).Apply(ValueAction, f.ToLive()).Outcome);
        Assert.Null(Get(f, Key, "Flag"));
        Assert.NotNull(Get(f, Key, "Other"));

        Assert.Equal(ActionOutcome.Applied, Handler(KeyAction).Apply(KeyAction, f.ToLive()).Outcome);
        Assert.False(f.Registry.KeyExists(LiveHive.CurrentUser, Key, RegView.Default));
        Assert.False(f.Registry.KeyExists(LiveHive.CurrentUser, Key + "\\Sub", RegView.Default));
    }

    [Fact]
    public void RegistryDelete_UndoRestoresPrior()
    {
        var f = WithTree();
        var h = Handler(ValueAction);
        var prior = h.Read(ValueAction, f.ToLive());
        h.Apply(ValueAction, f.ToLive());
        h.Undo(ValueAction, prior, f.ToLive());
        Assert.Equal(Val, Get(f, Key, "Flag"));
    }

    [Fact]
    public void RegistryDelete_UndoRestoresWholeKey()
    {
        var f = WithTree();
        var h = Handler(KeyAction);
        var prior = h.Read(KeyAction, f.ToLive());
        h.Apply(KeyAction, f.ToLive());
        h.Undo(KeyAction, prior, f.ToLive());
        Assert.Equal(Val, Get(f, Key, "Flag"));
        Assert.Equal(Val, Get(f, Key + "\\Sub", "Deep"));
    }

    [Fact]
    public void RegistryDelete_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        Assert.IsType<MissingPrior>(Handler(ValueAction).Read(ValueAction, f.ToLive()));
        Assert.IsType<MissingPrior>(Handler(KeyAction).Read(KeyAction, f.ToLive()));
        Assert.Equal(ActionOutcome.Skipped, Handler(ValueAction).Apply(ValueAction, f.ToLive()).Outcome);
        var skip = Handler(KeyAction).Apply(KeyAction, f.ToLive());
        Assert.Equal(ActionOutcome.Skipped, skip.Outcome);
        Assert.Equal("target not found", skip.Detail);
        // Key present but value absent is also a missing target.
        f.Registry.SetValue(LiveHive.CurrentUser, Key, "Other", Val, RegView.Default);
        Assert.IsType<MissingPrior>(Handler(ValueAction).Read(ValueAction, f.ToLive()));
        Assert.Equal(ActionOutcome.Skipped, Handler(ValueAction).Apply(ValueAction, f.ToLive()).Outcome);
    }

    [Fact]
    public void RegistryDelete_UndoOfMissingPriorDoesNothing()
    {
        var f = new FakeSystems();
        Handler(KeyAction).Undo(KeyAction, new MissingPrior(), f.ToLive());
        Assert.False(f.Registry.KeyExists(LiveHive.CurrentUser, Key, RegView.Default));
    }

    [Fact]
    public void RegistryDelete_IsInEffectAfterApply()
    {
        foreach (var a in new[] { ValueAction, KeyAction })
        {
            var f = WithTree();
            var h = Handler(a);
            Assert.False(h.IsInEffect(a, f.ToLive()));
            var prior = h.Read(a, f.ToLive());
            h.Apply(a, f.ToLive());
            Assert.True(h.IsInEffect(a, f.ToLive()));
            h.Undo(a, prior, f.ToLive());
            Assert.False(h.IsInEffect(a, f.ToLive()));
        }
    }

    [Fact]
    public void RegistryDelete_NamedValueCountsAsDeletedWhenItsKeyIsGone()
    {
        var f = WithTree();
        f.Registry.DeleteKey(LiveHive.CurrentUser, Key, RegView.Default);
        Assert.True(Handler(ValueAction).IsInEffect(ValueAction, f.ToLive()));
    }
}
