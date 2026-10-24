using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>
/// Runs the uninstaller of every installed program matching the name and publisher. A match whose signer does not
/// match the publisher is refused. The action's args are passed to <see cref="IUninstaller.Run"/>, which appends them
/// after a space to the QuietUninstallString (else the UninstallString). Irreversible.
/// </summary>
public sealed class ProgramUninstallHandler : IActionHandler
{
    private const int RestartExitCode = 3010;

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (ProgramUninstall)action;
        var found = s.Uninstaller.Find(a.DisplayName, a.Publisher);
        if (found.Count == 0) return new MissingPrior();
        return new IrreversiblePrior(
            string.Join("; ", found.Select(e => e.DisplayName)),
            found[0].Version,
            $"Download it again from the website of {a.Publisher}");
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (ProgramUninstall)action;
        var found = s.Uninstaller.Find(a.DisplayName, a.Publisher);
        if (found.Count == 0) return ApplyResult.Skipped("program not installed");

        var failures = new List<string>();
        var restart = false;
        for (var i = 0; i < found.Count; i++)
        {
            var e = found[i];
            // A suite's first uninstaller often removes its other parts too: a later match that is gone is already removed.
            if (i > 0 && !s.Uninstaller.Find(a.DisplayName, a.Publisher)
                    .Any(x => string.Equals(x.KeyPath, e.KeyPath, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!s.Uninstaller.SignerMatches(e, a.Publisher))
            {
                failures.Add($"{e.DisplayName}: signature check failed");
                continue;
            }
            if (string.IsNullOrWhiteSpace(e.QuietUninstallString) && string.IsNullOrWhiteSpace(e.UninstallString))
            {
                failures.Add($"{e.DisplayName}: no uninstall command");
                continue;
            }
            var code = s.Uninstaller.Run(e, a.Args);
            if (code == RestartExitCode) restart = true;
            else if (code != 0) failures.Add($"{e.DisplayName}: exit code {code}");
        }
        if (failures.Count > 0) return ApplyResult.Failed(string.Join("; ", failures));
        return restart ? new ApplyResult(ActionOutcome.Applied, "restart needed to finish") : ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s) { }

    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (ProgramUninstall)action;
        return s.Uninstaller.Find(a.DisplayName, a.Publisher).Count == 0;
    }
}
