using System.Security.Principal;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Launch;

public sealed class WinAfterUpdateTaskTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinAfterUpdateTask_RegistersAndRemovesInVm()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: registers and removes a real scheduled task");

        var task = new WinAfterUpdateTask(@"\NitrOS.Tests\CheckAfterUpdates");
        string exe = Environment.ProcessPath ?? @"C:\Windows\System32\cmd.exe";
        string sid = WindowsIdentity.GetCurrent().User!.Value;

        task.Register(exe, sid);
        Assert.True(task.IsRegistered());

        task.Remove();
        Assert.False(task.IsRegistered());
    }
}
