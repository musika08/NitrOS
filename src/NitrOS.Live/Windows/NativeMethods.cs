using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NitrOS.Live.Windows;

/// <summary>A service control manager or service handle, closed with CloseServiceHandle.</summary>
internal sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeServiceHandle() : base(ownsHandle: true) { }

    protected override bool ReleaseHandle() => NativeMethods.CloseServiceHandle(handle);
}

internal static class NativeMethods
{
    // ---- Services (advapi32) ----
    public const uint SC_MANAGER_CONNECT = 0x0001;
    public const uint SERVICE_QUERY_CONFIG = 0x0001;
    public const uint SERVICE_CHANGE_CONFIG = 0x0002;
    public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
    public const uint SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_DELAYED_AUTO_START_INFO
    {
        public int fDelayedAutostart; // BOOL
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "OpenSCManagerW")]
    public static extern SafeServiceHandle OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "OpenServiceW")]
    public static extern SafeServiceHandle OpenService(SafeServiceHandle scManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "ChangeServiceConfigW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ChangeServiceConfig(
        SafeServiceHandle service, uint serviceType, uint startType, uint errorControl,
        string? binaryPathName, string? loadOrderGroup, IntPtr tagId, string? dependencies,
        string? serviceStartName, string? password, string? displayName);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "ChangeServiceConfig2W")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ChangeServiceConfig2(SafeServiceHandle service, uint infoLevel, ref SERVICE_DELAYED_AUTO_START_INFO info);

    // ---- Device and session facts (kernel32, user32, advapi32) ----
    public const int SM_MAXIMUMTOUCHES = 95;
    public const byte BATTERY_FLAG_NO_BATTERY = 128;
    public const uint SERVICE_QUERY_STATUS = 0x0004;
    public const uint SERVICE_RUNNING = 4;
    public const uint TOKEN_QUERY = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("kernel32.dll")]
    public static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryServiceStatus(SafeServiceHandle service, out SERVICE_STATUS status);

    public const int WTSUserName = 5;
    public const int WTSDomainName = 7;
    public const uint SERVICE_START_PENDING = 2;

    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "WTSQuerySessionInformationW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTSQuerySessionInformation(IntPtr server, uint sessionId, int infoClass, out IntPtr buffer, out uint bytesReturned);

    [DllImport("wtsapi32.dll")]
    public static extern void WTSFreeMemory(IntPtr memory);

    // ---- WinVerifyTrust (wintrust) ----
    public static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    public const uint WTD_UI_NONE = 2;
    public const uint WTD_REVOKE_NONE = 0;
    public const uint WTD_CHOICE_FILE = 1;
    public const uint WTD_CHOICE_CATALOG = 2;
    public const uint WTD_STATEACTION_VERIFY = 1;
    public const uint WTD_STATEACTION_CLOSE = 2;
    public const uint WTD_REVOCATION_CHECK_NONE = 0x00000010;
    public const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x00001000;
    public const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);

    [StructLayout(LayoutKind.Sequential)]
    public struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINTRUST_CATALOG_INFO
    {
        public uint cbStruct;
        public uint dwCatalogVersion;
        public IntPtr pcwszCatalogFilePath;
        public IntPtr pcwszMemberTag;
        public IntPtr pcwszMemberFilePath;
        public IntPtr hMemberFile;
        public IntPtr pbCalculatedFileHash;
        public uint cbCalculatedFileHash;
        public IntPtr pcCatalogContext;
        public IntPtr hCatAdmin;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pInfo;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    /// <summary>Returns 0 when trusted, otherwise an HRESULT. Not a SetLastError API.</summary>
    [DllImport("wintrust.dll", ExactSpelling = true)]
    public static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, ref WINTRUST_DATA data);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    public static extern IntPtr WTHelperProvDataFromStateData(IntPtr stateData);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    public static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provData, uint signerIndex, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterSignerIndex);

    /// <summary>Returns a CRYPT_PROVIDER_CERT*; its PCCERT_CONTEXT follows the leading DWORD (at offset IntPtr.Size).</summary>
    [DllImport("wintrust.dll", ExactSpelling = true)]
    public static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint certIndex);

    // ---- Catalog lookup (wintrust) ----
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CATALOG_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string wszCatalogFile;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CryptCATAdminAcquireContext2(out IntPtr catAdmin, IntPtr subsystem, string? hashAlgorithm, IntPtr strongHashPolicy, uint flags);

    [DllImport("wintrust.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CryptCATAdminReleaseContext(IntPtr catAdmin, uint flags);

    [DllImport("wintrust.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr catAdmin, SafeFileHandle file, ref uint hashSize, [Out] byte[]? hash, uint flags);

    [DllImport("wintrust.dll", SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr catAdmin, byte[] hash, uint hashSize, uint flags, IntPtr prevCatInfo);

    [DllImport("wintrust.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CryptCATCatalogInfoFromContext(IntPtr catInfo, ref CATALOG_INFO info, uint flags);

    [DllImport("wintrust.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CryptCATAdminReleaseCatalogContext(IntPtr catAdmin, IntPtr catInfo, uint flags);

    // ---- System Restore (srclient) ----
    public const uint BEGIN_SYSTEM_CHANGE = 100;
    public const uint END_SYSTEM_CHANGE = 101;
    public const uint APPLICATION_INSTALL = 0;
    public const int ERROR_SERVICE_DISABLED = 1058;

    /// <summary>srrestoreptapi.h declares these under pack(1).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct RESTOREPOINTINFOW
    {
        public uint dwEventType;
        public uint dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct STATEMGRSTATUS
    {
        public uint nStatus;
        public long llSequenceNumber;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SRSetRestorePointW(ref RESTOREPOINTINFOW restorePoint, out STATEMGRSTATUS status);

    // ---- Windows Installer (msi) ----
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_MORE_DATA = 234;

    /// <summary>Returns a Win32 error code (not a SetLastError API).</summary>
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern int MsiGetProductInfoW(string product, string property, [Out] char[]? value, ref uint valueLength);

    // ---- Files (kernel32) ----
    /// <summary>FILETIME is two DWORDs (4-byte aligned), so the longs need Pack = 4; native size 52.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public long ftCreationTime;
        public long ftLastAccessTime;
        public long ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFileInformationByHandle(SafeFileHandle file, out BY_HANDLE_FILE_INFORMATION info);

    // ---- File security without propagation (advapi32) ----
    public const uint OWNER_SECURITY_INFORMATION = 0x00000001;
    public const uint DACL_SECURITY_INFORMATION = 0x00000004;
    public const uint PROTECTED_DACL_SECURITY_INFORMATION = 0x80000000;

    /// <summary>Unlike SetNamedSecurityInfo, this does not push inheritable entries down to existing children.</summary>
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetFileSecurityW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetFileSecurity(string fileName, uint securityInformation, byte[] securityDescriptor);
}
