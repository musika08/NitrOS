using NitrOS.App.Logic.Startup;

namespace NitrOS.App.Tests;

public sealed class LaunchModeTests
{
    [Fact]
    public void LaunchMode_ParsesFlags()
    {
        Assert.IsType<LaunchMode.NormalMode>(LaunchMode.Parse([]));
        Assert.IsType<LaunchMode.AfterRestartMode>(LaunchMode.Parse(["--after-restart"]));
        Assert.IsType<LaunchMode.AfterUpdateMode>(LaunchMode.Parse(["--after-update"]));

        var smoke = LaunchMode.Parse(["--smoke", @"C:\temp\smoke.json"]);
        Assert.IsType<LaunchMode.SmokeMode>(smoke);
        Assert.Equal(@"C:\temp\smoke.json", ((LaunchMode.SmokeMode)smoke).ResultPath);

        Assert.IsType<LaunchMode.NormalMode>(LaunchMode.Parse(["--unknown", "arg"]));
    }
}
