using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.Selecting;

public class WarningEngineTests
{
    private static Selection Custom(NitrOS.Core.Model.Catalog catalog, TargetMode mode, params string[] on)
    {
        var sel = Selection.FromCard(catalog, CardId.Custom, mode, DeviceProfile.Desktop);
        foreach (var id in on) sel = sel.Toggle(id).Selection;
        return sel;
    }

    private static NitrOS.Core.Model.Catalog XboxStore() => Switches.CatalogOf(
        Switches.Make("store", b => b.Feature = "Microsoft Store"),
        Switches.Make("xbox", b => { b.Feature = "Xbox app"; b.Requires = ["store"]; }));

    [Fact]
    public void Breaks_OneWarningPerSentence()
    {
        var catalog = Switches.CatalogOf(
            Switches.Make("a", b => b.Breaks = ["First thing breaks.", "Second thing breaks."]));
        var warnings = WarningEngine.For(Custom(catalog, TargetMode.TunePc, "a"));
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, w => Assert.Equal(WarningKind.Breaks, w.Kind));
        Assert.Equal(new[] { "First thing breaks.", "Second thing breaks." }, warnings.Select(w => w.Message).ToArray());
    }

    [Fact]
    public void Dependency_WarnsWhenKeptFeatureLosesDependency()
    {
        var warnings = WarningEngine.For(Custom(XboxStore(), TargetMode.TunePc, "store"));
        var w = Assert.Single(warnings);
        Assert.Equal(WarningKind.Dependency, w.Kind);
        Assert.Equal("store", w.SwitchId);
        Assert.Equal("xbox", w.RelatedId);
        Assert.Equal("Microsoft Store is needed by Xbox app, which you are keeping.", w.Message);
    }

    [Fact]
    public void Dependency_WarningClearsWhenDependentAlsoRemoved()
    {
        var warnings = WarningEngine.For(Custom(XboxStore(), TargetMode.TunePc, "store", "xbox"));
        Assert.DoesNotContain(warnings, w => w.Kind == WarningKind.Dependency);
    }

    [Fact]
    public void Irreversible_OnlyInTunePc()
    {
        var catalog = Switches.CatalogOf(Switches.Make("gone", b =>
        {
            b.Reversible = false;
            b.Modes = [Mode.Live, Mode.Image];
            b.Image = [new FileRemove(@"C:\Test")];
        }));
        var tune = WarningEngine.For(Custom(catalog, TargetMode.TunePc, "gone"));
        var w = Assert.Single(tune);
        Assert.Equal(WarningKind.Irreversible, w.Kind);
        Assert.Equal("Can't be undone without reinstalling.", w.Message);
        Assert.Empty(WarningEngine.For(Custom(catalog, TargetMode.BuildIso, "gone")));
    }

    [Fact]
    public void NothingOn_NoWarnings()
    {
        var catalog = Switches.CatalogOf(
            Switches.Make("a", b => { b.Breaks = ["Breaks."]; b.Reversible = false; }),
            Switches.Make("b"));
        Assert.Empty(WarningEngine.For(Custom(catalog, TargetMode.TunePc)));
    }
}
