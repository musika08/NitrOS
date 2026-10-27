using System.Runtime.InteropServices;

namespace NitrOS.Live.Windows;

public sealed class WinMemoryFacts : IMemoryFacts
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    public MemoryInfo Read()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new InvalidOperationException($"GlobalMemoryStatusEx failed with error {Marshal.GetLastWin32Error()}.");
        }

        ulong installedKb;
        if (!GetPhysicallyInstalledSystemMemory(out installedKb) || installedKb == 0)
        {
            installedKb = status.ullTotalPhys / 1024;
        }

        int installedMb = (int)(installedKb / 1024);
        int visibleMb = (int)(status.ullTotalPhys / (1024 * 1024));
        int inUseMb = (int)((status.ullTotalPhys - status.ullAvailPhys) / (1024 * 1024));

        return new MemoryInfo(installedMb, visibleMb, inUseMb);
    }
}
