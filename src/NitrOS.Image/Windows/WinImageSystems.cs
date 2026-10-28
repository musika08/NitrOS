namespace NitrOS.Image.Windows;

using NitrOS.Image.Systems;
using NitrOS.Live.Windows;

public static class WinImageSystems
{
    public static ImageSystems Create(string? oscdimgPath = null, string? dismLogDir = null)
    {
        var servicing = new WinImageServicing(dismLogDir);
        var registry = new WinOfflineRegistry();
        var files = new WinImageFiles();
        var media = new WinSourceMedia();
        var iso = oscdimgPath != null ? new WinIsoWriter(oscdimgPath) : null!;
        var host = new WinHostFacts();
        var power = new WinPowerSource();

        return new ImageSystems(servicing, registry, files, media, iso, host, power);
    }
}
