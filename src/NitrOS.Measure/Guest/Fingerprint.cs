using NitrOS.Core.Json;
using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Systems;

namespace NitrOS.Measure.Guest;

public record Fingerprint(IReadOnlyDictionary<string, string> Entries);

public record FingerprintDiff(string Key, string Before, string After, bool Expected);

public static class FingerprintTool
{
    public static Fingerprint Capture(Catalog catalog, LiveSystems systems)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(systems);

        var entries = new Dictionary<string, string>();

        foreach (var s in catalog.Switches)
        {
            for (int i = 0; i < s.Live.Count; i++)
            {
                var action = s.Live[i];
                var key = $"{s.Id}#{i}";
                try
                {
                    var handler = Handlers.For(action);
                    var prior = handler.Read(action, systems);
                    entries[key] = CatalogJson.Write(prior);
                }
                catch (Exception ex)
                {
                    entries[key] = $"error: {ex.Message}";
                }
            }
        }

        return new Fingerprint(entries);
    }

    public static IReadOnlyList<FingerprintDiff> Compare(
        Fingerprint before,
        Fingerprint after,
        Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(catalog);

        var allKeys = before.Entries.Keys
            .Concat(after.Entries.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal);

        var diffs = new List<FingerprintDiff>();

        foreach (var key in allKeys)
        {
            before.Entries.TryGetValue(key, out var bVal);
            after.Entries.TryGetValue(key, out var aVal);

            bVal ??= "<missing>";
            aVal ??= "<missing>";

            if (bVal == aVal)
            {
                continue;
            }

            bool expected = false;
            var parts = key.Split('#');
            if (parts.Length == 2 && int.TryParse(parts[1], out int actionIdx))
            {
                var switchId = parts[0];
                var sw = catalog.Switches.FirstOrDefault(s => s.Id == switchId);
                if (sw is not null)
                {
                    if (!sw.Reversible)
                    {
                        expected = true;
                    }
                    else if (actionIdx >= 0 && actionIdx < sw.Live.Count)
                    {
                        var action = sw.Live[actionIdx];
                        expected = action is AppxRemove
                            or CapabilityRemove
                            or FileRemove
                            or ProgramUninstall
                            or Script;
                    }
                }
            }

            diffs.Add(new FingerprintDiff(key, bVal, aVal, expected));
        }

        return diffs;
    }
}
