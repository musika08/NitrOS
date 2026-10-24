using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>
/// Makes an existing power scheme active. It never creates a scheme: when the GUID is not in the list of
/// schemes the action is skipped. The engine orders this before power.set within a run.
/// </summary>
public sealed class PowerSchemeHandler : IActionHandler
{
    private static bool Has(LiveSystems s, string scheme) =>
        s.Power.Schemes().Contains(scheme, StringComparer.OrdinalIgnoreCase);

    public PriorState Read(SwitchAction action, LiveSystems s) =>
        Has(s, ((PowerScheme)action).Scheme) ? new SchemePrior(s.Power.ActiveScheme()) : new MissingPrior();

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var scheme = ((PowerScheme)action).Scheme;
        if (!Has(s, scheme)) return ApplyResult.Skipped("not available on this PC");
        s.Power.SetActive(scheme);
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not SchemePrior p || !Has(s, p.PreviousScheme)) return;
        if (string.Equals(s.Power.ActiveScheme(), p.PreviousScheme, StringComparison.OrdinalIgnoreCase)) return; // already active
        s.Power.SetActive(p.PreviousScheme);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s) =>
        string.Equals(s.Power.ActiveScheme(), ((PowerScheme)action).Scheme, StringComparison.OrdinalIgnoreCase);
}
