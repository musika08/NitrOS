namespace NitrOS.Image.Building;

using System;
using NitrOS.Core.Model;

public static class ImagePhases
{
    public static ImagePhase For(SwitchAction a)
    {
        ArgumentNullException.ThrowIfNull(a);

        return a switch
        {
            AppxRemove => ImagePhase.Dism,
            CapabilityRemove => ImagePhase.Dism,
            FeatureDisable => ImagePhase.Dism,
            PackageRemove => ImagePhase.Dism,

            FileRemove => ImagePhase.Files,

            RegistrySet => ImagePhase.Registry,
            RegistryDelete => ImagePhase.Registry,
            ServiceStart => ImagePhase.Registry,
            StartupDisable => ImagePhase.Registry,

            TaskDisable => ImagePhase.FirstBoot,
            PowerSet => ImagePhase.FirstBoot,
            Script => ImagePhase.FirstBoot,

            ProgramUninstall or PowerScheme or LaunchAtLogon =>
                throw new InvalidOperationException($"live-only action in image: {a.GetType().Name}"),

            _ => throw new InvalidOperationException($"Unknown action type: {a.GetType().Name}")
        };
    }
}
