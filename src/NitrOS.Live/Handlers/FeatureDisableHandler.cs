using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>Disables a Windows optional feature. Undo re-enables it when it was enabled before.</summary>
public sealed class FeatureDisableHandler : IActionHandler
{
    private static bool Is(string? state, string wanted) => string.Equals(state, wanted, StringComparison.OrdinalIgnoreCase);

    private static bool IsOff(string? state) =>
        Is(state, "Disabled") || Is(state, "DisablePending") || Is(state, "DisabledWithPayloadRemoved");

    public PriorState Read(SwitchAction action, LiveSystems s) =>
        s.Dism.FeatureState(((FeatureDisable)action).Name) is { } state ? new FeaturePrior(state) : new MissingPrior();

    /// <summary>A feature that is already off (including with its payload removed) is skipped without calling DISM.</summary>
    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (FeatureDisable)action;
        var state = s.Dism.FeatureState(a.Name);
        if (state is null) return ApplyResult.Skipped("feature not found");
        if (IsOff(state)) return ApplyResult.Skipped("already off");
        return s.Dism.DisableFeature(a.Name) switch
        {
            DismOutcome.Done => ApplyResult.Applied,
            DismOutcome.RebootRequired or DismOutcome.Deferred => new ApplyResult(ActionOutcome.Applied, "restart needed to finish"),
            DismOutcome.NotPresent => ApplyResult.Skipped("feature not found"),
            _ => ApplyResult.Failed("disabling the feature failed"),
        };
    }

    /// <summary>
    /// Throws when re-enabling fails, so the engine records "Rollback failed" and a later undo retries. Calls nothing when the
    /// feature is already enabled (or enabling).
    /// </summary>
    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not FeaturePrior { State: { } state } || !Is(state, "Enabled")) return;
        var name = ((FeatureDisable)action).Name;
        var now = s.Dism.FeatureState(name);
        if (Is(now, "Enabled") || Is(now, "EnablePending")) return;
        if (s.Dism.EnableFeature(name) == DismOutcome.Failed)
            throw new InvalidOperationException($"re-enabling the feature {name} failed");
    }

    /// <summary>Disabled, DisablePending and DisabledWithPayloadRemoved all mean the switch holds.</summary>
    public bool IsInEffect(SwitchAction action, LiveSystems s) => IsOff(s.Dism.FeatureState(((FeatureDisable)action).Name));
}
