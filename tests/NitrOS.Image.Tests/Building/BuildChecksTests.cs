namespace NitrOS.Image.Tests.Building;

using System;
using NitrOS.Image.Building;
using NitrOS.Image.Tests.Fakes;
using NitrOS.Live.Systems;

public sealed class BuildChecksTests
{
    [Fact]
    public void Build_RefusesExistingOrSourceOutput()
    {
        var files = new FakeImageFiles();
        files.Files["C:\\output.iso"] = "existing";
        files.Files["C:\\source.iso"] = "source";

        var reqExisting = new BuildRequest("C:\\source.iso", "C:\\output.iso", null, [], null);
        var err1 = BuildChecks.Paths(reqExisting, files);
        Assert.NotNull(err1);
        Assert.Contains("already there", err1);

        var reqSame = new BuildRequest("C:\\source.iso", "C:\\source.iso", null, [], null);
        files.Files.Remove("C:\\source.iso");
        files.Files["C:\\source.iso"] = "source";
        var err2 = BuildChecks.Paths(reqSame, files);
        Assert.NotNull(err2);

        var reqInside = new BuildRequest("C:\\source", "C:\\source\\out.iso", null, [], null);
        var err3 = BuildChecks.Paths(reqInside, files);
        Assert.NotNull(err3);
    }

    [Fact]
    public void Build_RefusesOnBattery()
    {
        var powerOnBattery = new PowerState(HasBattery: true, OnBattery: true, BatteryPercent: 80);
        var err = BuildChecks.Power(powerOnBattery);
        Assert.NotNull(err);
        Assert.Contains("battery", err, StringComparison.OrdinalIgnoreCase);

        var powerAc = new PowerState(HasBattery: true, OnBattery: false, BatteryPercent: 80);
        Assert.Null(BuildChecks.Power(powerAc));

        var powerNoBattery = new PowerState(HasBattery: false, OnBattery: false, BatteryPercent: null);
        Assert.Null(BuildChecks.Power(powerNoBattery));
    }

    [Fact]
    public void Build_RefusesOldHostDismBeforeAnything()
    {
        var hostOld = new FakeHostFacts { FakeDismVersion = new Version(10, 0, 22621, 1) };
        var errOld = BuildChecks.HostDism(hostOld);
        Assert.NotNull(errOld);
        Assert.Contains("too old", errOld, StringComparison.OrdinalIgnoreCase);

        var hostOk = new FakeHostFacts { FakeDismVersion = new Version(10, 0, 26100, 1) };
        Assert.Null(BuildChecks.HostDism(hostOk));
    }
}
