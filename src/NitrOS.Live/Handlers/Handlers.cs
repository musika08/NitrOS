using NitrOS.Core.Model;

namespace NitrOS.Live.Handlers;

public static class Handlers
{
    private static readonly RegistrySetHandler SetHandler = new();
    private static readonly RegistryDeleteHandler DeleteHandler = new();
    private static readonly StartupDisableHandler StartupHandler = new();
    private static readonly LaunchAtLogonHandler LaunchHandler = new();
    private static readonly ServiceStartHandler ServiceHandler = new();
    private static readonly TaskDisableHandler TaskHandler = new();
    private static readonly PowerSetHandler PowerSetter = new();
    private static readonly PowerSchemeHandler PowerSchemer = new();
    private static readonly AppxRemoveHandler AppxHandler = new();
    private static readonly CapabilityRemoveHandler CapabilityHandler = new();
    private static readonly FeatureDisableHandler FeatureHandler = new();
    private static readonly FileRemoveHandler FileHandler = new();
    private static readonly ProgramUninstallHandler ProgramHandler = new();
    private static readonly ScriptHandler ScriptRunner = new();

    /// <summary>Returns the handler for an action. Unknown types throw NotSupportedException.</summary>
    public static IActionHandler For(SwitchAction action) => action switch
    {
        RegistrySet => SetHandler,
        RegistryDelete => DeleteHandler,
        StartupDisable => StartupHandler,
        LaunchAtLogon => LaunchHandler,
        ServiceStart => ServiceHandler,
        TaskDisable => TaskHandler,
        PowerSet => PowerSetter,
        PowerScheme => PowerSchemer,
        AppxRemove => AppxHandler,
        CapabilityRemove => CapabilityHandler,
        FeatureDisable => FeatureHandler,
        FileRemove => FileHandler,
        ProgramUninstall => ProgramHandler,
        Script => ScriptRunner,
        _ => throw new NotSupportedException($"No handler for action type {action.GetType().Name}"),
    };
}
