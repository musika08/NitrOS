namespace NitrOS.App.Logic.Running;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Image.Iso;
using NitrOS.Image.Systems;
using NitrOS.Image.Windows;
using NitrOS.Live.Systems;

public sealed class ImageIsoBackend : IIsoBackend
{
    private readonly ImageSystems _systems;
    private readonly Catalog _catalog;
    private readonly DeviceProfile _device;
    private readonly IOscdimg _oscdimg;
    private readonly ImageBuilder _builder;

    public ImageIsoBackend(ImageSystems systems, Catalog catalog, DeviceProfile device, IOscdimg oscdimg)
    {
        _systems = systems ?? throw new ArgumentNullException(nameof(systems));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _device = device;
        _oscdimg = oscdimg ?? throw new ArgumentNullException(nameof(oscdimg));
        _builder = new ImageBuilder(_systems, _catalog, _device, folder => new BuildStore(folder), _oscdimg);
    }

    public static ImageIsoBackend CreateReal(Catalog catalog, DeviceProfile device)
    {
        var toolsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "tools");
        var oscdimg = new Oscdimg(toolsDir, new OscdimgDownloader(new HttpGet()));
        var systems = WinImageSystems.Create(Path.Combine(toolsDir, "oscdimg.exe"));
        return new ImageIsoBackend(systems, catalog, device, oscdimg);
    }

    public IReadOnlyList<ImageEdition> Editions(string source)
    {
        using var mount = _systems.Media.Open(source);
        var installWim = Path.Combine(mount.Root, "sources", "install.wim");
        if (!_systems.Files.Exists(installWim))
        {
            installWim = Path.Combine(mount.Root, "sources", "install.esd");
        }
        return _systems.Dism.Editions(installWim);
    }

    public BuildState? FindResumable(string outputIso) => _builder.FindResumable(outputIso);

    public BuildState Start(BuildRequest request) => _builder.Start(request);

    public BuildResult Run(BuildState state, IProgress<BuildProgress> progress, CancellationToken ct) =>
        _builder.Run(state, progress, ct);

    public void Discard(BuildState state) => _builder.Discard(state);

    public string? HostDismReason() => BuildChecks.HostDism(_systems.Host);

    public PowerState Power() => _systems.Power.Read();

    public string? PathsReason(BuildRequest request) => BuildChecks.Paths(request, _systems.Files);
}
