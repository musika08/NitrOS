using NitrOS.Core.Selecting;

namespace NitrOS.Live;

public interface IDeviceFacts
{
    bool HasBattery { get; }
    int MaxTouches { get; }
    string Manufacturer { get; }
    string Model { get; }
}

/// <summary>Turns raw hardware facts into the traits the catalog selects on.</summary>
public static class DeviceDetector
{
    // Matched anywhere in the model text: the Ally X reports "ROG Xbox Ally X RC73XA_RC73XA", not a bare code.
    private static readonly string[] KnownHandheldModels = ["RC71L", "RC72LA", "RC73XA", "RC73YA"];

    public static DeviceProfile Detect(IDeviceFacts facts)
    {
        var battery = facts.HasBattery;
        var touch = facts.MaxTouches > 0;
        var model = facts.Model ?? "";
        var known = KnownHandheldModels.Any(m => model.Contains(m, StringComparison.OrdinalIgnoreCase));
        return new DeviceProfile(battery, touch, IsHandheld: battery && touch && known);
    }
}
