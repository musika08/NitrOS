using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

public sealed class RegistryDeleteHandler : IActionHandler
{
    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (RegistryDelete)action;
        var hive = HiveMap.Parse(a.Hive);
        if (a.Name is not null)
        {
            var v = s.Registry.GetValue(hive, a.Key, a.Name, RegView.Default);
            return v is null ? new MissingPrior() : new RegistryPrior(v);
        }
        var tree = s.Registry.ExportKey(hive, a.Key, RegView.Default);
        return tree is null ? new MissingPrior() : new RegistryTreePrior(tree, null);
    }

    public ActionOutcome Apply(SwitchAction action, LiveSystems s)
    {
        var a = (RegistryDelete)action;
        var hive = HiveMap.Parse(a.Hive);
        if (a.Name is not null)
        {
            if (s.Registry.GetValue(hive, a.Key, a.Name, RegView.Default) is null) return ActionOutcome.Skipped;
            s.Registry.DeleteValue(hive, a.Key, a.Name, RegView.Default);
            return ActionOutcome.Applied;
        }
        if (!s.Registry.KeyExists(hive, a.Key, RegView.Default)) return ActionOutcome.Skipped;
        s.Registry.DeleteKey(hive, a.Key, RegView.Default);
        return ActionOutcome.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        var a = (RegistryDelete)action;
        var hive = HiveMap.Parse(a.Hive);
        switch (prior)
        {
            case RegistryPrior { Value: { } v } when a.Name is not null:
                s.Registry.SetValue(hive, a.Key, a.Name, v, RegView.Default);
                break;
            case RegistryTreePrior { Tree: { } tree }:
                s.Registry.ImportKey(hive, tree, RegView.Default);
                break;
            case RegistryTreePrior { Value: { } tv } when a.Name is not null:
                s.Registry.SetValue(hive, a.Key, a.Name, tv, RegView.Default);
                break;
        }
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (RegistryDelete)action;
        var hive = HiveMap.Parse(a.Hive);
        return a.Name is not null
            ? s.Registry.KeyExists(hive, a.Key, RegView.Default) && s.Registry.GetValue(hive, a.Key, a.Name, RegView.Default) is null
            : !s.Registry.KeyExists(hive, a.Key, RegView.Default);
    }
}
