namespace NitrOS.Live.Systems;

public enum DismOutcome { Done, RebootRequired, Deferred, NotPresent, Failed }

public interface IDism
{
    string? FeatureState(string name);
    DismOutcome DisableFeature(string name);
    DismOutcome EnableFeature(string name);
    string? CapabilityState(string name);
    DismOutcome RemoveCapability(string name);
}
