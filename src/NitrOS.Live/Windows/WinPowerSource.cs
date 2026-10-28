namespace NitrOS.Live.Windows;

using System.ComponentModel;
using NitrOS.Live.Systems;

public sealed class WinPowerSource : IPowerSource
{
    public PowerState Read()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var status))
        {
            throw new Win32Exception();
        }

        return Map(status.ACLineStatus, status.BatteryFlag, status.BatteryLifePercent);
    }

    internal static PowerState Map(byte acLine, byte batteryFlag, byte percent)
    {
        var hasBattery = batteryFlag != NativeMethods.BATTERY_FLAG_NO_BATTERY && batteryFlag != 255;
        var onBattery = acLine == 0;
        int? batteryPercent = percent != 255 ? percent : null;

        return new PowerState(hasBattery, onBattery, batteryPercent);
    }
}
