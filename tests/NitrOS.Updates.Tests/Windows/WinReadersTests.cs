namespace NitrOS.Updates.Tests.Windows;

using System.Linq;
using NitrOS.Updates.Windows;

public sealed class WinReadersTests
{
    [Fact]
    public void WinIdentity_ReadsThisPc()
    {
        var identity = new WinIdentity();
        Assert.False(string.IsNullOrWhiteSpace(identity.Manufacturer));
    }

    [Fact]
    public void WinInstalledDevices_FindsADisplayDevice()
    {
        var dev = new WinInstalledDevices();
        var present = dev.Present();
        Assert.NotEmpty(present);
        Assert.Contains(present, d => string.Equals(d.DeviceClass, "Display", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WinFirmwareTable_ReadsWithoutThrowing()
    {
        var fw = new WinFirmwareTable();
        var list = fw.Read();
        Assert.NotNull(list);
        if (list.Count > 0)
        {
            Assert.Contains(list, f => f.Type == 1);
        }
    }
}
