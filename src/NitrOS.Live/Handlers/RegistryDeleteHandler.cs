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

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (RegistryDelete)action;
        var hive = HiveMap.Parse(a.Hive);
        if (a.Name is not null)
        {
            if (s.Registry.GetValue(hive, a.Key, a.Name, RegView.Default) is null) return ApplyResult.Skipped("target not found");
            s.Registry.DeleteValue(hive, a.Key, a.Name, RegView.Default);
            return ApplyResult.Applied;
        }
        if (!s.Registry.KeyExists(hive, a.Key, RegView.Default)) return ApplyResult.Skipped("target not found");
        s.Registry.DeleteKey(hive, a.Key, RegView.Default);
        return ApplyResult.Applied;
    }

    /// <summary>Puts back the deleted value or key. Writes nothing when it is already there as it was.</summary>
    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        var a = (RegistryDelete)action;
        var hive = HiveMap.Parse(a.Hive);
        switch (prior)
        {
            case RegistryPrior { Value: { } v } when a.Name is not null:
                RestoreValue(s, hive, a.Key, a.Name, v);
                break;
            case RegistryTreePrior { Tree: { } tree }:
                if (s.Registry.ExportKey(hive, tree.Key, RegView.Default) is { } now && Holds(now, tree)) return;
                s.Registry.ImportKey(hive, tree, RegView.Default);
                break;
            case RegistryTreePrior { Value: { } tv } when a.Name is not null:
                RestoreValue(s, hive, a.Key, a.Name, tv);
                break;
        }
    }

    private static void RestoreValue(LiveSystems s, LiveHive hive, string key, string name, RegValue v)
    {
        if (s.Registry.GetValue(hive, key, name, RegView.Default) == v) return;
        s.Registry.SetValue(hive, key, name, v, RegView.Default);
    }

    /// <summary>True when every value and subkey of <paramref name="prior"/> is in <paramref name="now"/> with the same data (Import writes nothing else).</summary>
    private static bool Holds(RegTree now, RegTree prior)
    {
        foreach (var pv in prior.Values)
            if (!now.Values.Any(nv => string.Equals(nv.Name, pv.Name, StringComparison.OrdinalIgnoreCase) && nv.Value == pv.Value))
                return false;
        foreach (var ps in prior.SubKeys)
        {
            var match = now.SubKeys.FirstOrDefault(ns => string.Equals(ns.Key, ps.Key, StringComparison.OrdinalIgnoreCase));
            if (match is null || !Holds(match, ps)) return false;
        }
        return true;
    }

    /// <summary>A named value counts as deleted when it is absent, including when its whole key is gone.</summary>
    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (RegistryDelete)action;
        var hive = HiveMap.Parse(a.Hive);
        return a.Name is not null
            ? s.Registry.GetValue(hive, a.Key, a.Name, RegView.Default) is null
            : !s.Registry.KeyExists(hive, a.Key, RegView.Default);
    }
}
