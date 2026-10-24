using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

public sealed class RegistrySetHandler : IActionHandler
{
    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (RegistrySet)action;
        return new RegistryPrior(s.Registry.GetValue(HiveMap.Parse(a.Hive), a.Key, a.Name, RegView.Default));
    }

    public ActionOutcome Apply(SwitchAction action, LiveSystems s)
    {
        var a = (RegistrySet)action;
        s.Registry.SetValue(HiveMap.Parse(a.Hive), a.Key, a.Name, new RegValue(a.Kind, a.Data), RegView.Default);
        return ActionOutcome.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not RegistryPrior p) return;
        var a = (RegistrySet)action;
        var hive = HiveMap.Parse(a.Hive);
        if (p.Value is null) s.Registry.DeleteValue(hive, a.Key, a.Name, RegView.Default);
        else s.Registry.SetValue(hive, a.Key, a.Name, p.Value, RegView.Default);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (RegistrySet)action;
        return s.Registry.GetValue(HiveMap.Parse(a.Hive), a.Key, a.Name, RegView.Default) == new RegValue(a.Kind, a.Data);
    }
}
