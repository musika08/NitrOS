using NitrOS.App.Logic.Formatting;
using NitrOS.Live;

namespace NitrOS.App.Tests;

public sealed class PlainTests
{
    [Fact]
    public void Plain_MemoryFormats()
    {
        Assert.Equal("850 MB", Plain.Memory(850));
        Assert.Equal("1.2 GB", Plain.Memory(1229));
    }

    [Fact]
    public void Plain_MemoryLineNotesGraphicsReserve()
    {
        var info = new MemoryInfo(24576, 15974, 5000);
        var line = Plain.MemoryLine(info);

        Assert.Contains("available to Windows", line);
        Assert.Contains("set aside for graphics", line);
        Assert.Contains("15.6 GB", line);
        Assert.Contains("8.4 GB", line);
        Assert.Contains("24.0 GB", line);
    }

    [Fact]
    public void Plain_MemoryLineNoNoteBelow512()
    {
        var info = new MemoryInfo(16384, 16000, 4000); // 384 MB reserved (< 512)
        var line = Plain.MemoryLine(info);

        Assert.Contains("available to Windows", line);
        Assert.DoesNotContain("set aside for graphics", line);
    }

    [Fact]
    public void Plain_CountFormats()
    {
        Assert.Equal("1 switch", Plain.Count(1, "switch", "switches"));
        Assert.Equal("2 switches", Plain.Count(2, "switch", "switches"));
    }
}
