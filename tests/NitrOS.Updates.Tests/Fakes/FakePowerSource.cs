using NitrOS.Live.Systems;

namespace NitrOS.Updates.Tests.Fakes;

public sealed class FakePowerSource(PowerState initial) : IPowerSource
{
    public PowerState CurrentState { get; set; } = initial;
    public PowerState Read() => CurrentState;
}
