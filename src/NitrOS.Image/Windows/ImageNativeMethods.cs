namespace NitrOS.Image.Windows;

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class ImageNativeMethods
{
    public const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    public const uint TOKEN_QUERY = 0x0008;

    public const int ERROR_SUCCESS = 0;
    public const int ERROR_NOT_ALL_ASSIGNED = 1300;

    public static readonly IntPtr HKEY_LOCAL_MACHINE = new(unchecked((int)0x80000002));

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out SafeProcessToken TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LookupPrivilegeValueW(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AdjustTokenPrivileges(SafeProcessToken TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int RegLoadKeyW(IntPtr hKey, string lpSubKey, string lpFile);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int RegUnLoadKeyW(IntPtr hKey, string lpSubKey);

    // File disposition flags for SetFileInformationByHandle
    public const int FileDispositionInfoEx = 21;
    public const uint FILE_DISPOSITION_FLAG_DELETE = 0x00000001;
    public const uint FILE_DISPOSITION_FLAG_POSIX_SEMANTICS = 0x00000002;
    public const uint FILE_DISPOSITION_FLAG_IGNORE_READONLY_ATTRIBUTE = 0x00000010;

    [StructLayout(LayoutKind.Sequential)]
    public struct FILE_DISPOSITION_INFO_EX
    {
        public uint Flags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetFileInformationByHandle(SafeFileHandle hFile, int FileInformationClass, ref FILE_DISPOSITION_INFO_EX lpFileInformation, uint dwBufferSize);

    public const uint FILE_READ_ATTRIBUTES = 0x0080;
    public const uint DELETE = 0x00010000;
    public const uint SYNCHRONIZE = 0x00100000;
    public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    public const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_SHARE_READ = 1;
    public const uint FILE_SHARE_WRITE = 2;
    public const uint FILE_SHARE_DELETE = 4;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
}

internal sealed class SafeProcessToken : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeProcessToken() : base(true) { }

    protected override bool ReleaseHandle()
    {
        return CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
