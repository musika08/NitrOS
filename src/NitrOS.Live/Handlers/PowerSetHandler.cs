using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>
/// Writes a power setting to the scheme that is active when Apply runs. The prior state records that scheme and
/// only the sides (AC, DC) the action writes; undo puts back just those, on that same scheme.
/// </summary>
public sealed class PowerSetHandler : IActionHandler
{
    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (PowerSet)action;
        var scheme = s.Power.ActiveScheme();
        if (s.Power.Get(scheme, a.Subgroup, a.Setting) is not { } cur) return new MissingPrior();
        return new PowerPrior(scheme, a.Ac is null ? null : cur.Ac, a.Dc is null ? null : cur.Dc);
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (PowerSet)action;
        var scheme = s.Power.ActiveScheme();
        if (s.Power.Get(scheme, a.Subgroup, a.Setting) is null) return ApplyResult.Skipped("power setting not available on this PC");
        if (a.Ac is { } ac) s.Power.SetAc(scheme, a.Subgroup, a.Setting, ac);
        if (a.Dc is { } dc) s.Power.SetDc(scheme, a.Subgroup, a.Setting, dc);
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not PowerPrior p) return;
        var a = (PowerSet)action;
        if (!s.Power.Schemes().Contains(p.Scheme, StringComparer.OrdinalIgnoreCase)) return;
        var now = s.Power.Get(p.Scheme, a.Subgroup, a.Setting);
        if (p.Ac is { } ac && now?.Ac != ac) s.Power.SetAc(p.Scheme, a.Subgroup, a.Setting, ac);
        if (p.Dc is { } dc && now?.Dc != dc) s.Power.SetDc(p.Scheme, a.Subgroup, a.Setting, dc);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (PowerSet)action;
        if (s.Power.Get(s.Power.ActiveScheme(), a.Subgroup, a.Setting) is not { } cur) return false;
        return (a.Ac is null || cur.Ac == a.Ac) && (a.Dc is null || cur.Dc == a.Dc);
    }
}
