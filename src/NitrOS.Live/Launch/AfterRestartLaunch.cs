using NitrOS.Core.Model;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Launch;

public sealed class AfterRestartLaunch(IRegistry registry)
{
    public const string Key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    public const string ValueName = "NitrOS.HealthCheck";

    private readonly IRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public bool TryRegister(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        try
        {
            if (!_registry.CanWrite(LiveHive.LocalMachine, Key, RegView.Default))
                return false;

            _registry.SetValue(LiveHive.LocalMachine, Key, ValueName, new RegValue(RegistryKind.String, $"\"{exePath}\" --after-restart"), RegView.Default);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Clear()
    {
        try
        {
            _registry.DeleteValue(LiveHive.LocalMachine, Key, ValueName, RegView.Default);
        }
        catch (Exception)
        {
            // ignore refusal
        }
    }
}
