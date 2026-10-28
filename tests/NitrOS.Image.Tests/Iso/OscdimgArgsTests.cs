namespace NitrOS.Image.Tests.Iso;

using System;
using System.IO;
using NitrOS.Image.Iso;

public sealed class OscdimgArgsTests
{
    [Fact]
    public void OscdimgArgs_BiosAndUefi()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros-args-" + Guid.NewGuid().ToString("N"));
        var bootDir = Path.Combine(temp, "boot");
        var efiDir = Path.Combine(temp, "efi", "microsoft", "boot");
        Directory.CreateDirectory(bootDir);
        Directory.CreateDirectory(efiDir);

        var etfsboot = Path.Combine(bootDir, "etfsboot.com");
        var efisys = Path.Combine(efiDir, "efisys.bin");
        File.WriteAllText(etfsboot, "");
        File.WriteAllText(efisys, "");

        try
        {
            var bootOrder = Path.Combine(temp, "bootorder.txt");
            var outIso = Path.Combine(temp, "out.iso");

            var args = OscdimgArgs.For(temp, bootOrder, "TEST_ISO", outIso);

            Assert.Contains("-m", args);
            Assert.Contains("-o", args);
            Assert.Contains("-u2", args);
            Assert.Contains("-udfver102", args);
            Assert.Contains("-h", args);
            Assert.Contains("-lTEST_ISO", args);
            Assert.Contains($"-yo{bootOrder}", args);
            Assert.Contains($"-bootdata:2#p0,e,b{etfsboot}#pEF,e,b{efisys}", args);
            Assert.Equal(temp, args[^2]);
            Assert.Equal(outIso, args[^1]);
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void OscdimgArgs_UefiOnlyWithoutEtfsboot()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros-args-" + Guid.NewGuid().ToString("N"));
        var efiDir = Path.Combine(temp, "efi", "microsoft", "boot");
        Directory.CreateDirectory(efiDir);

        var efisys = Path.Combine(efiDir, "efisys.bin");
        File.WriteAllText(efisys, "");

        try
        {
            var bootOrder = Path.Combine(temp, "bootorder.txt");
            var outIso = Path.Combine(temp, "out.iso");

            var args = OscdimgArgs.For(temp, bootOrder, "TEST_UEFI", outIso);

            Assert.Contains($"-bootdata:1#pEF,e,b{efisys}", args);
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void OscdimgArgs_ThrowsOnSpacesInPaths()
    {
        Assert.Throws<ArgumentException>(() => OscdimgArgs.For("C:\\Path With Space", "C:\\order.txt", "LABEL", "C:\\out.iso"));
        Assert.Throws<ArgumentException>(() => OscdimgArgs.For("C:\\Path", "C:\\order with space.txt", "LABEL", "C:\\out.iso"));
    }
}
