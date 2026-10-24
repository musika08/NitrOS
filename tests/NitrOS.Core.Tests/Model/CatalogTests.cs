using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.Model;

public class CatalogTests
{
    [Fact]
    public void Switches_IsCachedAcrossCalls()
    {
        var catalog = Switches.CatalogOf(Switches.Make("a-one"), Switches.Make("a-two"));
        Assert.Same(catalog.Switches, catalog.Switches);
        Assert.Equal(2, catalog.Switches.Count);
    }

    [Fact]
    public void Switches_RefreshesWhenGroupsAreReplaced()
    {
        var catalog = Switches.CatalogOf(Switches.Make("a-one"));
        Assert.Single(catalog.Switches);

        var other = Switches.CatalogOf(Switches.Make("b-one"), Switches.Make("b-two"));
        var replaced = catalog with { Groups = other.Groups };

        Assert.Equal(2, replaced.Switches.Count);
        Assert.Single(catalog.Switches);
    }

    [Fact]
    public void Catalogs_WithSameGroupsAndCardsAreEqualRegardlessOfCache()
    {
        var catalog = Switches.CatalogOf(Switches.Make("a-one"));
        var copy = catalog with { };
        _ = catalog.Switches;
        Assert.Equal(catalog, copy);
        Assert.Equal(catalog.GetHashCode(), copy.GetHashCode());
    }
}
