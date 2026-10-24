using NitrOS.Core.Model;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

public class WinServiceConfigTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    [Fact]
    public void WinServiceConfig_ReadsKnownService()
    {
        var info = new WinServiceConfig().Get("Audiosrv");
        Assert.NotNull(info);
        Assert.Contains(info!.Start, new[] { ServiceStartType.Automatic, ServiceStartType.AutomaticDelayed, ServiceStartType.Manual, ServiceStartType.Disabled });
    }

    [Fact]
    public void WinServiceConfig_MissingServiceIsNull()
    {
        Assert.Null(new WinServiceConfig().Get("NitrOS-No-Such-Service-7f3a"));
    }

    [Fact]
    public void WinServiceConfig_FindsPerUserInstances()
    {
        var s = new WinServiceConfig();
        var instances = s.PerUserInstances("CDPUserSvc");
        Assert.NotEmpty(instances);
        Assert.All(instances, n => Assert.StartsWith("CDPUserSvc_", n, StringComparison.OrdinalIgnoreCase));
        Assert.All(instances, n => Assert.NotNull(s.Get(n)));
    }

    [Theory]
    [InlineData(0, null, ServiceStartType.Boot)]
    [InlineData(1, null, ServiceStartType.System)]
    [InlineData(2, null, ServiceStartType.Automatic)]
    [InlineData(2, 0, ServiceStartType.Automatic)]
    [InlineData(2, 1, ServiceStartType.AutomaticDelayed)]
    [InlineData(3, 1, ServiceStartType.Manual)]
    [InlineData(4, null, ServiceStartType.Disabled)]
    public void FromRegistry_MapsStartValues(int start, int? delayed, ServiceStartType expected)
    {
        Assert.Equal(expected, WinServiceConfig.FromRegistry(start, delayed, "x"));
    }

    [Fact]
    public void FromRegistry_ThrowsForUnknownOrMissing()
    {
        Assert.Throws<InvalidOperationException>(() => WinServiceConfig.FromRegistry(5, null, "x"));
        Assert.Throws<InvalidOperationException>(() => WinServiceConfig.FromRegistry(null, null, "x"));
        Assert.Throws<InvalidOperationException>(() => WinServiceConfig.FromRegistry("2", null, "x"));
        Assert.Throws<InvalidOperationException>(() => WinServiceConfig.FromRegistry(2, "1", "x"));
    }

    [Fact]
    public void ToStartType_MapsToScmValues()
    {
        Assert.Equal(2u, WinServiceConfig.ToStartType(ServiceStartType.Automatic));
        Assert.Equal(2u, WinServiceConfig.ToStartType(ServiceStartType.AutomaticDelayed));
        Assert.Equal(3u, WinServiceConfig.ToStartType(ServiceStartType.Manual));
        Assert.Equal(4u, WinServiceConfig.ToStartType(ServiceStartType.Disabled));
        Assert.Throws<ArgumentOutOfRangeException>(() => WinServiceConfig.ToStartType((ServiceStartType)99));
    }

    [Fact]
    public void MatchInstances_OnlyHexSuffixes()
    {
        var names = new[] { "CDPUserSvc", "CDPUserSvc_4a3b2", "cdpusersvc_FF01", "CDPUserSvc_", "CDPUserSvc_xyz", "CDPUserSvcX_12", "Other_12" };
        Assert.Equal(new[] { "CDPUserSvc_4a3b2", "cdpusersvc_FF01" }, WinServiceConfig.MatchInstances("CDPUserSvc", names));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Audiosrv\\..\\Other")]
    [InlineData("a/b")]
    public void BadNames_Throw(string bad)
    {
        var s = new WinServiceConfig();
        Assert.Throws<ArgumentException>(() => s.Get(bad));
        Assert.Throws<ArgumentException>(() => s.Set(bad, ServiceStartType.Manual));
        Assert.Throws<ArgumentException>(() => s.PerUserInstances(bad));
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinServiceConfig_Set_RoundTrip()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: changes a real service start type (set NITROS_VM=1 inside the VM)");
        var s = new WinServiceConfig();
        const string name = "Fax";
        var before = s.Get(name) ?? throw new InvalidOperationException("Fax service not present in the VM");
        try
        {
            foreach (var t in new[] { ServiceStartType.AutomaticDelayed, ServiceStartType.Automatic, ServiceStartType.Disabled, ServiceStartType.Manual })
            {
                s.Set(name, t);
                Assert.Equal(t, s.Get(name)!.Start);
            }
        }
        finally { s.Set(name, before.Start); }
        Assert.Equal(before.Start, s.Get(name)!.Start);
    }
}
