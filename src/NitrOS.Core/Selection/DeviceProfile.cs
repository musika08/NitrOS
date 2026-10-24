using NitrOS.Core.Model;

namespace NitrOS.Core.Selection;

public record DeviceProfile(bool HasBattery, bool HasTouchscreen, bool IsHandheld)
{
    public static DeviceProfile Desktop { get; } = new(false, false, false);

    public bool Has(DeviceTrait trait) => trait switch
    {
        DeviceTrait.Battery => HasBattery,
        DeviceTrait.Touchscreen => HasTouchscreen,
        DeviceTrait.Handheld => IsHandheld,
        _ => false,
    };
}
