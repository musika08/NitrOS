using NitrOS.Core.Model;
using NitrOS.Core.Selecting;

namespace NitrOS.Measure.Campaign;

public static class CampaignPlanner
{
    private static readonly (CardId Id, string Slug)[] Cards =
    [
        (CardId.Recommended, "recommended"),
        (CardId.Gamer, "gamer"),
        (CardId.Streamer, "streamer"),
        (CardId.MaxLean, "max-lean"),
    ];

    public static IReadOnlyList<RunSpec> Build(Catalog catalog, DeviceProfile device)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var runs = new List<RunSpec>
        {
            new("baseline-1", RunKind.Baseline, [], null, ""),
            new("baseline-2", RunKind.Baseline, [], null, ""),
            new("baseline-3", RunKind.Baseline, [], null, ""),
        };

        foreach (var s in catalog.Switches)
        {
            if (!s.Modes.Contains(Mode.Live))
            {
                continue;
            }

            var sel = Selection.FromIds(catalog, [s.Id], TargetMode.TunePc, device);
            if (!sel.On.Contains(s.Id))
            {
                continue;
            }

            runs.Add(new RunSpec(
                $"switch-{s.Id}",
                RunKind.Switch,
                [s.Id],
                null,
                DefinitionHash.Of(s)));
        }

        var cardRunIds = new Dictionary<string, IReadOnlyList<string>>();

        foreach (var (cardId, slug) in Cards)
        {
            var sel = Selection.FromCard(catalog, cardId, TargetMode.TunePc, device);
            var sortedIds = sel.On.OrderBy(x => x, StringComparer.Ordinal).ToList();
            cardRunIds[slug] = sortedIds;

            var switches = sortedIds.Select(catalog.Get).ToList();
            runs.Add(new RunSpec(
                $"card-{slug}",
                RunKind.Card,
                sortedIds,
                slug,
                DefinitionHash.Of(switches)));
        }

        // Variants for gamer and streamer with svchost-grouping-on
        foreach (var slug in new[] { "gamer", "streamer" })
        {
            var baseIds = cardRunIds[slug];
            var variantIds = baseIds.Append("svchost-grouping-on")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            var switches = variantIds.Select(catalog.Get).ToList();

            runs.Add(new RunSpec(
                $"variant-{slug}-svchost",
                RunKind.Variant,
                variantIds,
                slug,
                DefinitionHash.Of(switches)));
        }

        runs.Add(new("baseline-final", RunKind.Baseline, [], null, ""));

        return runs;
    }
}
