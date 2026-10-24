using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeRestorePoint : IRestorePoint
{
    public RestorePointOutcome Outcome { get; set; } = RestorePointOutcome.Created;
    public List<string> Descriptions { get; } = [];
    /// <summary>Throws when the description equals this text.</summary>
    public string? FailOn { get; set; }

    public RestorePointOutcome Create(string description)
    {
        if (FailOn is not null && string.Equals(FailOn, description, StringComparison.Ordinal))
            throw new InvalidOperationException("FakeRestorePoint failure");
        Descriptions.Add(description);
        return Outcome;
    }
}
