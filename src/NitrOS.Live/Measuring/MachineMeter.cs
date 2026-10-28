namespace NitrOS.Live.Measuring;

public static class MachineMeter
{
    public static MachineSnapshot Sample(IMachineMeter meter, int count, TimeSpan gap, Action<TimeSpan> sleep)
    {
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentNullException.ThrowIfNull(sleep);
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "count must be at least 1");

        var mems = new List<long>(count);
        var procs = new List<int>(count);
        DateTimeOffset at = DateTimeOffset.Now;

        for (int i = 0; i < count; i++)
        {
            if (i > 0) sleep(gap);
            var snap = meter.ReadOnce();
            mems.Add(snap.MemoryInUseMb);
            procs.Add(snap.Processes);
            at = snap.At;
        }

        mems.Sort();
        procs.Sort();

        int mid = (count - 1) / 2;
        return new MachineSnapshot(mems[mid], procs[mid], at);
    }
}
