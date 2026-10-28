using System.Text.RegularExpressions;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Systems;

public sealed class WindowsVersionTests
{
    [Fact]
    public void WinWindowsVersion_ReadsBuildAndRevision()
    {
        var ver = new WinWindowsVersion();
        Assert.Matches(@"^\d{5}(\.\d+)?$", ver.FullBuild);
    }
}
