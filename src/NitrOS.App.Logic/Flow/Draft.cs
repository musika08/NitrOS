using CommunityToolkit.Mvvm.ComponentModel;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;

namespace NitrOS.App.Logic.Flow;

public sealed class Draft(Catalog catalog, DeviceProfile device) : ObservableObject
{
    private readonly Catalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly DeviceProfile _device = device;
    private TargetMode _mode;
    private CardId? _card;
    private Selection? _selection;

    public TargetMode Mode
    {
        get => _mode;
        private set => SetProperty(ref _mode, value);
    }

    public CardId? Card
    {
        get => _card;
        set => SetProperty(ref _card, value);
    }

    public Selection? Selection
    {
        get => _selection;
        set => SetProperty(ref _selection, value);
    }

    public void Start(TargetMode mode)
    {
        Mode = mode;
        Card = null;
        Selection = null;
    }

    public void PickCard(CardId card)
    {
        Card = card;
        Selection = Selection.FromCard(_catalog, card, Mode, _device);
    }

    public string? Toggle(string id)
    {
        if (Selection is null) return null;
        var result = Selection.Toggle(id);
        if (result.Changed)
        {
            Selection = result.Selection;
            Card = Selection.MatchingCard();
        }
        return result.RefusedReason;
    }

    public void UseIds(IEnumerable<string> ids)
    {
        Card = null;
        Selection = Selection.FromIds(_catalog, ids, Mode, _device);
    }
}
