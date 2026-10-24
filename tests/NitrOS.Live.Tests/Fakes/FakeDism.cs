using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeDism : IDism
{
    public Dictionary<string, string> Features { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Capabilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>What Disable/Enable/RemoveCapability return for a present item; the state changes only on Done.</summary>
    public DismOutcome Result { get; set; } = DismOutcome.Done;
    public string? FailOn { get; set; }

    private void Check(string name)
    {
        if (FailOn is not null && string.Equals(FailOn, name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"FakeDism failure on {name}");
    }

    public string? FeatureState(string name)
    {
        Check(name);
        return Features.TryGetValue(name, out var s) ? s : null;
    }

    public DismOutcome DisableFeature(string name) => Change(Features, name, "Disabled");

    public DismOutcome EnableFeature(string name) => Change(Features, name, "Enabled");

    public string? CapabilityState(string name)
    {
        Check(name);
        return Capabilities.TryGetValue(name, out var s) ? s : null;
    }

    public DismOutcome RemoveCapability(string name) => Change(Capabilities, name, "NotPresent");

    private DismOutcome Change(Dictionary<string, string> map, string name, string newState)
    {
        Check(name);
        if (!map.ContainsKey(name)) return DismOutcome.NotPresent;
        if (Result is DismOutcome.Done or DismOutcome.RebootRequired or DismOutcome.Deferred) map[name] = newState;
        return Result;
    }
}
