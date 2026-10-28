using NitrOS.Live;
using NitrOS.Live.Measuring;

namespace NitrOS.App.Tests.Fakes;

public sealed class FakeMeter : IMachineMeter
{
    public MemoryInfo CurrentMemory { get; set; } = new(24576, 15974, 6000);
    public MachineSnapshot CurrentSnapshot { get; set; } = new(6000, 150, DateTimeOffset.Now);
    public DateTimeOffset CurrentLastBoot { get; set; } = DateTimeOffset.Now.AddDays(-1);

    public MemoryInfo Memory() => CurrentMemory;

    public MachineSnapshot ReadOnce() => CurrentSnapshot;

    public DateTimeOffset LastBoot() => CurrentLastBoot;
}
