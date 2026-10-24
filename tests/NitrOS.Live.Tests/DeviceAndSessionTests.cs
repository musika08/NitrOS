using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests;

public class DeviceAndSessionTests
{
    private sealed record Device(bool HasBattery, int MaxTouches, string Manufacturer, string Model) : IDeviceFacts;

    private sealed record Session(string ProcessUserSid, string? ConsoleUserSid, bool IsElevated, int WindowsBuild) : ISessionFacts;

    [Fact]
    public void Detect_AllyXIsHandheldBatteryTouch()
    {
        var p = DeviceDetector.Detect(new Device(true, 10, "ASUSTeK COMPUTER INC.", "ROG Xbox Ally X RC73XA_RC73XA"));
        Assert.Equal(new DeviceProfile(true, true, true), p);
    }

    [Fact]
    public void Detect_DesktopHasNoTraits()
    {
        Assert.Equal(DeviceProfile.Desktop, DeviceDetector.Detect(new Device(false, 0, "MSI", "MS-7E12")));
    }

    [Fact]
    public void Detect_LaptopWithTouchIsNotHandheld()
    {
        var p = DeviceDetector.Detect(new Device(true, 10, "Dell", "XPS 13"));
        Assert.True(p.HasBattery && p.HasTouchscreen);
        Assert.False(p.IsHandheld);
    }

    [Fact]
    public void SessionGuard_AcceptsMatchingElevatedModernSession()
    {
        Assert.True(SessionGuard.Check(new Session("S-1-5-21-1", "S-1-5-21-1", true, 26100)).Ok);
    }

    [Fact]
    public void SessionGuard_RefusesWhenProcessUserIsNotConsoleUser()
    {
        var r = SessionGuard.Check(new Session("S-1-5-21-1", "S-1-5-21-2", true, 26200));
        Assert.False(r.Ok);
        Assert.Equal("NitrOS is running as a different user than the one signed in, so it would change the wrong account's settings.", r.Reason);
    }

    [Fact]
    public void SessionGuard_RefusesWhenNoConsoleUser()
    {
        var r = SessionGuard.Check(new Session("S-1-5-21-1", null, true, 26200));
        Assert.Equal("No one is signed in at this PC.", r.Reason);
    }

    [Fact]
    public void SessionGuard_RefusesWhenNotElevated()
    {
        var r = SessionGuard.Check(new Session("S-1-5-21-1", "S-1-5-21-1", false, 26200));
        Assert.False(r.Ok);
        Assert.Equal("NitrOS needs administrator rights.", r.Reason);
    }

    [Fact]
    public void SessionGuard_RefusesOldWindows()
    {
        var old = SessionGuard.Check(new Session("S-1-5-21-1", "S-1-5-21-1", true, 22631));
        Assert.False(old.Ok);
        Assert.Equal("NitrOS needs Windows 11 24H2 or newer.", old.Reason);
        Assert.True(SessionGuard.Check(new Session("S-1-5-21-1", "S-1-5-21-1", true, 26100)).Ok);
    }

    [Fact]
    public void SessionGuard_ChecksBuildBeforeElevation()
    {
        Assert.Equal("NitrOS needs Windows 11 24H2 or newer.",
            SessionGuard.Check(new Session("a", null, false, 19045)).Reason);
    }

    [Fact]
    public void WinDeviceFacts_OnThisPcIsAllyX()
    {
        // Read-only: power status, touch metric and firmware strings.
        var facts = new WinDeviceFacts();
        var profile = DeviceDetector.Detect(facts);
        if (facts.Model.Contains("RC73XA", StringComparison.OrdinalIgnoreCase))
            Assert.True(profile.IsHandheld);
    }

    [Fact]
    public void WinSessionFacts_ReadsThisSession()
    {
        var f = new WinSessionFacts();
        Assert.StartsWith("S-1-", f.ProcessUserSid);
        Assert.True(f.WindowsBuild > 0);
        _ = f.IsElevated;
        _ = f.ConsoleUserSid;
    }
}
