using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Tests;

public sealed class SwitchRowAppliedTests
{
    private static SwitchRow Row(bool isTuneMode, bool inEffect)
    {
        var def = TestCatalog.LoadRepo().Groups.First().Switches.First();
        return new SwitchRow(def, isOn: true, isAvailable: true, unavailableReason: null,
            isTuneMode, warnings: [], onToggle: _ => { }, currentlyInEffect: inEffect);
    }

    [Fact]
    public void Applied_OnThisPcWhenSettingIsInEffect() =>
        Assert.Equal(AppliedState.Applied, Row(isTuneMode: true, inEffect: true).Applied);

    [Fact]
    public void NotApplied_OnThisPcWhenSettingIsNotInEffect() =>
        Assert.Equal(AppliedState.NotApplied, Row(isTuneMode: true, inEffect: false).Applied);

    [Fact]
    public void None_WhenBuildingAnIso() =>
        Assert.Equal(AppliedState.None, Row(isTuneMode: false, inEffect: false).Applied);
}
