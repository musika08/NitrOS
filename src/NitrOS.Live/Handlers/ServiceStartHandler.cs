using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>
/// Sets a service's start type. For a per-user template (CDPUserSvc, BluetoothUserService) every per-user instance
/// is changed as well, and all of them are saved in the prior state.
/// </summary>
public sealed class ServiceStartHandler : IActionHandler
{
    private static List<ServiceStartEntry> Current(ServiceStart a, LiveSystems s)
    {
        var result = new List<ServiceStartEntry>();
        foreach (var name in new[] { a.Service }.Concat(s.Services.PerUserInstances(a.Service)))
            if (s.Services.Get(name) is { } info) result.Add(new ServiceStartEntry(name, info.Start));
        return result;
    }

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var current = Current((ServiceStart)action, s);
        return current.Count == 0 ? new MissingPrior() : new ServicePrior(current);
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (ServiceStart)action;
        var current = Current(a, s);
        if (current.Count == 0) return ApplyResult.Skipped("service not found");
        foreach (var e in current) s.Services.Set(e.Name, a.Start);
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not ServicePrior p) return;
        foreach (var e in p.Services)
            if (s.Services.Get(e.Name) is { } now && now.Start != e.Start) s.Services.Set(e.Name, e.Start);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (ServiceStart)action;
        var current = Current(a, s);
        return current.Count > 0 && current.All(e => e.Start == a.Start);
    }
}
