using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;

namespace NitrOS.App.Tests;

public sealed class NavigatorTests
{
    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public object? ShownArgument { get; private set; }
        public int ShownCount { get; private set; }

        public void OnShown(object? argument)
        {
            ShownArgument = argument;
            ShownCount++;
        }
    }

    [Fact]
    public void Navigator_GoToUnregisteredThrows()
    {
        var nav = new Navigator(new RunGate());
        Assert.Throws<InvalidOperationException>(() => nav.GoTo(ScreenKeys.Cards));
    }

    [Fact]
    public void Navigator_BackReturnsPreviousInstance()
    {
        var nav = new Navigator(new RunGate());
        var start = new DummyScreen(ScreenKeys.Start);
        var cards = new DummyScreen(ScreenKeys.Cards);

        nav.Register(ScreenKeys.Start, () => start);
        nav.Register(ScreenKeys.Cards, () => cards);

        nav.GoTo(ScreenKeys.Start);
        nav.GoTo(ScreenKeys.Cards);
        Assert.Equal(ScreenKeys.Cards, nav.Current?.Key);
        Assert.True(nav.CanGoBack);

        nav.GoBack();
        Assert.Same(start, nav.Current);
        Assert.Equal(1, start.ShownCount); // GoBack reuses previous instance without calling OnShown again
        Assert.False(nav.CanGoBack);
    }

    [Fact]
    public void Navigator_ResetClearsHistory()
    {
        var nav = new Navigator(new RunGate());
        nav.Register(ScreenKeys.Start, () => new DummyScreen(ScreenKeys.Start));
        nav.Register(ScreenKeys.Cards, () => new DummyScreen(ScreenKeys.Cards));

        nav.GoTo(ScreenKeys.Start);
        nav.GoTo(ScreenKeys.Cards);
        Assert.True(nav.CanGoBack);

        nav.ResetTo(ScreenKeys.Start);
        Assert.Equal(ScreenKeys.Start, nav.Current?.Key);
        Assert.False(nav.CanGoBack);
    }

    [Fact]
    public void Navigator_LockedWhileBusy()
    {
        var gate = new RunGate();
        var nav = new Navigator(gate);
        var start = new DummyScreen(ScreenKeys.Start);
        var cards = new DummyScreen(ScreenKeys.Cards);
        var done = new DummyScreen(ScreenKeys.Done);

        nav.Register(ScreenKeys.Start, () => start);
        nav.Register(ScreenKeys.Cards, () => cards);
        nav.Register(ScreenKeys.Done, () => done);

        nav.GoTo(ScreenKeys.Start);
        nav.GoTo(ScreenKeys.Cards);

        using (gate.TryEnter())
        {
            Assert.True(gate.IsBusy);
            nav.GoBack();
            Assert.Same(cards, nav.Current); // ignored while busy

            nav.GoTo(ScreenKeys.Start);
            Assert.Same(cards, nav.Current); // ignored while busy

            nav.ResetTo(ScreenKeys.Done);
            Assert.Same(done, nav.Current); // ResetTo(Done) works while busy
        }
    }
}
