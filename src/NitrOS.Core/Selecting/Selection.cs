using System.Collections.Frozen;
using NitrOS.Core.Model;

namespace NitrOS.Core.Selecting;

public enum TargetMode { TunePc, BuildIso }

public record Availability(bool Available, string? Reason);

public record ToggleResult(Selection Selection, bool Changed, string? RefusedReason);

public sealed class Selection
{
    private static readonly CardId[] Cards =
        [CardId.Recommended, CardId.Gamer, CardId.Streamer, CardId.MaxLean];

    private readonly FrozenSet<string> _on;

    private Selection(Catalog catalog, TargetMode mode, DeviceProfile device, FrozenSet<string> on)
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
        if (card != CardId.Custom)
        {
            foreach (var def in catalog.Switches)
            {
                if (DefaultFor(def, card) == CardDefault.On && AvailabilityOf(def, mode, device).Available)
                    on.Add(def.Id);
            }
        }
        return new Selection(catalog, mode, device, on.ToFrozenSet());
    }

    public Availability AvailabilityOf(string id) => AvailabilityOf(Catalog.Get(id), Mode, Device);

    public ToggleResult Toggle(string id)
    {
        var def = Catalog.Get(id);
        var availability = AvailabilityOf(def, Mode, Device);
        if (!availability.Available)
            return new ToggleResult(this, false, availability.Reason);

        var next = new HashSet<string>(_on);
        if (!next.Remove(id)) next.Add(id);
        return new ToggleResult(new Selection(Catalog, Mode, Device, next.ToFrozenSet()), true, null);
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
        Catalog.Switches.All(def => DefaultFor(def, card) != CardDefault.On || AvailabilityOf(def, Mode, Device).Available);

    private static Availability AvailabilityOf(SwitchDef def, TargetMode mode, DeviceProfile device)
    {
        if (mode == TargetMode.TunePc && !def.Modes.Contains(Model.Mode.Live))
            return new Availability(false, "Only available when building an ISO.");
        if (mode == TargetMode.BuildIso && !def.Modes.Contains(Model.Mode.Image))
            return new Availability(false, "Only available when tuning this PC.");

        foreach (var trait in new[] { DeviceTrait.Battery, DeviceTrait.Touchscreen, DeviceTrait.Handheld })
        {
            if (def.SkipOn.Contains(trait) && device.Has(trait))
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
