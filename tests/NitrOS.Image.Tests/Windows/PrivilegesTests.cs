namespace NitrOS.Image.Tests.Windows;

using System;
using System.Security.Principal;
using NitrOS.Image.Windows;

public sealed class PrivilegesTests
{
    [Fact]
    public void Privileges_NotElevatedThrowsPlainMessage()
    {
        var isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        if (!isElevated)
        {
            var ex = Assert.Throws<UnauthorizedAccessException>(() => Privileges.Enable("SeBackupPrivilege"));
            Assert.Contains("administrator rights", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
