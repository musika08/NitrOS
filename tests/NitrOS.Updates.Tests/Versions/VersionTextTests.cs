namespace NitrOS.Updates.Tests.Versions;

using NitrOS.Updates.Versions;

public sealed class VersionTextTests
{
    [Fact]
    public void Version_BiosNumbers()
    {
        Assert.True(VersionText.Compare("319", "310") > 0);
        Assert.Equal(0, VersionText.Compare("RC73XA.319", "319"));
        Assert.True(VersionText.Compare("V326", "V325") > 0);
    }

    [Fact]
    public void Version_DottedDrivers()
    {
        Assert.True(VersionText.Compare("V32.0.32015.5008", "32.0.32015.7029") < 0);
        Assert.True(VersionText.Compare("11.19.20.602", "11.19.602.2025") < 0);
        Assert.Equal(0, VersionText.Compare("1.2.31.0", "1.2.31"));
        Assert.Equal(0, VersionText.Compare("V3.1.72.0", "3.1.72.0"));
    }

    [Fact]
    public void Version_LeadingZeros()
    {
        Assert.True(VersionText.Compare("0020", "0015") > 0);
        Assert.Equal(0, VersionText.Compare("0020", "20"));
    }

    [Fact]
    public void Version_UnparseableIsNull()
    {
        Assert.Null(VersionText.Compare("latest version at the MS store", "1.0"));
        Assert.Null(VersionText.Compare("", "1.0"));
        Assert.Null(VersionText.Compare(null, "1.0"));
        Assert.Null(VersionText.Compare("1.2a", "1.2"));
    }

    [Fact]
    public void Version_FromWindowsUpdateTitles()
    {
        Assert.Equal("11.19.20.602", VersionText.FromTitle("Realtek - Net - 11.19.20.602"));
        Assert.Equal("10.1.2.319", VersionText.FromTitle("ASUSTeK COMPUTER INC. Firmware Driver Update (10.1.2.319)"));
        Assert.Null(VersionText.FromTitle("No version here"));
    }
}
