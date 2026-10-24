using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>Disables a scheduled task. Undo puts back whatever enabled state it had.</summary>
public sealed class TaskDisableHandler : IActionHandler
{
    public PriorState Read(SwitchAction action, LiveSystems s) =>
        s.Tasks.Get(((TaskDisable)action).Path) is { } t ? new TaskPrior(t.Enabled) : new MissingPrior();

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var path = ((TaskDisable)action).Path;
        if (s.Tasks.Get(path) is null) return ApplyResult.Skipped("task not found");
        s.Tasks.SetEnabled(path, false);
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not TaskPrior p) return;
        var path = ((TaskDisable)action).Path;
        if (s.Tasks.Get(path) is null) return;
        s.Tasks.SetEnabled(path, p.Enabled);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s) =>
        s.Tasks.Get(((TaskDisable)action).Path) is { Enabled: false };
}
