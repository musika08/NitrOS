using NitrOS.Core.Model;

namespace NitrOS.Core.Selecting;

public enum WarningKind { Breaks, Dependency, Irreversible }

public record Warning(WarningKind Kind, string SwitchId, string? RelatedId, string Message);

public static class WarningEngine
{
    public static IReadOnlyList<Warning> For(Selection selection)
    {
        var warnings = new List<Warning>();
        foreach (var t in selection.Catalog.Switches)
        {
            if (!selection.IsOn(t.Id)) continue;

            foreach (var sentence in t.Breaks)
                warnings.Add(new Warning(WarningKind.Breaks, t.Id, null, sentence));

            foreach (var s in selection.Catalog.Switches)
            {
                if (s.Requires.Contains(t.Id) && !selection.IsOn(s.Id))
                    warnings.Add(new Warning(WarningKind.Dependency, t.Id, s.Id,
                        $"{t.Feature} is needed by {s.Feature}, which you are keeping."));
            }

            if (!t.Reversible && selection.Mode == TargetMode.TunePc)
                warnings.Add(new Warning(WarningKind.Irreversible, t.Id, null,
                    "Can't be undone without reinstalling."));
        }
        return warnings;
    }
}
