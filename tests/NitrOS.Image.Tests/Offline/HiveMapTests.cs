namespace NitrOS.Image.Tests.Offline;

using System;
using System.IO;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Image.Offline;

public sealed class HiveMapTests
{
    [Fact]
    public void HiveMap_MapsEveryRepoImageRegistryAction()
    {
        var cur = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (cur is not null && !Directory.Exists(Path.Combine(cur.FullName, "catalog")))
        {
            cur = cur.Parent;
        }
        Assert.NotNull(cur);
        var catalogDir = Path.Combine(cur.FullName, "catalog");
        var loadResult = CatalogLoader.Load(catalogDir);
        Assert.NotNull(loadResult.Catalog);

        const int currentControlSet = 1;

        foreach (var sw in loadResult.Catalog.Switches)
        {
            foreach (var action in sw.Image)
            {
                if (action is RegistrySet rs)
                {
                    var target = HiveMap.Map(rs.Hive, rs.Key, currentControlSet);
                    Assert.NotNull(target);
                }
                else if (action is RegistryDelete rd)
                {
                    var target = HiveMap.Map(rd.Hive, rd.Key, currentControlSet);
                    Assert.NotNull(target);
                }
            }
        }

        // Specific test: SYSTEM\CurrentControlSet\Control\GraphicsDrivers with current set 1 becomes ControlSet001\Control\GraphicsDrivers
        var targetSet = HiveMap.Map("HKLM", "SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", 1);
        Assert.Equal(OfflineHive.System, targetSet.Hive);
        Assert.Equal("ControlSet001\\Control\\GraphicsDrivers", targetSet.SubKey);
    }

    [Fact]
    public void HiveMap_LowerCaseSoftwareMapsToSoftware()
    {
        var target = HiveMap.Map("HKLM", "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Paint", 1);
        Assert.Equal(OfflineHive.Software, target.Hive);
        Assert.Equal("Microsoft\\Windows\\CurrentVersion\\Policies\\Paint", target.SubKey);
    }

    [Fact]
    public void HiveMap_HkcuGoesToDefaultUser()
    {
        var target = HiveMap.Map("HKCU", "Software\\Microsoft\\Windows\\CurrentVersion", 1);
        Assert.Equal(OfflineHive.DefaultUser, target.Hive);
        Assert.Equal("Software\\Microsoft\\Windows\\CurrentVersion", target.SubKey);
    }

    [Fact]
    public void HiveMap_Registry32InsertsWow6432Node()
    {
        var target = HiveMap.Map("HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion", 1, registry32: true);
        Assert.Equal(OfflineHive.Software, target.Hive);
        Assert.Equal("WOW6432Node\\Microsoft\\Windows\\CurrentVersion", target.SubKey);

        // If already present, don't duplicate
        var targetAlready = HiveMap.Map("HKLM", "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion", 1, registry32: true);
        Assert.Equal("WOW6432Node\\Microsoft\\Windows\\CurrentVersion", targetAlready.SubKey);
    }

    [Fact]
    public void HiveMap_UnknownRootThrows()
    {
        Assert.Throws<NotSupportedException>(() => HiveMap.Map("HKLM", "HARDWARE\\Description", 1));
        Assert.Throws<NotSupportedException>(() => HiveMap.Map("HKCR", "Clsid", 1));
    }

    [Fact]
    public void ServiceStartValues_AllSix()
    {
        Assert.Equal((0, null), ServiceStartValues.For(ServiceStartType.Boot));
        Assert.Equal((1, null), ServiceStartValues.For(ServiceStartType.System));
        Assert.Equal((2, 0), ServiceStartValues.For(ServiceStartType.Automatic));
        Assert.Equal((2, 1), ServiceStartValues.For(ServiceStartType.AutomaticDelayed));
        Assert.Equal((3, null), ServiceStartValues.For(ServiceStartType.Manual));
        Assert.Equal((4, null), ServiceStartValues.For(ServiceStartType.Disabled));
    }
}
