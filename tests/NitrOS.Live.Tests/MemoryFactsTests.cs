using NitrOS.Live;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests;

public class MemoryFactsTests
{
    [Fact]
    public void MemoryInfo_ReservedIsInstalledMinusVisible()
    {
        var info = new MemoryInfo(InstalledMb: 24576, VisibleMb: 16384, InUseMb: 4096);
        Assert.Equal(8192, info.ReservedMb);
    }

    [Fact]
    public void WinMemoryFacts_ReadsThisPc()
    {
        var facts = new WinMemoryFacts();
        var mem = facts.Read();

        Assert.True(mem.InstalledMb >= mem.VisibleMb);
        Assert.True(mem.VisibleMb > 0);
        Assert.True(mem.InUseMb > 0);
    }
}
