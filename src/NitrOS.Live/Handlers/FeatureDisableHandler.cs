using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>Disables a Windows optional feature. Undo re-enables it when it was enabled before.</summary>
public sealed class FeatureDisableHandler : IActionHandler
{
    private static bool Is(string? state, string wanted) => string.Equals(state, wanted, StringComparison.OrdinalIgnoreCase);

    public PriorState Read(SwitchAction action, LiveSystems s) =>
        s.Dism.FeatureState(((FeatureDisable)action).Name) is { } state ? new FeaturePrior(state) : new MissingPrior();

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (FeatureDisable)action;
        if (s.Dism.FeatureState(a.Name) is null) return ApplyResult.Skipped("feature not found");
        return s.Dism.DisableFeature(a.Name) switch
        {
            DismOutcome.Done => ApplyResult.Applied,
            DismOutcome.RebootRequired or DismOutcome.Deferred => new ApplyResult(ActionOutcome.Applied, "restart needed to finish"),
            DismOutcome.NotPresent => ApplyResult.Skipped("feature not found"),
            _ => ApplyResult.Failed("disabling the feature failed"),
        };
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not FeaturePrior { State: { } state } || !Is(state, "Enabled")) return;
        s.Dism.EnableFeature(((FeatureDisable)action).Name);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s) =>
        Is(s.Dism.FeatureState(((FeatureDisable)action).Name), "Disabled");
}
