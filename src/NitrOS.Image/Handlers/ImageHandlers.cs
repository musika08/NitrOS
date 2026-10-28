namespace NitrOS.Image.Handlers;

using System;
using NitrOS.Core.Model;

public static class ImageHandlers
{
    private static readonly AppxRemoveHandler _appx = new();
    private static readonly CapabilityRemoveHandler _capability = new();
    private static readonly FeatureDisableHandler _feature = new();
    private static readonly PackageRemoveHandler _package = new();
    private static readonly FileRemoveImageHandler _fileRemove = new();
    private static readonly RegistrySetImageHandler _registrySet = new();
    private static readonly RegistryDeleteImageHandler _registryDelete = new();
    private static readonly ServiceStartImageHandler _serviceStart = new();
    private static readonly StartupDisableImageHandler _startupDisable = new();
    private static readonly TaskDisableImageHandler _taskDisable = new();
    private static readonly PowerSetImageHandler _powerSet = new();
    private static readonly ScriptImageHandler _script = new();

    public static IImageActionHandler For(SwitchAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action switch
        {
            AppxRemove => _appx,
            CapabilityRemove => _capability,
            FeatureDisable => _feature,
            PackageRemove => _package,
            FileRemove => _fileRemove,
            RegistrySet => _registrySet,
            RegistryDelete => _registryDelete,
            ServiceStart => _serviceStart,
            StartupDisable => _startupDisable,
            TaskDisable => _taskDisable,
            PowerSet => _powerSet,
            Script => _script,

            ProgramUninstall or PowerScheme or LaunchAtLogon =>
                throw new InvalidOperationException($"live-only action in image: {action.GetType().Name}"),

            _ => throw new InvalidOperationException($"Unknown action type: {action.GetType().Name}")
        };
    }
}
