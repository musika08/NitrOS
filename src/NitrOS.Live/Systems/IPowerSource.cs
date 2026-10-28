namespace NitrOS.Live.Systems;

public sealed record PowerState(bool HasBattery, bool OnBattery, int? BatteryPercent);

public interface IPowerSource
{
    PowerState Read();
}
