using System.Net.Http;
using NitrOS.Live;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests;

public class HealthCheckTests
{
    private sealed class FakeProbe : IHealthProbe
    {
        public HashSet<string> Processes { get; } = ["StartMenuExperienceHost"];
        public HashSet<string> Services { get; } = ["Audiosrv", "bthserv", "GamingServices"];
        public HashSet<string> Packages { get; } = ["Microsoft.WindowsStore", "Microsoft.GamingApp"];
        public string? Body { get; set; } = "Microsoft Connect Test";
        public List<string> Asked { get; } = [];

        public bool IsProcessRunning(string n) { Asked.Add(n); return Processes.Contains(n); }
        public bool IsServiceRunning(string n) { Asked.Add(n); return Services.Contains(n); }
        public bool IsPackageInstalled(string n) { Asked.Add(n); return Packages.Contains(n); }
        public string? HttpGet(string url) => Body;
    }

    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    [Fact]
    public void Health_AllGoodReportsAllOk()
    {
        var r = HealthCheck.Run(new FakeProbe(), None);
        Assert.True(r.AllOk);
        Assert.Equal(6, r.Items.Count);
    }

    [Fact]
    public void Health_SkipsKeptChecksForRemovedFeatures()
    {
        var probe = new FakeProbe();
        probe.Services.Clear();
        probe.Packages.Clear();
        probe.Services.Add("Audiosrv");
        IReadOnlySet<string> applied = new HashSet<string>
            { "bluetooth-off", "store-remove", "xbox-app-remove", "gaming-services-remove" };

        var r = HealthCheck.Run(probe, applied);

        Assert.True(r.AllOk);
        Assert.DoesNotContain(r.Items, i => i.Name is "Bluetooth" or "Microsoft Store" or "Game Pass");
        Assert.DoesNotContain("bthserv", probe.Asked);
        Assert.DoesNotContain("Microsoft.WindowsStore", probe.Asked);
        Assert.DoesNotContain("Microsoft.GamingApp", probe.Asked);
        Assert.DoesNotContain("GamingServices", probe.Asked);
    }

    [Fact]
    public void Health_NamesLikelySwitch()
    {
        var probe = new FakeProbe();
        probe.Processes.Clear();
        probe.Services.Remove("Audiosrv");
        probe.Services.Remove("bthserv");
        probe.Packages.Remove("Microsoft.WindowsStore");
        probe.Body = null;

        var items = HealthCheck.Run(probe, None).Items.ToDictionary(i => i.Name);

        Assert.Equal("windows-ads-off", items["Start menu"].LikelySwitchId);
        Assert.False(items["Internet"].Ok);
        Assert.Null(items["Internet"].LikelySwitchId);
        Assert.False(items["Sound"].Ok);
        Assert.Null(items["Sound"].LikelySwitchId);
        Assert.Equal("bluetooth-off", items["Bluetooth"].LikelySwitchId);
        Assert.Equal("store-remove", items["Microsoft Store"].LikelySwitchId);
        Assert.True(items["Game Pass"].Ok);
    }

    [Fact]
    public void Health_GamePassNamesTheFailingPart()
    {
        var probe = new FakeProbe();
        probe.Services.Remove("GamingServices");
        Assert.Equal("gaming-services-remove", HealthCheck.Run(probe, None).Items.Single(i => i.Name == "Game Pass").LikelySwitchId);

        probe = new FakeProbe();
        probe.Packages.Remove("Microsoft.GamingApp");
        Assert.Equal("xbox-app-remove", HealthCheck.Run(probe, None).Items.Single(i => i.Name == "Game Pass").LikelySwitchId);
    }

    [Fact]
    public void Health_GamePassChecksOnlyTheKeptPart()
    {
        var probe = new FakeProbe();
        probe.Packages.Remove("Microsoft.GamingApp");
        IReadOnlySet<string> applied = new HashSet<string> { "xbox-app-remove" };
        Assert.True(HealthCheck.Run(probe, applied).Items.Single(i => i.Name == "Game Pass").Ok);
    }

    [Fact]
    public void WinHealthProbe_ReadsThisPc()
    {
        // Read-only: process list, service status, package query and one HTTP GET.
        var p = new WinHealthProbe(_ => { });
        Assert.True(p.IsServiceRunning("RpcSs"));
        Assert.False(p.IsServiceRunning("NitrOS-no-such-service"));
        Assert.True(p.IsProcessRunning("explorer"));
        Assert.False(p.IsPackageInstalled("NitrOS.No.Such.Package.7f3a"));
        _ = p.HttpGet(HealthCheck.ConnectTestUrl);
    }

    private sealed class ThrowingProbe : IHealthProbe
    {
        public bool IsProcessRunning(string n) => throw new InvalidOperationException("boom " + n);
        public bool IsServiceRunning(string n) => n == "Audiosrv" ? true : throw new InvalidOperationException("svc " + n);
        public bool IsPackageInstalled(string n) => true;
        public string? HttpGet(string url) => throw new HttpRequestException("offline");
    }

    [Fact]
    public void Health_ProbeExceptionFailsOnlyThatItemWithDetail()
    {
        var r = HealthCheck.Run(new ThrowingProbe(), None);
        var items = r.Items.ToDictionary(i => i.Name);
        Assert.False(items["Start menu"].Ok);
        Assert.Contains("boom", items["Start menu"].Detail);
        Assert.False(items["Internet"].Ok);
        Assert.Contains("offline", items["Internet"].Detail);
        Assert.True(items["Sound"].Ok);
        Assert.Null(items["Sound"].Detail);
        Assert.False(items["Bluetooth"].Ok);
        Assert.True(items["Microsoft Store"].Ok);
        Assert.False(items["Game Pass"].Ok);
        Assert.Equal(6, r.Items.Count);
    }

    [Fact]
    public void Retry_StopsAtFirstSuccessAndSleepsBetweenTries()
    {
        var sleeps = new List<TimeSpan>();
        var calls = 0;
        var ok = ProbeRetry.Until(() => ++calls == 2, sleeps.Add);
        Assert.True(ok);
        Assert.Equal(2, calls);
        Assert.Equal([TimeSpan.FromSeconds(2)], sleeps);
    }

    [Fact]
    public void Retry_GivesUpAfterThreeTries()
    {
        var sleeps = new List<TimeSpan>();
        var calls = 0;
        Assert.False(ProbeRetry.Until(() => { calls++; return false; }, sleeps.Add));
        Assert.Equal(3, calls);
        Assert.Equal(2, sleeps.Count);
    }

    [Fact]
    public void Retry_ServicePendingCountsAsNotYet()
    {
        var states = new Queue<uint>([NativeMethods.SERVICE_START_PENDING, NativeMethods.SERVICE_RUNNING]);
        var ok = ProbeRetry.Until(() => states.Dequeue() == NativeMethods.SERVICE_RUNNING, _ => { });
        Assert.True(ok);
    }
}
