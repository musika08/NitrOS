namespace NitrOS.Image.Handlers;

using System.Collections.Generic;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Offline;
using NitrOS.Image.Systems;

public sealed class ImageContext
{
    public ImageContext(ImageSystems systems, string mountDir, int currentControlSet, FirstBootPlan firstBoot, string switchId = "")
    {
        Systems = systems;
        MountDir = mountDir;
        CurrentControlSet = currentControlSet;
        FirstBoot = firstBoot;
        SwitchId = switchId;
    }

    public ImageSystems Systems { get; }
    public string MountDir { get; }
    public int CurrentControlSet { get; }
    public FirstBootPlan FirstBoot { get; }
    public string SwitchId { get; set; }
    public IReadOnlyDictionary<OfflineHive, IOfflineHive>? Hives { get; init; }
}
