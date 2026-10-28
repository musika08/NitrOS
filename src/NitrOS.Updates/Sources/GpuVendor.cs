using NitrOS.Updates.Model;

namespace NitrOS.Updates.Sources;

public enum GpuVendor
{
    Amd,
    Nvidia,
    Intel
}

public static class GpuVendors
{
    public static GpuVendor? FromHardwareId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var upper = id.ToUpperInvariant();
        if (upper.Contains("VEN_1002")) return GpuVendor.Amd;
        if (upper.Contains("VEN_10DE")) return GpuVendor.Nvidia;
        if (upper.Contains("VEN_8086")) return GpuVendor.Intel;

        return null;
    }
}
