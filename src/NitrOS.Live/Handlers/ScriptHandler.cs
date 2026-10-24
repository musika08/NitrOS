using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>Runs a script body. Undo runs UndoBody when the action has one; otherwise the action is irreversible.</summary>
public sealed class ScriptHandler : IActionHandler
{
    public PriorState Read(SwitchAction action, LiveSystems s) =>
        ((Script)action).UndoBody is null
            ? new IrreversiblePrior("script", null, "This script has no undo")
            : new ScriptPrior();

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var code = s.Scripts.Run(((Script)action).Body);
        return code == 0 ? ApplyResult.Applied : ApplyResult.Failed($"script exit code {code}");
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not ScriptPrior || ((Script)action).UndoBody is not { } undo) return;
        s.Scripts.Run(undo);
    }

    /// <summary>A script has no state to inspect, so drift cannot be detected: it always counts as in effect.</summary>
    public bool IsInEffect(SwitchAction action, LiveSystems s) => true;
}
