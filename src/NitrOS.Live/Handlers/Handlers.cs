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

    /// <summary>Returns the handler for an action. Types without a handler yet throw NotSupportedException.</summary>
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
        _ => throw new NotSupportedException($"No handler for action type {action.GetType().Name}"),
    };
}
