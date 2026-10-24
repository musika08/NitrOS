using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

public class WinAppxTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    [Fact]
    public void WinAppx_FindsInstalledPackage()
    {
        // Read-only, current user (the all-users query needs elevation). Calculator, else any Microsoft package.
        var appx = new WinAppx();
        var found = appx.InstalledForCurrentUser("Microsoft.WindowsCalculator");
        if (found.Count == 0) found = appx.InstalledForCurrentUser("Microsoft.*");
        Assert.NotEmpty(found);
        Assert.Empty(appx.InstalledForCurrentUser("NitrOS.No.Such.Package.7f3a"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("**")]
    public void WildcardOnlyNames_ThrowBeforeAnyQuery(string bad)
    {
        var appx = new WinAppx();
        Assert.Throws<ArgumentException>(() => appx.IsInstalled(bad));
        Assert.Throws<ArgumentException>(() => appx.IsProvisioned(bad));
        Assert.Throws<ArgumentException>(() => appx.RemoveForAllUsers(bad));
        Assert.Throws<ArgumentException>(() => appx.Deprovision(bad));
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinAppx_RemoveAndDeprovision()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: removes and deprovisions a real app (set NITROS_VM=1 inside the VM)");
        var appx = new WinAppx();
        const string name = "Microsoft.WindowsCalculator";
        Assert.True(appx.IsInstalled(name) || appx.IsProvisioned(name), "Calculator must be present in the VM");
        appx.RemoveForAllUsers(name);
        appx.Deprovision(name);
        Assert.False(appx.IsInstalled(name));
        Assert.False(appx.IsProvisioned(name));
    }
}
