using System.Diagnostics;
using NitrOS.Live.Measuring;

namespace NitrOS.Live.Windows;

public sealed class WinMachineMeter(IMemoryFacts memory) : IMachineMeter
{
    private readonly IMemoryFacts _memory = memory ?? throw new ArgumentNullException(nameof(memory));

    public WinMachineMeter() : this(new WinMemoryFacts()) { }

    public MemoryInfo Memory() => _memory.Read();

    public MachineSnapshot ReadOnce()
    {
        var mem = _memory.Read();
        int currentPid = Environment.ProcessId;
        int count = 0;
        foreach (var p in Process.GetProcesses())
        {
            if (p.Id != 0 && p.Id != currentPid) count++;
            p.Dispose();
        }
        return new MachineSnapshot(mem.InUseMb, count, DateTimeOffset.Now);
    }

    public DateTimeOffset LastBoot() =>
        DateTimeOffset.Now.AddMilliseconds(-Environment.TickCount64);
}
