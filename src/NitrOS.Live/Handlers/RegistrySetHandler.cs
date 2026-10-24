using System.Globalization;
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

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (RegistrySet)action;
        s.Registry.SetValue(HiveMap.Parse(a.Hive), a.Key, a.Name, new RegValue(a.Kind, a.Data), RegView.Default);
        return ApplyResult.Applied;
    }

    /// <summary>Puts back the prior value (or its absence). Writes nothing when the value already is the prior.</summary>
    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not RegistryPrior p) return;
        var a = (RegistrySet)action;
        var hive = HiveMap.Parse(a.Hive);
        if (s.Registry.GetValue(hive, a.Key, a.Name, RegView.Default) == p.Value) return;
        if (p.Value is null) s.Registry.DeleteValue(hive, a.Key, a.Name, RegView.Default);
        else s.Registry.SetValue(hive, a.Key, a.Name, p.Value, RegView.Default);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (RegistrySet)action;
        return s.Registry.GetValue(HiveMap.Parse(a.Hive), a.Key, a.Name, RegView.Default) == Normalise(new RegValue(a.Kind, a.Data));
    }

    /// <summary>
    /// The catalog value in the form the registry adapter returns it: dwords and qwords in decimal (the catalog may write
    /// 0x hex), binary as uppercase hex. Data that does not parse is left as it is.
    /// </summary>
    internal static RegValue Normalise(RegValue v)
    {
        switch (v.Kind)
        {
            case RegistryKind.Dword or RegistryKind.Qword when v.Data.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && ulong.TryParse(v.Data.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var n):
                return v with { Data = n.ToString(CultureInfo.InvariantCulture) };
            case RegistryKind.Binary:
                return v with { Data = v.Data.ToUpperInvariant() };
            default:
                return v;
        }
    }
}
