using NitrOS.Core.Model;

namespace NitrOS.Core.Selecting;

public enum TargetMode { TunePc, BuildIso }

public record Availability(bool Available, string? Reason);

public record ToggleResult(Selection Selection, bool Changed, string? RefusedReason);

public sealed class Selection
{
    private static readonly CardId[] Cards =
        [CardId.Recommended, CardId.Gamer, CardId.Streamer, CardId.MaxLean];

    private readonly HashSet<string> _on;

    private Selection(Catalog catalog, TargetMode mode, DeviceProfile device, HashSet<string> on)
    {
        Catalog = catalog;
        Mode = mode;
        Device = device;
        _on = on;
    }

    public Catalog Catalog { get; }
    public TargetMode Mode { get; }
    public DeviceProfile Device { get; }
    public IReadOnlySet<string> On => _on;

    public bool IsOn(string id) => _on.Contains(id);

    public static Selection FromCard(Catalog catalog, CardId card, TargetMode mode, DeviceProfile device)
    {
        var on = new HashSet<string>();
        var probe = new Selection(catalog, mode, device, on);
        if (card != CardId.Custom)
        {
            foreach (var def in catalog.Switches)
            {
                if (DefaultFor(def, card) == CardDefault.On && probe.AvailabilityOf(def).Available)
                    on.Add(def.Id);
            }
        }
        return probe;
    }

    public Availability AvailabilityOf(string id) => AvailabilityOf(Catalog.Get(id));

    public ToggleResult Toggle(string id)
    {
        var def = Catalog.Get(id);
        var availability = AvailabilityOf(def);
        if (!availability.Available)
            return new ToggleResult(this, false, availability.Reason);

        var next = new HashSet<string>(_on);
        if (!next.Remove(id)) next.Add(id);
        return new ToggleResult(new Selection(Catalog, Mode, Device, next), true, null);
    }

    /// <summary>The card whose preset equals the current selection, or null. An empty selection matches none.</summary>
    public CardId? MatchingCard()
    {
        if (_on.Count == 0) return null;
        foreach (var card in Cards)
        {
            if (FromCard(Catalog, card, Mode, Device)._on.SetEquals(_on))
                return card;
        }
        return null;
    }

    /// <summary>
    /// True when every switch the card turns on is available in the current mode and device,
    /// so applying the card dropped nothing.
    /// </summary>
    public bool AppliesWholeCard(CardId card) =>
        Catalog.Switches.All(def => DefaultFor(def, card) != CardDefault.On || AvailabilityOf(def).Available);

    private Availability AvailabilityOf(SwitchDef def)
    {
        if (Mode == TargetMode.TunePc && !def.Modes.Contains(Model.Mode.Live))
            return new Availability(false, "Only available when building an ISO.");
        if (Mode == TargetMode.BuildIso && !def.Modes.Contains(Model.Mode.Image))
            return new Availability(false, "Only available when tuning this PC.");

        foreach (var trait in new[] { DeviceTrait.Battery, DeviceTrait.Touchscreen, DeviceTrait.Handheld })
        {
            if (def.SkipOn.Contains(trait) && Device.Has(trait))
            {
                var why = trait switch
                {
                    DeviceTrait.Battery => "it runs on battery",
                    DeviceTrait.Touchscreen => "it has a touchscreen",
                    _ => "it is a handheld",
                };
                return new Availability(false, $"Kept on this device because {why}.");
            }
        }
        return new Availability(true, null);
    }

    private static CardDefault DefaultFor(SwitchDef def, CardId card) => card switch
    {
        CardId.Recommended => def.Cards.Recommended,
        CardId.Gamer => def.Cards.Gamer,
        CardId.Streamer => def.Cards.Streamer,
        CardId.MaxLean => def.Cards.MaxLean,
        _ => CardDefault.Off,
    };
}
