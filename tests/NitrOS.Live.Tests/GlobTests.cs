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

    [Fact]
    public void Glob_EdgeCases()
    {
        Assert.True(Glob.IsMatch("*", "anything"));
        Assert.True(Glob.IsMatch("*", ""));
        Assert.True(Glob.IsMatch("a*b*c", "aXXbYYc"));
        Assert.True(Glob.IsMatch("**", "x"));
        Assert.False(Glob.IsMatch("a", ""));
        Assert.False(Glob.IsMatch("Foo", "Foo2"));
        Assert.True(Glob.IsMatch("Foo", "FOO"));
        Assert.False(Glob.IsMatch("Foo", "foo\n"));
    }

    [Theory]
    [InlineData("a(b", "a(b")]
    [InlineData("a[b", "a[b")]
    [InlineData("a+b", "a+b")]
    [InlineData("a.b*", "a.bcd")]
    [InlineData("(x)*", "(x)yz")]
    public void Glob_RegexSpecialCharactersAreLiteral(string pattern, string text)
    {
        Assert.True(Glob.IsMatch(pattern, text));
    }

    [Fact]
    public void Glob_SpecialCharactersDoNotActAsRegex()
    {
        Assert.False(Glob.IsMatch("a+b", "aab"));
        Assert.False(Glob.IsMatch("a[bc]", "ab"));
    }
}
