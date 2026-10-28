using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Formatting;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Startup;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;

namespace NitrOS.App.Logic.Screens;

public sealed class SwitchesViewModel : ObservableObject, IScreen
{
    private readonly Navigator _navigator;
    private readonly Draft _draft;
    private readonly AppContextInfo _context;

    private string _searchText = "";
    private IReadOnlyList<SwitchGroupView> _groups = [];
    private string _totalsText = "";
    private string _onCountText = "";
    private string _cardText = "";
    private readonly List<SwitchRow> _allRows = [];

    public ScreenKey Key => ScreenKeys.Switches;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                FilterGroups();
        }
    }

    public IReadOnlyList<SwitchGroupView> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public string TotalsText
    {
        get => _totalsText;
        private set => SetProperty(ref _totalsText, value);
    }

    public string OnCountText
    {
        get => _onCountText;
        private set => SetProperty(ref _onCountText, value);
    }

    public string CardText
    {
        get => _cardText;
        private set => SetProperty(ref _cardText, value);
    }

    public IRelayCommand Review { get; }
    public IRelayCommand OpenMyths { get; }

    public SwitchesViewModel(Navigator navigator, Draft draft, AppContextInfo context)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _context = context ?? throw new ArgumentNullException(nameof(context));

        Review = new RelayCommand(
            () => _navigator.GoTo(ScreenKeys.Review),
            () => _draft.Selection is not null && _draft.Selection.On.Count > 0);

        OpenMyths = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Myths));

        BuildRows();
    }

    public void OnShown(object? argument)
    {
        BuildRows();
    }

    private void BuildRows()
    {
        _allRows.Clear();
        if (_context.Catalog is null || _draft.Selection is null)
        {
            Groups = [];
            TotalsText = "";
            OnCountText = "";
            CardText = "";
            Review.NotifyCanExecuteChanged();
            return;
        }

        var catalog = _context.Catalog;
        var selection = _draft.Selection;
        bool isTuneMode = _draft.Mode == TargetMode.TunePc;
        var allWarnings = WarningEngine.For(selection);

        foreach (var group in catalog.Groups)
        {
            foreach (var def in group.Switches)
            {
                var avail = selection.AvailabilityOf(def.Id);
                bool isOn = selection.IsOn(def.Id);

                var warnings = isOn
                    ? def.Breaks.Concat(allWarnings.Where(w => w.Kind == WarningKind.Dependency && w.SwitchId == def.Id).Select(w => w.Message)).ToList()
                    : new List<string>();

                var row = new SwitchRow(
                    def,
                    isOn,
                    avail.Available,
                    avail.Reason,
                    isTuneMode,
                    warnings,
                    OnToggleSwitch);

                _allRows.Add(row);
            }
        }

        UpdateDerivedState();
        FilterGroups();
    }

    private void OnToggleSwitch(string id)
    {
        _draft.Toggle(id);
        if (_draft.Selection is null) return;

        var selection = _draft.Selection;
        var allWarnings = WarningEngine.For(selection);

        foreach (var row in _allRows)
        {
            bool isOn = selection.IsOn(row.Id);
            row.IsOn = isOn;
            var def = _context.Catalog?.Get(row.Id);
            if (def is not null && isOn)
            {
                row.Warnings = def.Breaks.Concat(allWarnings.Where(w => w.Kind == WarningKind.Dependency && w.SwitchId == def.Id).Select(w => w.Message)).ToList();
            }
            else
            {
                row.Warnings = [];
            }
        }

        UpdateDerivedState();
    }

    private void UpdateDerivedState()
    {
        if (_draft.Selection is null) return;
        var sel = _draft.Selection;

        var totals = TotalsCalculator.For(sel);
        TotalsText = Formatting.TotalsText.For(totals);

        int count = sel.On.Count;
        OnCountText = count == 1 ? "1 switch on" : $"{count} switches on";

        var matchingCard = sel.MatchingCard();
        CardText = matchingCard switch
        {
            CardId.Recommended => "Recommended",
            CardId.Gamer => "Gamer",
            CardId.Streamer => "Streamer",
            CardId.MaxLean => "Max Lean",
            _ => "Your own mix"
        };

        Review.NotifyCanExecuteChanged();
    }

    private void FilterGroups()
    {
        if (_context.Catalog is null)
        {
            Groups = [];
            return;
        }

        var groups = new List<SwitchGroupView>();
        var rowsById = _allRows.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var group in _context.Catalog.Groups)
        {
            var matchingRows = new List<SwitchRow>();
            foreach (var def in group.Switches)
            {
                if (rowsById.TryGetValue(def.Id, out var row) && Matches(row, _searchText))
                {
                    matchingRows.Add(row);
                }
            }

            if (matchingRows.Count > 0)
            {
                groups.Add(new SwitchGroupView(group.Title, matchingRows));
            }
        }

        Groups = groups;
    }

    private static bool Matches(SwitchRow row, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Feature.Contains(query, StringComparison.OrdinalIgnoreCase)
            || row.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
