namespace NitrOS.Live.Tests.Windows;

using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

public sealed class WinPowerSourceTests
{
    [Fact]
    public void PowerState_MapsFlags()
    {
        // AC on (1), normal battery (0), 80%
        var s1 = WinPowerSource.Map(1, 0, 80);
        Assert.True(s1.HasBattery);
        Assert.False(s1.OnBattery);
        Assert.Equal(80, s1.BatteryPercent);

        // AC off (0) -> on battery, normal battery (0), 50%
        var s2 = WinPowerSource.Map(0, 0, 50);
        Assert.True(s2.HasBattery);
        Assert.True(s2.OnBattery);
        Assert.Equal(50, s2.BatteryPercent);

        // ACLine unknown (255) -> not on battery (ACLineStatus == 0 is false)
        var s3 = WinPowerSource.Map(255, 0, 50);
        Assert.True(s3.HasBattery);
        Assert.False(s3.OnBattery);
        Assert.Equal(50, s3.BatteryPercent);

        // No battery (128)
        var s4 = WinPowerSource.Map(1, 128, 255);
        Assert.False(s4.HasBattery);
        Assert.False(s4.OnBattery);
        Assert.Null(s4.BatteryPercent);

        // Unknown battery flag (255)
        var s5 = WinPowerSource.Map(1, 255, 255);
        Assert.False(s5.HasBattery);
        Assert.False(s5.OnBattery);
        Assert.Null(s5.BatteryPercent);
    }

    [Fact]
    public void WinPowerSource_ReadsThisPc()
    {
        var source = new WinPowerSource();
        var state = source.Read();
        Assert.NotNull(state);
        // Ally X has a battery
        Assert.True(state.HasBattery);
        if (state.BatteryPercent.HasValue)
        {
            Assert.InRange(state.BatteryPercent.Value, 0, 100);
        }
    }
}
