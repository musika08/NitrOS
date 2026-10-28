namespace NitrOS.Image.Building;

using System;
using System.Collections.Generic;
using System.Linq;

public static class SwitchResults
{
    public static IReadOnlyDictionary<string, SwitchImageResult> From(
        IEnumerable<ImageEntry> entries,
        IEnumerable<string> switchIds)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(switchIds);

        var groups = entries
            .GroupBy(e => e.SwitchId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, SwitchImageResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in switchIds)
        {
            if (!groups.TryGetValue(id, out var switchEntries) || switchEntries.Count == 0)
            {
                result[id] = SwitchImageResult.Skipped;
                continue;
            }

            var outcomes = switchEntries.Select(e => e.Outcome).ToList();

            var hasFailed = outcomes.Any(o => o == ImageOutcome.Failed);
            var hasAppliedOrDeferred = outcomes.Any(o => o is ImageOutcome.Applied or ImageOutcome.Deferred);
            var allSkipped = outcomes.All(o => o == ImageOutcome.Skipped);

            if (allSkipped)
            {
                result[id] = SwitchImageResult.Skipped;
            }
            else if (hasFailed && hasAppliedOrDeferred)
            {
                result[id] = SwitchImageResult.PartlyApplied;
            }
            else if (hasFailed)
            {
                result[id] = SwitchImageResult.Failed;
            }
            else
            {
                result[id] = SwitchImageResult.Applied;
            }
        }

        return result;
    }
}
