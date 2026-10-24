using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>Deletes a file or folder (taking ownership first). Environment variables in the path are expanded. Irreversible.</summary>
public sealed class FileRemoveHandler : IActionHandler
{
    private static string Expanded(FileRemove a) => Environment.ExpandEnvironmentVariables(a.Path);

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var path = Expanded((FileRemove)action);
        return s.Files.Exists(path) ? new IrreversiblePrior(path, null, "Deleted files cannot be restored by NitrOS") : new MissingPrior();
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var path = Expanded((FileRemove)action);
        if (!s.Files.Exists(path)) return ApplyResult.Skipped("file not found");
        s.Files.TakeOwnershipAndDelete(path);
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s) { }

    public bool IsInEffect(SwitchAction action, LiveSystems s) => !s.Files.Exists(Expanded((FileRemove)action));
}
