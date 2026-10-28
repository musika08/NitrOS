using NitrOS.Updates.KnownBad;
using NitrOS.Updates.Model;
using Xunit;

namespace NitrOS.Updates.Tests.KnownBad;

public sealed class KnownBadListTests
{
    [Fact]
    public void KnownBad_ShippedListParses()
    {
        var list = KnownBadList.Shipped();
        Assert.NotNull(list);
        Assert.NotEmpty(list.Entries);
    }

    [Fact]
    public void KnownBad_FindsByMakerModelKindVersion()
    {
        var list = KnownBadList.Shipped();
        var entry = list.Find(MakerId.Lenovo, "83E1", UpdateKind.Bios, "N3CN42WW");
        Assert.NotNull(entry);
        Assert.Equal("Lenovo", entry.Maker.ToString());
        Assert.Contains("boot failures", entry.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnownBad_StarModelMatchesAll()
    {
        var list = KnownBadList.Shipped();
        var entry = list.Find(MakerId.Lenovo, "AnyModelCode", UpdateKind.Bios, "N3CN42WW");
        Assert.NotNull(entry);
    }
}
