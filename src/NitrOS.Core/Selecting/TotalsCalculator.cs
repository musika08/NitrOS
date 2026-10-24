using NitrOS.Core.Model;

namespace NitrOS.Core.Selecting;

public record Totals(
    int MemoryMbSaved,
    int ProcessesSaved,
    bool MeasuredAsWhole,
    int UnmeasuredCount,
    IReadOnlyList<string> StopsWorking);

public static class TotalsCalculator
{
    public static Totals For(Selection selection)
    {
        var on = selection.Catalog.Switches.Where(s => selection.IsOn(s.Id)).ToList();
        var stops = on.SelectMany(s => s.Breaks).Distinct().ToList();

        var card = selection.MatchingCard();
        var whole = card is { } id ? selection.Catalog.Cards.For(id) : null;
        if (whole is not null)
            return new Totals(whole.MemoryMb, whole.Processes, true, 0, stops);

        var measured = on.Where(s => s.Measured is not null).Select(s => s.Measured!).ToList();
        return new Totals(
            measured.Sum(m => m.MemoryMb),
            measured.Sum(m => m.Processes),
            false,
            on.Count - measured.Count,
            stops);
    }
}
