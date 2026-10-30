using NitrOS.App.Logic.Screens;
using NitrOS.Core.Model;

namespace NitrOS.App.Tests;

/// <summary>The status chip compares the switch with what the chosen profile recommends.</summary>
public sealed class SwitchRowRecommendTests
{
    private static SwitchRow Row(bool isOn, CardDefault? recommended, bool available = true)
    {
        var def = TestCatalog.LoadRepo().Groups.First().Switches.First();
        return new SwitchRow(def, isOn, available, unavailableReason: null,
            isTuneMode: true, warnings: [], onToggle: _ => { }, recommended: recommended);
    }

    [Fact]
    public void Matches_WhenSwitchIsOnAndProfileRecommendsOn() =>
        Assert.Equal(RecommendState.Matches, Row(isOn: true, CardDefault.On).Recommend);

    [Fact]
    public void Differs_WhenSwitchIsOffButProfileRecommendsOn() =>
        Assert.Equal(RecommendState.Differs, Row(isOn: false, CardDefault.On).Recommend);

    [Fact]
    public void Matches_WhenSwitchIsOffAndProfileRecommendsOff() =>
        Assert.Equal(RecommendState.Matches, Row(isOn: false, CardDefault.Off).Recommend);

    [Fact]
    public void Differs_WhenSwitchIsOnButProfileRecommendsOff() =>
        Assert.Equal(RecommendState.Differs, Row(isOn: true, CardDefault.Off).Recommend);

    [Fact]
    public void None_WhenProfileHasNoRecommendation()
    {
        Assert.Equal(RecommendState.None, Row(isOn: true, null).Recommend);
        Assert.Equal(RecommendState.None, Row(isOn: true, CardDefault.Pending).Recommend);
    }

    [Fact]
    public void None_WhenSwitchIsUnavailable() =>
        Assert.Equal(RecommendState.None, Row(isOn: false, CardDefault.On, available: false).Recommend);

    [Fact]
    public void FlippingTheSwitchUpdatesTheState()
    {
        var row = Row(isOn: false, CardDefault.On);
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.IsOn = true;

        Assert.Equal(RecommendState.Matches, row.Recommend);
        Assert.Contains(nameof(SwitchRow.Recommend), changed);
    }
}
