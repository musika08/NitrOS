using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

public sealed class LaunchAtLogonHandler : IActionHandler
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "NitrOS.LaunchAtLogon";

    private static string Program(LaunchAtLogon a) => Environment.ExpandEnvironmentVariables(a.Program);

    private static RegValue Wanted(LaunchAtLogon a) =>
        new(RegistryKind.String, string.IsNullOrEmpty(a.Args) ? $"\"{Program(a)}\"" : $"\"{Program(a)}\" {a.Args}");

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (LaunchAtLogon)action;
        if (!s.Files.Exists(Program(a))) return new MissingPrior();
        return new LaunchPrior(s.Registry.GetValue(LiveHive.CurrentUser, RunKey, ValueName, RegView.Default));
    }

    public ActionOutcome Apply(SwitchAction action, LiveSystems s)
    {
        var a = (LaunchAtLogon)action;
        if (!s.Files.Exists(Program(a))) return ActionOutcome.Skipped;
        s.Registry.SetValue(LiveHive.CurrentUser, RunKey, ValueName, Wanted(a), RegView.Default);
        return ActionOutcome.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not LaunchPrior p) return;
        if (p.Value is null) s.Registry.DeleteValue(LiveHive.CurrentUser, RunKey, ValueName, RegView.Default);
        else s.Registry.SetValue(LiveHive.CurrentUser, RunKey, ValueName, p.Value, RegView.Default);
    }

    public bool IsInEffect(SwitchAction action, LiveSystems s) =>
        s.Registry.GetValue(LiveHive.CurrentUser, RunKey, ValueName, RegView.Default) == Wanted((LaunchAtLogon)action);
}
