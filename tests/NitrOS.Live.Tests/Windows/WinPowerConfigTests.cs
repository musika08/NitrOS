using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

public class WinPowerConfigTests
{
    // USB selective suspend, from catalog/gaming-settings.json
    private const string UsbSub = "2a737441-1930-4402-8d77-b2bebba308a3";
    private const string UsbSetting = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    [Fact]
    public void WinPowerConfig_ReadsActiveSchemeAndOneSetting()
    {
        var p = new WinPowerConfig();
        var active = p.ActiveScheme();
        Assert.Matches("^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$", active);
        var all = p.Schemes();
        Assert.Contains(active, all);
        Assert.All(all, s => Assert.Equal(s.ToLowerInvariant().Trim('{', '}'), s));

        // Any case and braces are accepted on input.
        var upper = "{" + active.ToUpperInvariant() + "}";
        var lower = p.Get(active, UsbSub, UsbSetting);
        var viaUpper = p.Get(upper, UsbSub.ToUpperInvariant(), UsbSetting.ToUpperInvariant());
        Assert.Equal(lower, viaUpper);
        // /q (or the /qh fallback for hidden settings) must show the USB selective suspend setting on this PC.
        Assert.NotNull(lower);
        Assert.InRange(lower!.Value.Ac, 0, 1);
        Assert.InRange(lower.Value.Dc, 0, 1);
    }

    [Fact]
    public void WinPowerConfig_UnknownSettingIsNull()
    {
        var p = new WinPowerConfig();
        Assert.Null(p.Get(p.ActiveScheme(), "00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002"));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("381b4222-f694-41f0-9685-ff5bb260df2e /setactive")]
    public void BadGuids_ThrowBeforeAnyProcessRuns(string bad)
    {
        var p = new WinPowerConfig();
        const string ok = "381b4222-f694-41f0-9685-ff5bb260df2e";
        Assert.Throws<ArgumentException>(() => p.Get(bad, ok, ok));
        Assert.Throws<ArgumentException>(() => p.Get(ok, bad, ok));
        Assert.Throws<ArgumentException>(() => p.Get(ok, ok, bad));
        Assert.Throws<ArgumentException>(() => p.SetAc(bad, ok, ok, 1));
        Assert.Throws<ArgumentException>(() => p.SetDc(ok, bad, ok, 1));
        Assert.Throws<ArgumentException>(() => p.SetActive(bad));
    }

    [Fact]
    public void Normalise_LowercasesAndStripsBraces()
    {
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", WinPowerConfig.Normalise("{381B4222-F694-41F0-9685-FF5BB260DF2E}"));
    }

    [Fact]
    public void IsNotFoundExit_OnlyForExitOne()
    {
        Assert.True(WinPowerConfig.IsNotFoundExit(1));
        Assert.False(WinPowerConfig.IsNotFoundExit(0));
        Assert.False(WinPowerConfig.IsNotFoundExit(ProcessRunner.TimeoutExitCode));
        Assert.False(WinPowerConfig.IsNotFoundExit(5));
    }

    [Fact]
    public void ParseIndexes_TakesLastTwoHexLines()
    {
        const string text = "  Power Setting GUID: x  (USB selective suspend setting)\r\n" +
            "    Possible Setting Index: 000\r\n" +
            "    Minimum Possible Setting: 0x00000000\r\n" +
            "    Maximum Possible Setting: 0x00000001\r\n" +
            "    Possible Settings increment: 0x00000001\r\n" +
            "  Current AC Power Setting Index: 0x00000001\r\n" +
            "  Current DC Power Setting Index: 0x0000000a\r\n";
        Assert.Equal((1, 10), WinPowerConfig.ParseIndexes(text));
        Assert.Null(WinPowerConfig.ParseIndexes("nothing here"));
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinPowerConfig_SetAcAndDc_RoundTrip()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: changes a real power setting (set NITROS_VM=1 inside the VM)");
        var p = new WinPowerConfig();
        var s = p.ActiveScheme();
        var before = p.Get(s, UsbSub, UsbSetting)!.Value;
        try
        {
            p.SetAc(s, UsbSub, UsbSetting, 1 - before.Ac);
            p.SetDc(s, UsbSub, UsbSetting, 1 - before.Dc);
            Assert.Equal((1 - before.Ac, 1 - before.Dc), p.Get(s, UsbSub, UsbSetting));
        }
        finally
        {
            p.SetAc(s, UsbSub, UsbSetting, before.Ac);
            p.SetDc(s, UsbSub, UsbSetting, before.Dc);
        }
        Assert.Equal(before, p.Get(s, UsbSub, UsbSetting));
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinPowerConfig_SetActive_RoundTrip()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: changes the active power scheme (set NITROS_VM=1 inside the VM)");
        var p = new WinPowerConfig();
        var original = p.ActiveScheme();
        try
        {
            var other = p.Schemes().FirstOrDefault(x => x != original) ?? original;
            p.SetActive(other.ToUpperInvariant());
            Assert.Equal(other, p.ActiveScheme());
        }
        finally { p.SetActive(original); }
        Assert.Equal(original, p.ActiveScheme());
    }
}
