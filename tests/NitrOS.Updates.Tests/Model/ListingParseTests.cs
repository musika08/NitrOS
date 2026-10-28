namespace NitrOS.Updates.Tests.Model;

using System;
using NitrOS.Updates.Model;

public sealed class ListingParseTests
{
    [Fact]
    public void ListingParse_Sizes()
    {
        Assert.Equal((long)Math.Round(15.36 * 1024 * 1024), ListingParse.SizeBytes("15.36 MB"));
        Assert.Equal((long)Math.Round(1.07 * 1024 * 1024 * 1024), ListingParse.SizeBytes("1.07 GB"));
        Assert.Equal((long)Math.Round(4.2 * 1024 * 1024), ListingParse.SizeBytes("4.2 MB"));
        Assert.Equal((long)Math.Round(818.9 * 1024 * 1024), ListingParse.SizeBytes("818.9 MB"));
        Assert.Null(ListingParse.SizeBytes(""));
        Assert.Null(ListingParse.SizeBytes(null));
        Assert.Null(ListingParse.SizeBytes("invalid"));
    }

    [Fact]
    public void ListingParse_AsusDate()
    {
        Assert.Equal(new DateOnly(2026, 10, 14), ListingParse.AsusDate("2026/10/14"));
        Assert.Null(ListingParse.AsusDate("14/10/2026"));
        Assert.Null(ListingParse.AsusDate(""));
        Assert.Null(ListingParse.AsusDate(null));
    }
}
