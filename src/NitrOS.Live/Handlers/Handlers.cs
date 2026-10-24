using NitrOS.Core.Model;

namespace NitrOS.Live.Handlers;

public static class Handlers
{
    private static readonly RegistrySetHandler SetHandler = new();
    private static readonly RegistryDeleteHandler DeleteHandler = new();
    private static readonly StartupDisableHandler StartupHandler = new();
    private static readonly LaunchAtLogonHandler LaunchHandler = new();

    /// <summary>Returns the handler for an action. Types without a handler yet throw NotSupportedException.</summary>
    public static IActionHandler For(SwitchAction action) => action switch
    {
        RegistrySet => SetHandler,
        RegistryDelete => DeleteHandler,
        StartupDisable => StartupHandler,
        LaunchAtLogon => LaunchHandler,
        _ => throw new NotSupportedException($"No handler for action type {action.GetType().Name}"),
    };
}
