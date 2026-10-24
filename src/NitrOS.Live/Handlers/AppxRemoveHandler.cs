using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>Removes a Store app for all users, then deprovisions it. Irreversible: the user reinstalls from the Store.</summary>
public sealed class AppxRemoveHandler : IActionHandler
{
    private static bool Present(AppxRemove a, LiveSystems s) => s.Appx.IsInstalled(a.Name) || s.Appx.IsProvisioned(a.Name);

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (AppxRemove)action;
        return Present(a, s) ? new IrreversiblePrior(a.Name, null, "Reinstall from the Microsoft Store") : new MissingPrior();
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (AppxRemove)action;
        if (!Present(a, s)) return ApplyResult.Skipped("app not installed");
        s.Appx.RemoveForAllUsers(a.Name);
        s.Appx.Deprovision(a.Name);
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s) { }

    public bool IsInEffect(SwitchAction action, LiveSystems s) => !Present((AppxRemove)action, s);
}
