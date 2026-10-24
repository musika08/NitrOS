using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.Selecting;

public class TotalsCalculatorTests
{
    private static readonly DateOnly Day = new(2026, 10, 24);

    private static Measurement M(int mb, int procs) => new(mb, procs, "26300", Day);

    private static SwitchDef GamerOn(string id, Measurement? measured = null, params string[] breaks) =>
        Switches.Make(id, b =>
        {
            b.Cards = new CardDefaults
            {
                Recommended = CardDefault.Off,
                Gamer = CardDefault.On,
                Streamer = CardDefault.Off,
                MaxLean = CardDefault.Off,
            };
            b.Measured = measured;
            b.Breaks = breaks;
        });

    private static Selection Gamer(Catalog catalog) =>
        Selection.FromCard(catalog, CardId.Gamer, TargetMode.TunePc, DeviceProfile.Desktop);

    [Fact]
    public void Totals_SumsSwitchMeasurements()
    {
        var catalog = Switches.CatalogOf(GamerOn("a", M(100, 3)), GamerOn("b", M(50, 1)));
        var totals = TotalsCalculator.For(Gamer(catalog));
        Assert.Equal(150, totals.MemoryMbSaved);
        Assert.Equal(4, totals.ProcessesSaved);
        Assert.False(totals.MeasuredAsWhole);
        Assert.Equal(0, totals.UnmeasuredCount);
    }

    [Fact]
    public void Totals_UsesCardMeasurementWhenSelectionMatchesCard()
    {
        var catalog = Switches.CatalogOf(GamerOn("a", M(100, 3)), GamerOn("b", M(50, 1)));
        catalog = catalog with { Cards = new CardMeasurements(Gamer: M(900, 40)) };
        var totals = TotalsCalculator.For(Gamer(catalog));
        Assert.Equal(900, totals.MemoryMbSaved);
        Assert.Equal(40, totals.ProcessesSaved);
        Assert.True(totals.MeasuredAsWhole);
        Assert.Equal(0, totals.UnmeasuredCount);
    }

    [Fact]
    public void Totals_FallsBackToSumAfterEdit()
    {
        var catalog = Switches.CatalogOf(GamerOn("a", M(100, 3)), GamerOn("b", M(50, 1)), GamerOn("c", M(10, 1)));
        catalog = catalog with { Cards = new CardMeasurements(Gamer: M(900, 40)) };
        var edited = Gamer(catalog).Toggle("c").Selection;
        var totals = TotalsCalculator.For(edited);
        Assert.Equal(150, totals.MemoryMbSaved);
        Assert.Equal(4, totals.ProcessesSaved);
        Assert.False(totals.MeasuredAsWhole);
    }

    [Fact]
    public void Totals_CountsUnmeasuredSwitches()
    {
        var catalog = Switches.CatalogOf(GamerOn("a", M(100, 3)), GamerOn("b"), GamerOn("c"));
        var totals = TotalsCalculator.For(Gamer(catalog));
        Assert.Equal(2, totals.UnmeasuredCount);
        Assert.Equal(100, totals.MemoryMbSaved);
    }

    [Fact]
    public void Totals_StopsWorkingIsDistinctInOrder()
    {
        var catalog = Switches.CatalogOf(
            GamerOn("a", null, "Widgets stop working.", "Search is slower."),
            GamerOn("b", null, "Widgets stop working.", "Printing fails."));
        var totals = TotalsCalculator.For(Gamer(catalog));
        Assert.Equal(new[] { "Widgets stop working.", "Search is slower.", "Printing fails." }, totals.StopsWorking.ToArray());
    }
}
