using NitrOS.Core.Selecting;

namespace NitrOS.App.Logic.Formatting;

public static class TotalsText
{
    public static string For(Totals t)
    {
        ArgumentNullException.ThrowIfNull(t);

        if (t.MeasuredAsWhole)
            return $"About {Plain.Memory(t.MemoryMbSaved)} less memory in use and {t.ProcessesSaved} fewer processes (measured for the whole card, estimate)";

        if (t.MemoryMbSaved == 0 && t.ProcessesSaved == 0 && t.UnmeasuredCount > 0)
            return $"Memory saved: not measured yet ({Plain.Count(t.UnmeasuredCount, "switch", "switches")})";

        var baseText = $"About {Plain.Memory(t.MemoryMbSaved)} less memory in use and {t.ProcessesSaved} fewer processes (estimate)";
        if (t.UnmeasuredCount > 0)
            return $"{baseText}, {Plain.Count(t.UnmeasuredCount, "switch not measured yet", "switches not measured yet")}";

        return baseText;
    }
}
