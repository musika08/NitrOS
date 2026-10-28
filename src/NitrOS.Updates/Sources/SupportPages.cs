using NitrOS.Updates.Model;

namespace NitrOS.Updates.Sources;

public static class SupportPages
{
    public static Uri? For(MakerId maker, string? productName = null)
    {
        return maker switch
        {
            MakerId.Asus => (productName != null && productName.Contains("ROG", StringComparison.OrdinalIgnoreCase))
                ? new Uri("https://rog.asus.com/support/")
                : new Uri("https://www.asus.com/support/download-center/"),
            MakerId.Msi => new Uri("https://www.msi.com/support/download"),
            MakerId.Lenovo => new Uri("https://pcsupport.lenovo.com/"),
            MakerId.Dell => new Uri("https://www.dell.com/support/home/"),
            MakerId.Hp => new Uri("https://support.hp.com/drivers"),
            _ => null
        };
    }

    public static Uri ForGpu(string vendor)
    {
        return vendor.ToUpperInvariant() switch
        {
            "AMD" => new Uri("https://www.amd.com/en/support/download/drivers.html"),
            "NVIDIA" => new Uri("https://www.nvidia.com/Download/index.aspx"),
            "INTEL" => new Uri("https://www.intel.com/content/www/us/en/download-center/home.html"),
            _ => new Uri("https://www.google.com/search?q=" + Uri.EscapeDataString(vendor + " graphics drivers"))
        };
    }
}
