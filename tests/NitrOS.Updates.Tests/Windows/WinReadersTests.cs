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
        var dev = new WinInstalledDevices();
        var present = dev.Present();
        var cardReader = present.FirstOrDefault(d => d.InstanceId.Contains("17A0", System.StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Genesys", System.StringComparison.OrdinalIgnoreCase));
        if (cardReader != null)
        {
            Assert.NotNull(cardReader.DriverVersion);
        }

        var fw = new WinFirmwareTable(devices: dev);
        var list = fw.Read();
        Assert.NotNull(list);
        if (list.Count > 0)
        {
            Assert.Contains(list, f => f.Type == 1);
            var touch = list.FirstOrDefault(f => f.Id.ToString().Equals("222a79d6-d2aa-358b-b1ac-8771faf8fec0", System.StringComparison.OrdinalIgnoreCase));
            if (touch != null)
            {
                Assert.NotNull(touch.PackageVersion);
            }
        }
    }
}
