using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Measure.Campaign;

namespace NitrOS.Measure.Results;

public record SvchostChoice(bool Gamer, bool Streamer)
{
    public static SvchostChoice FromCatalog(Catalog c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var s = c.Get("svchost-grouping-on");
        bool gamer = s.Cards.Gamer == CardDefault.On;
        bool streamer = s.Cards.Streamer == CardDefault.On;
        return new SvchostChoice(gamer, streamer);
    }
}

public record CampaignOutcome(
    Baseline Baseline,
    IReadOnlyDictionary<string, Measurement> Switches,
    CardMeasurements Cards,
    IReadOnlyList<(string Id, string Reason)> NotMeasured,
    IReadOnlyList<string> Flagged,
    int FinalBaselineDriftMb);

public static class ResultsCalculator
{
    public static CampaignOutcome Compute(
        CampaignState state,
        IReadOnlyDictionary<string, RunResult> results,
        Catalog current,
        DateOnly date,
        SvchostChoice svchost)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(svchost);

        foreach (var (runId, r) in results)
        {
            if (r.WindowsBuild != state.WindowsBuild)
            {
                throw new InvalidOperationException(
                    $"Run '{runId}' has build '{r.WindowsBuild}', expected campaign build '{state.WindowsBuild}'.");
            }
        }

        // Baseline runs
        if (!results.TryGetValue("baseline-1", out var b1) ||
            !results.TryGetValue("baseline-2", out var b2) ||
            !results.TryGetValue("baseline-3", out var b3))
        {
            throw new InvalidOperationException("Missing one or more required baseline runs (baseline-1, 2, 3).");
        }

        var baseline = BaselineCalc.From([b1, b2, b3]);

        var switches = new Dictionary<string, Measurement>();
        var notMeasured = new List<(string Id, string Reason)>();
        var flagged = new List<string>();

        var failedStates = state.States
            .Where(s => s.Status == RunStatus.Failed)
            .Select(s => s.RunId)
            .ToHashSet();

        foreach (var runSpec in state.Runs.Where(r => r.Kind == RunKind.Switch))
        {
            var switchId = runSpec.Ids.Count > 0 ? runSpec.Ids[0] : runSpec.RunId["switch-".Length..];

            if (failedStates.Contains(runSpec.RunId) || !results.TryGetValue(runSpec.RunId, out var runResult))
            {
                notMeasured.Add((switchId, "not measured: the run failed"));
                continue;
            }

            if (runResult.Slept)
            {
                notMeasured.Add((switchId, "not measured: the guest slept"));
                continue;
            }

            if (runResult.SwitchResults.TryGetValue(switchId, out var swResult))
            {
                if (swResult == "Skipped")
                {
                    notMeasured.Add((switchId, "not measured: not present in the test machine"));
                    continue;
                }
                if (swResult == "Failed")
                {
                    notMeasured.Add((switchId, "not measured: failed in the test machine"));
                    continue;
                }
            }

            var currentSwitch = current.Switches.FirstOrDefault(s => s.Id == switchId);
            if (currentSwitch is null)
            {
                notMeasured.Add((switchId, "not measured: removed from catalog"));
                continue;
            }

            var currentHash = DefinitionHash.Of(currentSwitch);
            if (runSpec.DefinitionHash != currentHash)
            {
                notMeasured.Add((switchId, "not measured: changed since it was measured"));
                continue;
            }

            var measurement = Savings.For(runResult, baseline, date);
            switches[switchId] = measurement;

            if (Savings.IsFlagged(runResult, baseline))
            {
                flagged.Add(switchId);
            }
        }

        // Cards
        Measurement? mRecommended = null;
        Measurement? mGamer = null;
        Measurement? mStreamer = null;
        Measurement? mMaxLean = null;

        var cardConfigs = new[]
        {
            (CardId.Recommended, "recommended", "card-recommended"),
            (CardId.Gamer, "gamer", svchost.Gamer ? "variant-gamer-svchost" : "card-gamer"),
            (CardId.Streamer, "streamer", svchost.Streamer ? "variant-streamer-svchost" : "card-streamer"),
            (CardId.MaxLean, "max-lean", "card-max-lean"),
        };

        foreach (var (cardId, slug, chosenRunId) in cardConfigs)
        {
            var chosenSpec = state.Runs.FirstOrDefault(r => r.RunId == chosenRunId);
            if (chosenSpec is null || failedStates.Contains(chosenRunId) || !results.TryGetValue(chosenRunId, out var cardResult))
            {
                notMeasured.Add((slug, $"card {slug} run failed or missing"));
                continue;
            }

            if (cardResult.Slept)
            {
                notMeasured.Add((slug, $"card {slug} run: guest slept"));
                continue;
            }

            // Check if card selection changed
            var expectedSelection = Selection.FromCard(current, cardId, TargetMode.TunePc, DeviceProfile.Desktop).On;
            var expectedIds = expectedSelection.ToList();
            if (chosenRunId.Contains("svchost"))
            {
                expectedIds = expectedIds.Append("svchost-grouping-on").Distinct().ToList();
            }
            expectedIds.Sort(StringComparer.Ordinal);

            if (!chosenSpec.Ids.SequenceEqual(expectedIds))
            {
                notMeasured.Add((slug, $"card {slug} selection changed since measured"));
                continue;
            }

            var m = Savings.For(cardResult, baseline, date);
            if (Savings.IsFlagged(cardResult, baseline))
            {
                flagged.Add($"card-{slug}");
            }

            switch (cardId)
            {
                case CardId.Recommended: mRecommended = m; break;
                case CardId.Gamer: mGamer = m; break;
                case CardId.Streamer: mStreamer = m; break;
                case CardId.MaxLean: mMaxLean = m; break;
            }
        }

        var cards = new CardMeasurements(mRecommended, mGamer, mStreamer, mMaxLean);

        int drift = 0;
        if (results.TryGetValue("baseline-final", out var bFinal))
        {
            drift = bFinal.Median.InUseMb - baseline.Value.InUseMb;
        }

        return new CampaignOutcome(baseline, switches, cards, notMeasured, flagged, drift);
    }
}
