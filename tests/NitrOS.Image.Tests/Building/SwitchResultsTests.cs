namespace NitrOS.Image.Tests.Building;

using System;
using NitrOS.Core.Model;
using NitrOS.Image.Building;

public sealed class SwitchResultsTests
{
    [Fact]
    public void SwitchResults_AllFourShapes()
    {
        var dummyAction = new FileRemove("test");
        var now = DateTimeOffset.UtcNow;

        var entries = new ImageEntry[]
        {
            // sw1: all Skipped
            new("sw1", 0, dummyAction, ImagePhase.Files, ImageOutcome.Skipped, null, now),
            new("sw1", 1, dummyAction, ImagePhase.Files, ImageOutcome.Skipped, null, now),

            // sw2: Failed with an Applied -> PartlyApplied
            new("sw2", 0, dummyAction, ImagePhase.Files, ImageOutcome.Applied, null, now),
            new("sw2", 1, dummyAction, ImagePhase.Files, ImageOutcome.Failed, "denied", now),

            // sw3: only Failed -> Failed
            new("sw3", 0, dummyAction, ImagePhase.Files, ImageOutcome.Failed, "error", now),

            // sw4: all Applied -> Applied
            new("sw4", 0, dummyAction, ImagePhase.Files, ImageOutcome.Applied, null, now),

            // sw5: no entries -> Skipped
        };

        var dict = SwitchResults.From(entries, ["sw1", "sw2", "sw3", "sw4", "sw5"]);

        Assert.Equal(SwitchImageResult.Skipped, dict["sw1"]);
        Assert.Equal(SwitchImageResult.PartlyApplied, dict["sw2"]);
        Assert.Equal(SwitchImageResult.Failed, dict["sw3"]);
        Assert.Equal(SwitchImageResult.Applied, dict["sw4"]);
        Assert.Equal(SwitchImageResult.Skipped, dict["sw5"]);
    }

    [Fact]
    public void SwitchResults_DeferredCountsAsApplied()
    {
        var dummyAction = new FileRemove("test");
        var now = DateTimeOffset.UtcNow;

        var entries = new ImageEntry[]
        {
            // Deferred only -> Applied
            new("sw1", 0, dummyAction, ImagePhase.Files, ImageOutcome.Deferred, null, now),

            // Deferred + Applied -> Applied
            new("sw2", 0, dummyAction, ImagePhase.Files, ImageOutcome.Deferred, null, now),
            new("sw2", 1, dummyAction, ImagePhase.Files, ImageOutcome.Applied, null, now),

            // Deferred + Failed -> PartlyApplied
            new("sw3", 0, dummyAction, ImagePhase.Files, ImageOutcome.Deferred, null, now),
            new("sw3", 1, dummyAction, ImagePhase.Files, ImageOutcome.Failed, "err", now),
        };

        var dict = SwitchResults.From(entries, ["sw1", "sw2", "sw3"]);

        Assert.Equal(SwitchImageResult.Applied, dict["sw1"]);
        Assert.Equal(SwitchImageResult.Applied, dict["sw2"]);
        Assert.Equal(SwitchImageResult.PartlyApplied, dict["sw3"]);
    }
}
