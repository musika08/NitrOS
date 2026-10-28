using NitrOS.Updates.Model;

namespace NitrOS.Updates.Sources;

public sealed class GpuPageSource(GpuVendor vendor) : IUpdateSource
{
    public GpuVendor Vendor => vendor;

    public string Id => $"gpu-page-{vendor.ToString().ToLowerInvariant()}";
    public string Name => vendor.ToString().ToUpperInvariant();

    public bool AppliesTo(DeviceInfo device)
    {
        return device.Gpus.Any(g =>
            g.HardwareIds.Any(h => GpuVendors.FromHardwareId(h) == vendor));
    }

    public Task<SourceResult> CheckAsync(DeviceInfo device, CancellationToken ct)
    {
        if (!AppliesTo(device))
        {
            return Task.FromResult(new SourceResult(SourceStatus.NotForThisDevice, Array.Empty<UpdateItem>(), null, null));
        }

        var matchingGpus = device.Gpus.Where(g =>
            g.HardwareIds.Any(h => GpuVendors.FromHardwareId(h) == vendor)).ToList();

        var page = SupportPages.ForGpu(vendor.ToString());
        var vendorName = vendor.ToString().ToUpperInvariant();

        var items = new List<UpdateItem>();
        foreach (var gpu in matchingGpus)
        {
            var item = new UpdateItem(
                $"dev:{gpu.InstanceId}",
                gpu.Name,
                UpdateKind.Driver,
                gpu.DriverVersion,
                null,
                null,
                null,
                null,
                Id,
                Name)
            {
                Method = InstallMethod.PageOnly,
                Page = page,
                Note = $"Check {vendorName}'s site for a newer driver."
            };
            items.Add(item);
        }

        return Task.FromResult(new SourceResult(SourceStatus.Ok, items, null, null));
    }
}
