using NitrOS.Core.Model;
using NitrOS.Core.Selection;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.Selection;

public class SelectionTests
{
    private static CardDefaults GamerOnly(CardDefault gamer) => new()
    {
        Recommended = CardDefault.Off,
        Gamer = gamer,
        Streamer = CardDefault.Off,
        MaxLean = CardDefault.Off,
    };

    private static SwitchDef GamerSwitch(string id, CardDefault gamer, Action<SwitchDefBuilder>? more = null) =>
        Switches.Make(id, b => { b.Cards = GamerOnly(gamer); more?.Invoke(b); });

    private static SwitchDef ImageOnly(string id, CardDefault gamer) =>
        GamerSwitch(id, gamer, b =>
        {
            b.Modes = [Mode.Image];
            b.Live = [];
            b.Image = [new FileRemove(@"C:\Test")];
        });

    [Fact]
    public void FromCard_TurnsOnCardDefaults()
    {
        var catalog = Switches.CatalogOf(GamerSwitch("x", CardDefault.On), GamerSwitch("y", CardDefault.Off));
        var sel = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, DeviceProfile.Desktop);
        Assert.Equal(new[] { "x" }, sel.On.ToArray());
        Assert.True(sel.IsOn("x"));
        Assert.False(sel.IsOn("y"));
    }

    [Fact]
    public void FromCard_PendingCountsAsOff()
    {
        var catalog = Switches.CatalogOf(GamerSwitch("x", CardDefault.Pending));
        var sel = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, DeviceProfile.Desktop);
        Assert.False(sel.IsOn("x"));
    }

    [Fact]
    public void FromCard_CustomIsEmpty()
    {
        var catalog = Switches.CatalogOf(GamerSwitch("x", CardDefault.On));
        var sel = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Custom, TargetMode.TunePc, DeviceProfile.Desktop);
        Assert.Empty(sel.On);
    }

    [Fact]
    public void FromCard_SkipsUnavailableSwitches()
    {
        var catalog = Switches.CatalogOf(ImageOnly("x", CardDefault.On));
        var tune = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, DeviceProfile.Desktop);
        var iso = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Gamer, TargetMode.BuildIso, DeviceProfile.Desktop);
        Assert.False(tune.IsOn("x"));
        Assert.True(iso.IsOn("x"));
    }

    [Fact]
    public void FromCard_SkipsProtectedOnDevice()
    {
        var catalog = Switches.CatalogOf(GamerSwitch("x", CardDefault.On, b => b.SkipOn = [DeviceTrait.Battery]));
        var laptop = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, new DeviceProfile(true, false, false));
        var desktop = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, DeviceProfile.Desktop);
        Assert.False(laptop.IsOn("x"));
        Assert.True(desktop.IsOn("x"));
    }

    [Fact]
    public void Toggle_RefusesUnavailableWithReason()
    {
        var catalog = Switches.CatalogOf(
            ImageOnly("x", CardDefault.Off),
            GamerSwitch("b", CardDefault.Off, c => c.SkipOn = [DeviceTrait.Battery]));
        var sel = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Custom, TargetMode.TunePc, new DeviceProfile(true, false, false));

        var image = sel.Toggle("x");
        Assert.False(image.Changed);
        Assert.Equal("Only available when building an ISO.", image.RefusedReason);

        var battery = sel.Toggle("b");
        Assert.False(battery.Changed);
        Assert.Equal("Kept on this device because it runs on battery.", battery.RefusedReason);
    }

    [Fact]
    public void Availability_UsesExactReasons()
    {
        var catalog = Switches.CatalogOf(
            GamerSwitch("live", CardDefault.Off),
            GamerSwitch("t", CardDefault.Off, b => b.SkipOn = [DeviceTrait.Touchscreen]),
            GamerSwitch("h", CardDefault.Off, b => b.SkipOn = [DeviceTrait.Handheld]));
        var iso = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Custom, TargetMode.BuildIso, DeviceProfile.Desktop);
        Assert.Equal("Only available when tuning this PC.", iso.AvailabilityOf("live").Reason);

        var device = new DeviceProfile(false, true, true);
        var sel = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Custom, TargetMode.TunePc, device);
        Assert.Equal("Kept on this device because it has a touchscreen.", sel.AvailabilityOf("t").Reason);
        Assert.Equal("Kept on this device because it is a handheld.", sel.AvailabilityOf("h").Reason);
        Assert.True(sel.AvailabilityOf("live").Available);
        Assert.Null(sel.AvailabilityOf("live").Reason);
    }

    [Fact]
    public void Toggle_FlipsAvailableSwitch()
    {
        var catalog = Switches.CatalogOf(GamerSwitch("x", CardDefault.Off));
        var original = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Custom, TargetMode.TunePc, DeviceProfile.Desktop);

        var on = original.Toggle("x");
        Assert.True(on.Changed);
        Assert.Null(on.RefusedReason);
        Assert.True(on.Selection.IsOn("x"));
        Assert.False(original.IsOn("x"));

        var off = on.Selection.Toggle("x");
        Assert.True(off.Changed);
        Assert.False(off.Selection.IsOn("x"));
        Assert.True(on.Selection.IsOn("x"));
    }

    [Fact]
    public void MatchingCard_DetectsCardAndEdits()
    {
        var catalog = Switches.CatalogOf(GamerSwitch("x", CardDefault.On), GamerSwitch("y", CardDefault.Off));
        var sel = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, DeviceProfile.Desktop);
        Assert.Equal(CardId.Gamer, sel.MatchingCard());

        var edited = sel.Toggle("y").Selection;
        Assert.Null(edited.MatchingCard());
    }

    [Fact]
    public void UnknownId_Throws()
    {
        var catalog = Switches.CatalogOf(GamerSwitch("x", CardDefault.Off));
        var sel = NitrOS.Core.Selection.Selection.FromCard(catalog, CardId.Custom, TargetMode.TunePc, DeviceProfile.Desktop);
        Assert.Throws<KeyNotFoundException>(() => sel.Toggle("nope"));
    }
}
