using NitrOS.Live;
using NitrOS.Live.Measuring;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Measuring;

public sealed class MachineMeterTests
{
    private sealed class FakeMeter(List<MachineSnapshot> snapshots) : IMachineMeter
    {
        private int _index;

        public MemoryInfo Memory() => new(8192, 8000, (int)snapshots[0].MemoryInUseMb);

        public MachineSnapshot ReadOnce()
        {
            var snap = snapshots[_index % snapshots.Count];
            _index++;
            return snap;
        }

        public DateTimeOffset LastBoot() => DateTimeOffset.Now.AddDays(-1);
    }

    [Fact]
    public void MachineMeter_SampleKeepsMiddleValues()
    {
        var snaps = new List<MachineSnapshot>
        {
            new(5000, 150, DateTimeOffset.Now),
            new(4000, 170, DateTimeOffset.Now),
            new(6000, 160, DateTimeOffset.Now)
        };
        var meter = new FakeMeter(snaps);

        int sleeps = 0;
        var sample = MachineMeter.Sample(meter, 3, TimeSpan.FromMilliseconds(10), _ => sleeps++);

        Assert.Equal(5000, sample.MemoryInUseMb);
        Assert.Equal(160, sample.Processes);
        Assert.Equal(2, sleeps);
    }

    [Fact]
    public void WinMachineMeter_ReadsThisPc()
    {
        var meter = new WinMachineMeter();
        var mem = meter.Memory();
        Assert.True(mem.InstalledMb >= mem.VisibleMb);
        Assert.True(mem.VisibleMb > 0);
        Assert.True(mem.InUseMb > 0);

        var snap = meter.ReadOnce();
        Assert.True(snap.MemoryInUseMb > 0);
        Assert.True(snap.Processes > 10);

        var lastBoot = meter.LastBoot();
        Assert.True(lastBoot < DateTimeOffset.Now);
    }
}
