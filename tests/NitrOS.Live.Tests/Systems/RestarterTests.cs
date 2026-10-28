using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Systems;

public sealed class RestarterTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinRestarter_RestartsInVm()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: initiates a real system restart");

        var restarter = new WinRestarter();
        restarter.Restart();
    }
}
