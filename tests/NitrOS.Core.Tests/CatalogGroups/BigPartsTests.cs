using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class BigPartsTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "big-parts");
    }

    private static SwitchDef Sw(string id) => Group().Switches.Single(s => s.Id == id);

    private static void AssertCards(SwitchDef sw, CardDefault r, CardDefault g, CardDefault s, CardDefault m)
    {
        Assert.Equal(r, sw.Cards.Recommended);
        Assert.Equal(g, sw.Cards.Gamer);
        Assert.Equal(s, sw.Cards.Streamer);
        Assert.Equal(m, sw.Cards.MaxLean);
    }

    [Fact]
    public void BigParts_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "search-indexing-off", "search-web-engine-off", "store-remove", "printing-off",
            "recovery-remove", "virtualization-off", "bluetooth-off",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void BigParts_MultiSentenceBreaksOnePerSentence()
    {
        Assert.Equal(2, Sw("search-indexing-off").Breaks.Count);
        Assert.Equal(2, Sw("store-remove").Breaks.Count);
        Assert.Single(Sw("recovery-remove").Breaks);
    }

    [Fact]
    public void BigParts_SpotCheck()
    {
        var sw = Sw("search-indexing-off");
        var expected = new SwitchAction[] { new ServiceStart("WSearch", ServiceStartType.Disabled) };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
        AssertCards(sw, CardDefault.Off, CardDefault.On, CardDefault.On, CardDefault.On);
    }

    [Fact]
    public void BigParts_SearchWebEngineActions()
    {
        const string key = @"SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260";
        var expected = new SwitchAction[]
        {
            new RegistrySet("HKLM", key, "EnabledState", RegistryKind.Dword, "1"),
            new RegistrySet("HKLM", key, "EnabledStateOptions", RegistryKind.Dword, "0"),
            new RegistrySet("HKLM", key, "Variant", RegistryKind.Dword, "0"),
            new RegistrySet("HKLM", key, "VariantPayload", RegistryKind.Dword, "0"),
        };
        var sw = Sw("search-web-engine-off");
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }

    [Fact]
    public void BigParts_PrintingActionsAndMaxLeanOnly()
    {
        var sw = Sw("printing-off");
        var expected = new SwitchAction[]
        {
            new ServiceStart("Spooler", ServiceStartType.Disabled),
            new FeatureDisable("Printing-XPSServices-Features"),
            new FeatureDisable("Printing-PrintToPDFServices-Features"),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
        AssertCards(sw, CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On);
    }

    [Fact]
    public void BigParts_RecoveryIsImageOnly()
    {
        var sw = Sw("recovery-remove");
        Assert.Equal(new[] { Mode.Image }, sw.Modes);
        Assert.Empty(sw.Live);
        Assert.Equal(new SwitchAction[] { new FileRemove(@"%SystemRoot%\System32\Recovery\winre.wim") }, sw.Image);
        Assert.False(sw.Reversible);
    }

    [Fact]
    public void BigParts_VirtualizationCardsOffOffOffOn()
    {
        var sw = Sw("virtualization-off");
        AssertCards(sw, CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On);
        var expected = new SwitchAction[]
        {
            new FeatureDisable("VirtualMachinePlatform"),
            new FeatureDisable("Microsoft-Hyper-V-All"),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }

    [Fact]
    public void BigParts_BluetoothOffOnEveryCard()
    {
        var sw = Sw("bluetooth-off");
        AssertCards(sw, CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.Off);
        Assert.Equal(Risk.Breaks, sw.Risk);
        Assert.Equal(new[] { "Wireless controllers, headsets and earbuds stop working." }, sw.Breaks);
        var expected = new SwitchAction[]
        {
            new ServiceStart("bthserv", ServiceStartType.Disabled),
            new ServiceStart("BTAGService", ServiceStartType.Disabled),
            new ServiceStart("BthAvctpSvc", ServiceStartType.Disabled),
            new ServiceStart("BluetoothUserService", ServiceStartType.Disabled),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }

    [Fact]
    public void BigParts_StoreRemoveCardsAndAction()
    {
        var sw = Sw("store-remove");
        AssertCards(sw, CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On);
        Assert.Equal(new SwitchAction[] { new AppxRemove("Microsoft.WindowsStore") }, sw.Live);
        Assert.False(sw.Reversible);
    }
}
