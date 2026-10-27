using System.Text;
using NitrOS.Measure.Campaign;

namespace NitrOS.Measure.Results;

public static class ReportWriter
{
    public static string Markdown(
        CampaignOutcome outcome,
        CampaignState state,
        IReadOnlyDictionary<string, RunResult> results)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(results);

        var sb = new StringBuilder();

        sb.AppendLine("# NitrOS Measurement Campaign Report");
        sb.AppendLine();
        sb.AppendLine($"- Campaign ID: `{state.CampaignId}`");
        sb.AppendLine($"- Windows Build: `{state.WindowsBuild}`");
        sb.AppendLine($"- CLI Commit: `{state.CliCommit}`");
        sb.AppendLine($"- Date: `{DateTimeOffset.UtcNow:yyyy-MM-dd}`");
        sb.AppendLine();

        // Baseline
        sb.AppendLine("## Baseline");
        sb.AppendLine();
        sb.AppendLine($"- In-Use Memory: {outcome.Baseline.Value.InUseMb} MiB (Noise band: ±{outcome.Baseline.MemoryNoiseMb} MiB)");
        sb.AppendLine($"- Process Count: {outcome.Baseline.Value.Processes} (Noise band: ±{outcome.Baseline.ProcessNoise})");
        sb.AppendLine($"- Svchost Count: {outcome.Baseline.Value.Svchost}");
        sb.AppendLine();

        // Final baseline drift
        sb.AppendLine("## Final Baseline Drift");
        sb.AppendLine();
        bool driftFlagged = Math.Abs(outcome.FinalBaselineDriftMb) > 2 * outcome.Baseline.MemoryNoiseMb;
        sb.AppendLine($"- Drift: {outcome.FinalBaselineDriftMb} MiB {(driftFlagged ? "**(FLAGGED: exceeds 2x noise band)**" : "(OK)")}");
        sb.AppendLine();

        // Cards
        sb.AppendLine("## Cards");
        sb.AppendLine();
        sb.AppendLine("| Card | Memory Saving (MiB) | Process Reduction | Windows Build | Date |");
        sb.AppendLine("|---|---|---|---|---|");
        void AddCardRow(string name, Core.Model.Measurement? m)
        {
            if (m is not null)
            {
                sb.AppendLine($"| {name} | {m.MemoryMb} | {m.Processes} | {m.WindowsBuild} | {m.Date:yyyy-MM-dd} |");
            }
            else
            {
                sb.AppendLine($"| {name} | Not Measured | - | - | - |");
            }
        }
        AddCardRow("Recommended", outcome.Cards.Recommended);
        AddCardRow("Gamer", outcome.Cards.Gamer);
        AddCardRow("Streamer", outcome.Cards.Streamer);
        AddCardRow("Max Lean", outcome.Cards.MaxLean);
        sb.AppendLine();

        // Switches
        sb.AppendLine("## Switches");
        sb.AppendLine();
        sb.AppendLine("| Switch ID | Memory Saving (MiB) | Process Reduction | Flagged |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var (id, m) in outcome.Switches.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            string flag = outcome.Flagged.Contains(id) ? "Yes" : "No";
            sb.AppendLine($"| {id} | {m.MemoryMb} | {m.Processes} | {flag} |");
        }
        sb.AppendLine();

        // Not Measured
        sb.AppendLine("## Not Measured");
        sb.AppendLine();
        if (outcome.NotMeasured.Count > 0)
        {
            sb.AppendLine("| Item | Reason |");
            sb.AppendLine("|---|---|");
            foreach (var (id, reason) in outcome.NotMeasured)
            {
                sb.AppendLine($"| {id} | {reason} |");
            }
        }
        else
        {
            sb.AppendLine("All items measured successfully.");
        }
        sb.AppendLine();

        // Task 15 inputs: Svchost comparison
        sb.AppendLine("## Svchost Grouping Comparison (Task 15)");
        sb.AppendLine();
        void AddVariantComparison(string cardName, string baseRunId, string variantRunId)
        {
            results.TryGetValue(baseRunId, out var baseRes);
            results.TryGetValue(variantRunId, out var varRes);
            sb.AppendLine($"### {cardName}");
            if (baseRes is not null && varRes is not null)
            {
                sb.AppendLine($"- Default ({baseRunId}): In-Use {baseRes.Median.InUseMb} MiB, Processes {baseRes.Median.Processes}, Svchost {baseRes.Median.Svchost}");
                sb.AppendLine($"- With Grouping ({variantRunId}): In-Use {varRes.Median.InUseMb} MiB, Processes {varRes.Median.Processes}, Svchost {varRes.Median.Svchost}");
                sb.AppendLine($"- Delta: In-Use {baseRes.Median.InUseMb - varRes.Median.InUseMb} MiB, Processes {baseRes.Median.Processes - varRes.Median.Processes}, Svchost {baseRes.Median.Svchost - varRes.Median.Svchost}");
            }
            else
            {
                sb.AppendLine("- Runs not available for comparison.");
            }
            sb.AppendLine();
        }

        AddVariantComparison("Gamer", "card-gamer", "variant-gamer-svchost");
        AddVariantComparison("Streamer", "card-streamer", "variant-streamer-svchost");

        return sb.ToString();
    }
}
