namespace NitrOS.App.Logic.Running;

using System;
using System.Collections.Generic;
using NitrOS.App.Logic.Ports;
using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Systems;

public sealed class LiveSwitchProbe(Catalog? catalog, Func<LiveSystems> systemsFactory) : ISwitchProbe
{
    private readonly Catalog? _catalog = catalog;
    private readonly Func<LiveSystems> _systemsFactory = systemsFactory ?? throw new ArgumentNullException(nameof(systemsFactory));
    private HashSet<string>? _cachedActive;

    public IReadOnlySet<string> GetActiveSwitchIds()
    {
        if (_cachedActive != null) return _cachedActive;

        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_catalog is null)
        {
            _cachedActive = active;
            return active;
        }

        LiveSystems? systems = null;
        try
        {
            systems = _systemsFactory();
        }
        catch
        {
            _cachedActive = active;
            return active;
        }

        foreach (var sw in _catalog.Switches)
        {
            if (CheckInEffect(sw, systems))
            {
                active.Add(sw.Id);
            }
        }

        _cachedActive = active;
        return active;
    }

    public bool IsInEffect(SwitchDef sw)
    {
        ArgumentNullException.ThrowIfNull(sw);
        var active = GetActiveSwitchIds();
        return active.Contains(sw.Id);
    }

    private static bool CheckInEffect(SwitchDef sw, LiveSystems systems)
    {
        if (sw.Live.Count == 0) return false;
        foreach (var action in sw.Live)
        {
            try
            {
                if (!Handlers.For(action).IsInEffect(action, systems))
                    return false;
            }
            catch
            {
                return false;
            }
        }
        return true;
    }
}

public sealed class FakeSwitchProbe(IEnumerable<string>? activeIds = null) : ISwitchProbe
{
    private readonly HashSet<string> _active = new(activeIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

    public bool IsInEffect(SwitchDef sw) => _active.Contains(sw.Id);
    public IReadOnlySet<string> GetActiveSwitchIds() => _active;

    public static readonly FakeSwitchProbe Empty = new();
}
