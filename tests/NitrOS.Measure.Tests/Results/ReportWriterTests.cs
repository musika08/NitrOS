using NitrOS.Core.Model;
using NitrOS.Measure.Campaign;
using NitrOS.Measure.Results;

namespace NitrOS.Measure.Tests.Results;

public class ReportWriterTests
{
    [Fact]
    public void Report_ListsNotMeasuredReasons()
    {
        var baseline = new Baseline(new RunSummary(2000, 100, 50), 20, 5);
        var notMeasured = new List<(string Id, string Reason)>
        {
            ("fax-off", "not present in the test machine"),
            ("edge-remove", "the guest slept"),
        };
        var outcome = new CampaignOutcome(
            Baseline: baseline,
            Switches: new Dictionary<string, Measurement>
            {
                ["maps-updates-off"] = new Measurement(35, 1, "26100.1742", new DateOnly(2026, 10, 28))
            },
            Cards: new CardMeasurements(),
            NotMeasured: notMeasured,
            Flagged: ["edge-remove"],
            FinalBaselineDriftMb: 10);

        var state = new CampaignState("c1", "26100.1742", "gitsha", DateTimeOffset.UtcNow, [], []);
        var results = new Dictionary<string, RunResult>();

        var md = ReportWriter.Markdown(outcome, state, results);

        Assert.Contains("not present in the test machine", md);
        Assert.Contains("the guest slept", md);
        Assert.Contains("maps-updates-off", md);
        Assert.Contains("2000", md); // baseline
    }
}
