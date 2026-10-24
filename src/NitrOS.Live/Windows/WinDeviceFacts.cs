using Microsoft.Win32;

namespace NitrOS.Live.Windows;

/// <summary>Read-only hardware facts: battery, touch points, and the maker and model from the firmware.</summary>
public sealed class WinDeviceFacts : IDeviceFacts
{
    public bool HasBattery =>
        NativeMethods.GetSystemPowerStatus(out var s) && s.BatteryFlag != NativeMethods.BATTERY_FLAG_NO_BATTERY;

    public int MaxTouches => NativeMethods.GetSystemMetrics(NativeMethods.SM_MAXIMUMTOUCHES);

    public string Manufacturer => Bios("SystemManufacturer");

    public string Model => Bios("SystemProductName");

    // The same firmware strings Win32_ComputerSystem reports, read without WMI.
    private static string Bios(string value)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            return (key?.GetValue(value) as string)?.Trim() ?? "";
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return "";
        }
    }
}
