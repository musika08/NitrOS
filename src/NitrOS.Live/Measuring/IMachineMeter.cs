namespace NitrOS.Live.Measuring;

public sealed record MachineSnapshot(long MemoryInUseMb, int Processes, DateTimeOffset At);

public interface IMachineMeter
{
    MemoryInfo Memory();
    MachineSnapshot ReadOnce();
    DateTimeOffset LastBoot();
}
