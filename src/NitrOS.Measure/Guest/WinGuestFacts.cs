using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NitrOS.Measure.Guest;

public sealed class WinGuestFacts : IGuestFacts
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);

    public int OwnPid => Environment.ProcessId;

    public (long TotalPhys, long AvailPhys) Memory()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new InvalidOperationException($"GlobalMemoryStatusEx failed with error {Marshal.GetLastWin32Error()}.");
        }
        return ((long)status.ullTotalPhys, (long)status.ullAvailPhys);
    }

    public IReadOnlyList<(int Pid, string Name)> Processes()
    {
        var list = new List<(int Pid, string Name)>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                list.Add((p.Id, p.ProcessName));
            }
            catch
            {
                // Process may have exited
            }
        }
        return list;
    }

    public TimeSpan UnbiasedTime()
    {
        if (!QueryUnbiasedInterruptTime(out ulong time100ns))
        {
            throw new InvalidOperationException("QueryUnbiasedInterruptTime failed.");
        }
        return TimeSpan.FromTicks((long)time100ns);
    }

    public DateTimeOffset Now() => DateTimeOffset.UtcNow;

    public string WindowsBuild()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (key is not null)
        {
            var build = key.GetValue("CurrentBuild")?.ToString() ?? "0";
            var ubr = key.GetValue("UBR");
            return ubr is not null ? $"{build}.{ubr}" : build;
        }
        return "Unknown";
    }
}
