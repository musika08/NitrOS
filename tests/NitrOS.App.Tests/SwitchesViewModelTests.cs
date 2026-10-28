using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Startup;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;

namespace NitrOS.App.Tests;

public sealed class SwitchesViewModelTests
{
    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public void OnShown(object? argument) { }
    }

    private static (SwitchesViewModel vm, Navigator nav, Draft draft) Create(
        Catalog catalog,
        TargetMode mode = TargetMode.TunePc,
        CardId? card = null)
    {
        var nav = new Navigator(new RunGate());
        nav.Register(ScreenKeys.Review, () => new DummyScreen(ScreenKeys.Review));
        nav.Register(ScreenKeys.Myths, () => new DummyScreen(ScreenKeys.Myths));

        var dev = DeviceProfile.Desktop;
        var draft = new Draft(catalog, dev);
        draft.Start(mode);
        if (card.HasValue)
            draft.PickCard(card.Value);
        else
            draft.PickCard(CardId.Custom);

        var session = new SessionCheck(true, null);
        var context = new AppContextInfo(catalog, [], dev, new MemoryInfo(16000, 15000, 4000), session);
        var vm = new SwitchesViewModel(nav, draft, context);
        return (vm, nav, draft);
    }

    [Fact]
    public void Switches_IsoOnlyGreyedWithReasonInTuneMode()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, draft) = Create(repo, mode: TargetMode.TunePc);

        var row = vm.Groups.SelectMany(g => g.Rows).First(r => r.Id == "recovery-remove");
        Assert.False(row.IsAvailable);
        Assert.Equal("Only available when building an ISO.", row.UnavailableReason);

        row.Toggle.Execute(null);
        Assert.False(draft.Selection!.IsOn("recovery-remove"));
    }

    [Fact]
    public void Switches_SearchFiltersRowsButNotTotals()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo, card: CardId.Recommended);

        string originalTotals = vm.TotalsText;
        int totalRows = vm.Groups.Sum(g => g.Rows.Count);

        vm.SearchText = "Xbox";
        int filteredRows = vm.Groups.Sum(g => g.Rows.Count);

        Assert.True(filteredRows > 0);
        Assert.True(filteredRows < totalRows);
        Assert.Equal(originalTotals, vm.TotalsText);
    }

    [Fact]
    public void Switches_DependencyWarningShownOnRequiredSwitch()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, draft) = Create(repo, card: CardId.Custom);

        // Turn on store-remove while xbox-app-remove is off
        var storeRow = vm.Groups.SelectMany(g => g.Rows).First(r => r.Id == "store-remove");
        storeRow.Toggle.Execute(null);

        Assert.True(draft.Selection!.IsOn("store-remove"));
        Assert.False(draft.Selection!.IsOn("xbox-app-remove"));

        Assert.Contains(storeRow.Warnings, w => w.Contains("Microsoft Store is needed by Xbox app, which you are keeping."));
    }

    [Fact]
    public void Switches_ToggleUpdatesTotalsAndCardText()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo, card: CardId.Recommended);

        Assert.Equal("Recommended", vm.CardText);
        string initialOn = vm.OnCountText;

        // Toggle one switch off
        var firstOnRow = vm.Groups.SelectMany(g => g.Rows).First(r => r.IsOn);
        firstOnRow.Toggle.Execute(null);

        Assert.Equal("Your own mix", vm.CardText);
        Assert.NotEqual(initialOn, vm.OnCountText);
    }

    [Fact]
    public void Switches_RiskBadgeTexts()
    {
        var repo = TestCatalog.LoadRepo();
        var (vm, _, _) = Create(repo);

        var rows = vm.Groups.SelectMany(g => g.Rows).ToList();
        Assert.Contains(rows, r => r.RiskBadge == "Safe");
        Assert.Contains(rows, r => r.RiskBadge == "Caution");
        Assert.Contains(rows, r => r.RiskBadge == "Breaks something");
    }

    [Fact]
    public void Switches_CantUndoBadgeOnlyInTuneMode()
    {
        var repo = TestCatalog.LoadRepo();
        var (vmTune, _, _) = Create(repo, mode: TargetMode.TunePc);
        var (vmIso, _, _) = Create(repo, mode: TargetMode.BuildIso);

        var irreversibleTune = vmTune.Groups.SelectMany(g => g.Rows).First(r => repo.Get(r.Id).Reversible == false);
        Assert.True(irreversibleTune.CantUndoBadge);

        var irreversibleIso = vmIso.Groups.SelectMany(g => g.Rows).First(r => r.Id == irreversibleTune.Id);
        Assert.False(irreversibleIso.CantUndoBadge);
    }

    [Fact]
    public void Switches_ShowsWhetherSetupTurnsSettingOnOrOffAndIfInUse()
    {
        var repo = TestCatalog.LoadRepo();
        var recSel = Selection.FromCard(repo, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);

        var activeId = recSel.On.First();
        var probe = new FakeSwitchProbe([activeId]);

        var nav = new Navigator(new RunGate());
        var dev = DeviceProfile.Desktop;
        var draft = new Draft(repo, dev);
        draft.Start(TargetMode.TunePc);
        draft.PickCard(CardId.Recommended);
        var session = new SessionCheck(true, null);
        var context = new AppContextInfo(repo, [], dev, new MemoryInfo(16000, 15000, 4000), session);
        var vm = new SwitchesViewModel(nav, draft, context, probe);

        Assert.Equal("Recommended", vm.CardText);
        Assert.Contains("settings will change", vm.CardStatusText);

        var rows = vm.Groups.SelectMany(g => g.Rows).ToList();
        var appliedRow = rows.First(r => r.Id == activeId);
        Assert.True(appliedRow.IsOn);
        Assert.Equal("Already OFF", appliedRow.ActionBadge);

        var notAppliedRow = rows.First(r => r.IsOn && r.Id != activeId);
        Assert.Equal("Will turn OFF", notAppliedRow.ActionBadge);

        var offRow = rows.First(r => !r.IsOn && r.Id != activeId && r.Id != "svchost-grouping-on");
        Assert.Equal("Keeps ON", offRow.ActionBadge);

        var svchostRow = rows.First(r => r.Id == "svchost-grouping-on");
        Assert.Equal("Keeps OFF", svchostRow.ActionBadge);
    }
}
