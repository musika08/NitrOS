using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>Removes a Windows capability (Features on Demand). Irreversible.</summary>
public sealed class CapabilityRemoveHandler : IActionHandler
{
    private static bool Present(string? state) =>
        state is not null && !state.Replace(" ", "").Equals("NotPresent", StringComparison.OrdinalIgnoreCase);

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (CapabilityRemove)action;
        return Present(s.Dism.CapabilityState(a.Name))
            ? new IrreversiblePrior(a.Name, null, "Add it back from Settings > Optional features")
            : new MissingPrior();
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (CapabilityRemove)action;
        if (!Present(s.Dism.CapabilityState(a.Name))) return ApplyResult.Skipped("capability not installed");
        return s.Dism.RemoveCapability(a.Name) switch
        {
            DismOutcome.Done => ApplyResult.Applied,
            DismOutcome.RebootRequired or DismOutcome.Deferred => new ApplyResult(ActionOutcome.Applied, "restart needed to finish"),
            DismOutcome.NotPresent => ApplyResult.Skipped("capability not installed"),
            _ => ApplyResult.Failed("removing the capability failed"),
        };
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s) { }

    public bool IsInEffect(SwitchAction action, LiveSystems s) =>
        !Present(s.Dism.CapabilityState(((CapabilityRemove)action).Name));
}
