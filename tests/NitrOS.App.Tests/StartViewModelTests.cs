using NitrOS.App.Logic;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Startup;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;

namespace NitrOS.App.Tests;

public sealed class StartViewModelTests
{
    private static (StartViewModel vm, Navigator nav, Draft draft) Create(
        bool sessionOk = true,
        string? sessionReason = null)
    {
        var nav = new Navigator(new RunGate());
        nav.Register(ScreenKeys.Cards, () => new DummyScreen(ScreenKeys.Cards));
        nav.Register(ScreenKeys.Undo, () => new DummyScreen(ScreenKeys.Undo));
        nav.Register(ScreenKeys.Myths, () => new DummyScreen(ScreenKeys.Myths));
        nav.Register(ScreenKeys.Settings, () => new DummyScreen(ScreenKeys.Settings));

        var catalog = new Catalog([], new CardMeasurements());
        var profile = new DeviceProfile(true, true, true);
        var draft = new Draft(catalog, profile);
        var session = new SessionCheck(sessionOk, sessionReason);
        var context = new AppContextInfo(catalog, [], profile, new MemoryInfo(16000, 15000, 4000), session);

        var vm = new StartViewModel(nav, draft, context);
        return (vm, nav, draft);
    }

    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public void OnShown(object? argument) { }
    }

    [Fact]
    public void Start_DeviceUpdatesDisabledUntilRegistered()
    {
        var (vm, nav, _) = Create();
        var entry = vm.Entries.First(e => e.Title == "Device updates");
        Assert.False(entry.Enabled);
        Assert.Equal(Texts.DeviceUpdatesLater, entry.DisabledReason);

        nav.Register(ScreenKeys.DeviceUpdates, () => new DummyScreen(ScreenKeys.DeviceUpdates));
        vm.OnShown(null);

        var refreshed = vm.Entries.First(e => e.Title == "Device updates");
        Assert.True(refreshed.Enabled);
        Assert.Null(refreshed.DisabledReason);
    }

    [Fact]
    public void Start_SessionRefusalDisablesChangesButNotMyths()
    {
        string reason = "Not administrator";
        var (vm, _, _) = Create(sessionOk: false, sessionReason: reason);

        Assert.Equal(reason, vm.Banner);

        var tune = vm.Entries.First(e => e.Title == "Tune this PC");
        Assert.False(tune.Enabled);
        Assert.Equal(reason, tune.DisabledReason);

        var iso = vm.Entries.First(e => e.Title == "Build a slim ISO");
        Assert.False(iso.Enabled);
        Assert.Equal(reason, iso.DisabledReason);

        var undo = vm.Entries.First(e => e.Title == "Undo changes");
        Assert.False(undo.Enabled);
        Assert.Equal(reason, undo.DisabledReason);

        var settings = vm.Entries.First(e => e.Title == "Settings");
        Assert.False(settings.Enabled);
        Assert.Equal(reason, settings.DisabledReason);

        var myths = vm.Entries.First(e => e.Title == "Myths we tested");
        Assert.True(myths.Enabled);
        Assert.Null(myths.DisabledReason);
    }

    [Fact]
    public void Start_TuneOpensCardsInTuneMode()
    {
        var (vm, nav, draft) = Create();
        var tune = vm.Entries.First(e => e.Title == "Tune this PC");

        tune.Open.Execute(null);

        Assert.Equal(TargetMode.TunePc, draft.Mode);
        Assert.Equal(ScreenKeys.Cards, nav.Current?.Key);
    }

    [Fact]
    public void Start_IsoOpensCardsInIsoMode()
    {
        var (vm, nav, draft) = Create();
        var iso = vm.Entries.First(e => e.Title == "Build a slim ISO");

        iso.Open.Execute(null);

        Assert.Equal(TargetMode.BuildIso, draft.Mode);
        Assert.Equal(ScreenKeys.Cards, nav.Current?.Key);
    }
}
