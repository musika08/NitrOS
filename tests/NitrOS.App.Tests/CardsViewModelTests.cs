using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Startup;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;

namespace NitrOS.App.Tests;

public sealed class CardsViewModelTests
{
    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public void OnShown(object? argument) { }
    }

    private static (CardsViewModel vm, Navigator nav, Draft draft) Create(
        Catalog catalog,
        TargetMode mode = TargetMode.TunePc,
        DeviceProfile? device = null)
    {
        var nav = new Navigator(new RunGate());
        nav.Register(ScreenKeys.Switches, () => new DummyScreen(ScreenKeys.Switches));
        var dev = device ?? DeviceProfile.Desktop;
        var draft = new Draft(catalog, dev);
        draft.Start(mode);
        var session = new SessionCheck(true, null);
        var context = new AppContextInfo(catalog, [], dev, new MemoryInfo(16000, 15000, 4000), session);
        var vm = new CardsViewModel(nav, draft, context);
        return (vm, nav, draft);
    }

    [Fact]
    public void Cards_NullMeasurementShowsNotMeasuredYet()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo);

        var gamer = vm.Cards.First(c => c.Id == CardId.Gamer);
        Assert.Equal("Memory saved: not measured yet", gamer.MemoryText);
        Assert.Equal("Fewer processes: not measured yet", gamer.ProcessesText);
    }

    [Fact]
    public void Cards_MeasuredValuesLabelledEstimate()
    {
        var repo = TestCatalog.LoadRepo();
        var gamerMeasurement = new Measurement(1229, 23, "26300", DateOnly.FromDateTime(DateTime.Today));
        var measuredCatalog = repo with { Cards = repo.Cards with { Gamer = gamerMeasurement } };

        var (vm, _, _) = Create(measuredCatalog);
        var gamer = vm.Cards.First(c => c.Id == CardId.Gamer);

        Assert.Equal("About 1.2 GB less memory in use (estimate from our test PC)", gamer.MemoryText);
        Assert.Equal("About 23 fewer processes (estimate)", gamer.ProcessesText);
    }

    [Fact]
    public void Cards_StopsLineListsBreaksSentences()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo);

        var maxLean = vm.Cards.First(c => c.Id == CardId.MaxLean);
        Assert.False(string.IsNullOrWhiteSpace(maxLean.StopsText));
        Assert.NotEqual("Nothing you would notice stops working.", maxLean.StopsText);
    }

    [Fact]
    public void Cards_CustomHasNoMeasurementLines()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo);

        var custom = vm.Cards.First(c => c.Id == CardId.Custom);
        Assert.Equal("", custom.MemoryText);
        Assert.Equal("", custom.ProcessesText);
        Assert.Equal("Starts with nothing ticked. You pick every switch.", custom.StopsText);
        Assert.Null(custom.PartlyNote);
    }

    [Fact]
    public void Cards_IsoModeNeverShowsPartlyApplied()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo, mode: TargetMode.BuildIso);

        Assert.All(vm.Cards, c => Assert.Null(c.PartlyNote));
    }

    [Fact]
    public void Cards_TuneModeNotesDroppedSwitches()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo, mode: TargetMode.TunePc);

        var maxLean = vm.Cards.First(c => c.Id == CardId.MaxLean);
        Assert.NotNull(maxLean.PartlyNote);
        Assert.Contains("kept on this PC", maxLean.PartlyNote);
    }

    [Fact]
    public void Cards_PickTicksCardSwitchesAndOpensSwitches()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, nav, draft) = Create(repo);

        vm.Pick.Execute(CardId.Recommended);

        Assert.Equal(CardId.Recommended, draft.Card);
        Assert.NotNull(draft.Selection);
        Assert.True(draft.Selection.On.Count > 0);
        Assert.Equal(ScreenKeys.Switches, nav.Current?.Key);
    }
}
