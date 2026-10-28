using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;
using NitrOS.Updates.Windows;

namespace NitrOS.Updates.Sources;

public static class SourceSet
{
    public static IReadOnlyList<IUpdateSource> Default(
        IWindowsUpdateAgent agent,
        HttpClient http,
        FirmwareMap map)
    {
        var list = new List<IUpdateSource>
        {
            // Windows Update first
            new WindowsUpdateSource(agent, map),

            // Maker listing sources
            new AsusSource(http, map),

            // GPU vendors (all Page only per Task 1 research)
            new GpuPageSource(GpuVendor.Amd),
            new GpuPageSource(GpuVendor.Nvidia),
            new GpuPageSource(GpuVendor.Intel),

            // Support-page fallback sources for makers without live listing
            new SupportPageSource(MakerId.Msi),
            new SupportPageSource(MakerId.Lenovo),
            new SupportPageSource(MakerId.Dell),
            new SupportPageSource(MakerId.Hp)
        };

        return list;
    }
}
