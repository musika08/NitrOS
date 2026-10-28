using NitrOS.Core.Model;
using NitrOS.Live.Launch;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Launch;

public sealed class AfterRestartLaunchTests
{
    [Fact]
    public void AfterRestartLaunch_WritesQuotedExeWithFlag()
    {
        var reg = new FakeRegistry();
        var launch = new AfterRestartLaunch(reg);

        bool registered = launch.TryRegister(@"C:\Apps\NitrOS.exe");

        Assert.True(registered);
        var val = reg.GetValue(LiveHive.LocalMachine, AfterRestartLaunch.Key, AfterRestartLaunch.ValueName, RegView.Default);
        Assert.NotNull(val);
        Assert.Equal(RegistryKind.String, val.Kind);
        Assert.Equal("\"C:\\Apps\\NitrOS.exe\" --after-restart", val.Data);
    }

    [Fact]
    public void AfterRestartLaunch_RefusedKeyWritesNothing()
    {
        var reg = new FakeRegistry();
        reg.RefusedKeys.Add((LiveHive.LocalMachine, AfterRestartLaunch.Key));
        var launch = new AfterRestartLaunch(reg);

        bool registered = launch.TryRegister(@"C:\Apps\NitrOS.exe");

        Assert.False(registered);
        var val = reg.GetValue(LiveHive.LocalMachine, AfterRestartLaunch.Key, AfterRestartLaunch.ValueName, RegView.Default);
        Assert.Null(val);
    }

    [Fact]
    public void AfterRestartLaunch_ClearRemovesValue()
    {
        var reg = new FakeRegistry();
        var launch = new AfterRestartLaunch(reg);
        launch.TryRegister(@"C:\Apps\NitrOS.exe");
        Assert.NotNull(reg.GetValue(LiveHive.LocalMachine, AfterRestartLaunch.Key, AfterRestartLaunch.ValueName, RegView.Default));

        launch.Clear();

        Assert.Null(reg.GetValue(LiveHive.LocalMachine, AfterRestartLaunch.Key, AfterRestartLaunch.ValueName, RegView.Default));
    }
}
