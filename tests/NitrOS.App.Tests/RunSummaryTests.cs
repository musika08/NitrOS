using NitrOS.App.Logic.Screens;
using NitrOS.Core.Model;
using NitrOS.Live.Journal;

namespace NitrOS.App.Tests;

public sealed class RunSummaryTests
{
    [Fact]
    public void RunSummary_StatesFromEntries()
    {
        var repo = TestCatalog.LoadRepo();
        var dummyAction = new RegistrySet("HKLM", @"SOFTWARE\Test", "Val", RegistryKind.Dword, "1");

        var entries = new List<JournalEntry>
        {
            // CanUndo
            new("consumer-features-off", 0, dummyAction, new MissingPrior(), ActionOutcome.Applied, null, DateTimeOffset.Now.AddMinutes(-10)),
            // Undone
            new("fax-off", 0, dummyAction, new MissingPrior(), ActionOutcome.RolledBack, null, DateTimeOffset.Now.AddMinutes(-9)),
            // CannotUndo
            new("edge-remove", 0, dummyAction, new IrreversiblePrior("edge", null, "Reinstall Edge"), ActionOutcome.Applied, null, DateTimeOffset.Now.AddMinutes(-8)),
            // NothingToUndo
            new("copilot-remove", 0, dummyAction, new MissingPrior(), ActionOutcome.Skipped, null, DateTimeOffset.Now.AddMinutes(-7))
        };

        var journal = new RunJournal(
            "20261028-120000",
            "S-1-5-21-1",
            "26100.1",
            ["consumer-features-off", "fax-off", "edge-remove", "copilot-remove"],
            entries);

        var summary = RunSummary.Build(@"C:\data\20261028-120000.json", journal, repo);

        Assert.Equal("20261028-120000", summary.RunId);
        Assert.Equal(entries[0].At, summary.When);

        var canUndo = summary.Switches.First(s => s.Id == "consumer-features-off");
        Assert.Equal(SwitchUndoState.CanUndo, canUndo.State);
        Assert.Null(canUndo.Hint);

        var undone = summary.Switches.First(s => s.Id == "fax-off");
        Assert.Equal(SwitchUndoState.Undone, undone.State);

        var cannotUndo = summary.Switches.First(s => s.Id == "edge-remove");
        Assert.Equal(SwitchUndoState.CannotUndo, cannotUndo.State);
        Assert.Equal("Reinstall Edge", cannotUndo.Hint);

        var nothing = summary.Switches.First(s => s.Id == "copilot-remove");
        Assert.Equal(SwitchUndoState.NothingToUndo, nothing.State);
    }
}
