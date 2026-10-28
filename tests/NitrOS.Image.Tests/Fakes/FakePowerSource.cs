namespace NitrOS.Image.Tests.Fakes;

using NitrOS.Live.Systems;

public sealed class FakePowerSource : IPowerSource
{
    public PowerState State { get; set; } = new(HasBattery: true, OnBattery: false, BatteryPercent: 90);

    public PowerState Read() => State;
}
