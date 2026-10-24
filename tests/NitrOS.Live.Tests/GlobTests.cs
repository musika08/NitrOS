namespace NitrOS.Live.Tests;

public class GlobTests
{
    [Fact]
    public void Glob_MatchesStarAndIgnoresCase()
    {
        Assert.True(Glob.IsMatch("MicrosoftEdgeAutoLaunch_*", "MicrosoftEdgeAutoLaunch_96AC5A0F1B2C"));
        Assert.True(Glob.IsMatch("Foo*", "foo"));
        Assert.False(Glob.IsMatch("a*b", "ac"));
        Assert.True(Glob.IsMatch("a.b", "A.B"));
        Assert.False(Glob.IsMatch("a.b", "axb"));
    }
}
