using NitrOS.Live.Journal;

namespace NitrOS.Live.Handlers;

public sealed record ApplyResult(ActionOutcome Outcome, string? Detail = null)
{
    public static readonly ApplyResult Applied = new(ActionOutcome.Applied);
    public static ApplyResult Skipped(string? detail = null) => new(ActionOutcome.Skipped, detail);
    public static ApplyResult Failed(string detail) => new(ActionOutcome.Failed, detail);
}
