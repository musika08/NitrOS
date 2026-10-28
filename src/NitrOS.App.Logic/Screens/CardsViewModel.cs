using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Formatting;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Startup;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;

namespace NitrOS.App.Logic.Screens;

public sealed class CardsViewModel : ObservableObject, IScreen
{
    private readonly Navigator _navigator;
    private readonly Draft _draft;
    private readonly AppContextInfo _context;
    private string _memoryLine = "";
    private IReadOnlyList<CardItem> _cards = [];

    public ScreenKey Key => ScreenKeys.Cards;

    public string MemoryLine
    {
        get => _memoryLine;
        private set => SetProperty(ref _memoryLine, value);
    }

    public IReadOnlyList<CardItem> Cards
    {
        get => _cards;
        private set => SetProperty(ref _cards, value);
    }

    public IRelayCommand<CardId> Pick { get; }

    public CardsViewModel(Navigator navigator, Draft draft, AppContextInfo context)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _context = context ?? throw new ArgumentNullException(nameof(context));

        Pick = new RelayCommand<CardId>(card =>
        {
            _draft.PickCard(card);
            _navigator.GoTo(ScreenKeys.Switches);
        });

        BuildCards();
    }

    public void OnShown(object? argument)
    {
        BuildCards();
    }

    private void BuildCards()
    {
        MemoryLine = Plain.MemoryLine(_context.Memory);

        if (_context.Catalog is null)
        {
            Cards = [];
            return;
        }

        var catalog = _context.Catalog;
        var device = _context.Device;
        var mode = _draft.Mode;

        var items = new List<CardItem>();
        CardId[] presetCards = [CardId.Recommended, CardId.Gamer, CardId.Streamer, CardId.MaxLean];

        foreach (var id in presetCards)
        {
            string title = id switch
            {
                CardId.Recommended => "Recommended",
                CardId.Gamer => "Gamer",
                CardId.Streamer => "Streamer",
                CardId.MaxLean => "Max Lean",
                _ => id.ToString()
            };

            var sel = Selection.FromCard(catalog, id, mode, device);
            var m = catalog.Cards.For(id);

            string memText = m is not null
                ? $"About {Plain.Memory(m.MemoryMb)} less memory in use (estimate from our test PC)"
                : "Memory saved: not measured yet";

            string procText = m is not null
                ? $"About {m.Processes} fewer processes (estimate)"
                : "Fewer processes: not measured yet";

            var totals = TotalsCalculator.For(sel);
            string stopsText;
            if (totals.StopsWorking.Count == 0)
            {
                stopsText = "Nothing you would notice stops working.";
            }
            else
            {
                var firstThree = totals.StopsWorking.Take(3).ToList();
                var joined = string.Join(" ", firstThree);
                if (totals.StopsWorking.Count > 3)
                {
                    int more = totals.StopsWorking.Count - 3;
                    stopsText = $"{joined} and {more} more.";
                }
                else
                {
                    stopsText = joined;
                }
            }

            string? partlyNote = null;
            if (mode == TargetMode.TunePc && sel.Dropped.Count > 0)
            {
                partlyNote = $"{sel.Dropped.Count} of this card's switches are kept on this PC.";
            }

            items.Add(new CardItem(id, title, memText, procText, stopsText, partlyNote));
        }

        items.Add(new CardItem(
            CardId.Custom,
            "Custom",
            "",
            "",
            "Starts with nothing ticked. You pick every switch.",
            null));

        Cards = items;
    }
}
