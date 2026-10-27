namespace NitrOS.Live;

public record MemoryInfo(int InstalledMb, int VisibleMb, int InUseMb)
{
    public int ReservedMb => InstalledMb - VisibleMb;
}

public interface IMemoryFacts
{
    MemoryInfo Read();
}
