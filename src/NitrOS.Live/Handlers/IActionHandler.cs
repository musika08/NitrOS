using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

public interface IActionHandler
{
    /// <summary>Captures the target before the change. MissingPrior when the target is absent.</summary>
    PriorState Read(SwitchAction action, LiveSystems s);

    /// <summary>Makes the change. Skipped when the target is absent.</summary>
    ApplyResult Apply(SwitchAction action, LiveSystems s);

    /// <summary>Restores exactly the prior state. No-op for MissingPrior.</summary>
    void Undo(SwitchAction action, PriorState prior, LiveSystems s);

    /// <summary>True when the target currently holds the state Apply sets (used for drift detection).</summary>
    bool IsInEffect(SwitchAction action, LiveSystems s);
}
