using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace NitrOS.Live.Windows;

/// <summary>Handle-based helpers shared by the uninstaller and the backup folder: final paths, security reads, allow-list checks.</summary>
internal static class SecureFs
{
    public static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    public static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    public static readonly SecurityIdentifier TrustedInstaller = new("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");

    /// <summary>Administrators, SYSTEM and TrustedInstaller.</summary>
    public static IReadOnlySet<string> TrustedSids() =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Administrators.Value, LocalSystem.Value, TrustedInstaller.Value };

    // Rights that change an object or its contents.
    public const int WriteData = 0x2, AppendData = 0x4, WriteEa = 0x10, DeleteChild = 0x40, WriteAttributes = 0x100;
    public const int DeleteRight = 0x10000, WriteDac = 0x40000, WriteOwner = 0x80000, GenericAll = 0x10000000, GenericWrite = 0x40000000;

    /// <summary>
    /// True when the owner is not trusted, or an allow entry for an untrusted SID has one of the rights. Inherit-only
    /// entries count only when <paramref name="includeInheritOnly"/> (they shape future children). A missing DACL counts.
    /// </summary>
    public static bool OthersCanWrite(FileSystemSecurity security, IReadOnlySet<string> trusted, int mask, bool includeInheritOnly)
    {
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !trusted.Contains(owner.Value)) return true;
        var raw = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0);
        if (raw.DiscretionaryAcl is null) return true;
        foreach (FileSystemAccessRule r in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (r.AccessControlType != AccessControlType.Allow) continue;
            if (!includeInheritOnly && (r.PropagationFlags & PropagationFlags.InheritOnly) != 0) continue;
            if (trusted.Contains(r.IdentityReference.Value)) continue;
            if (((int)r.FileSystemRights & mask) != 0) return true;
        }
        return false;
    }

    /// <summary>Opens a file or folder itself (never what a link points to), sharing everything, for reading its security and attributes.</summary>
    public static SafeFileHandle OpenNoFollow(string path, uint access)
    {
        var h = NativeMethods.CreateFile(path, access,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_DELETE, IntPtr.Zero,
            NativeMethods.OPEN_EXISTING, NativeMethods.FILE_FLAG_BACKUP_SEMANTICS | NativeMethods.FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
        if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not open '{path}'");
        return h;
    }

    public static uint Attributes(SafeFileHandle h)
    {
        if (!NativeMethods.GetFileInformationByHandle(h, out var info)) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not read file information");
        return info.dwFileAttributes;
    }

    /// <summary>Owner and DACL of the open object.</summary>
    public static T ReadSecurity<T>(SafeFileHandle h) where T : FileSystemSecurity, new()
    {
        var rc = NativeMethods.GetSecurityInfo(h, NativeMethods.SE_FILE_OBJECT,
            NativeMethods.OWNER_SECURITY_INFORMATION | NativeMethods.DACL_SECURITY_INFORMATION,
            out _, out _, out _, out _, out var sd);
        if (rc != 0) throw new Win32Exception(rc, "GetSecurityInfo failed");
        try
        {
            var bytes = new byte[NativeMethods.GetSecurityDescriptorLength(sd)];
            Marshal.Copy(sd, bytes, 0, bytes.Length);
            var sec = new T();
            sec.SetSecurityDescriptorBinaryForm(bytes, AccessControlSections.Owner | AccessControlSections.Access);
            return sec;
        }
        finally { NativeMethods.LocalFree(sd); }
    }

    /// <summary>The normalised DOS path the handle really refers to ("\\?\" removed; network paths come back as "\\server\...").</summary>
    public static string FinalPath(SafeFileHandle h)
    {
        var buf = new char[512];
        while (true)
        {
            var n = NativeMethods.GetFinalPathNameByHandle(h, buf, (uint)buf.Length, 0);
            if (n == 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetFinalPathNameByHandle failed");
            if (n < buf.Length)
            {
                var p = new string(buf, 0, (int)n);
                if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + p[8..];
                if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) return p[4..];
                return p;
            }
            buf = new char[n + 1];
        }
    }

    /// <summary>The long-name form of an existing path (8.3 names expanded); the input when that fails.</summary>
    public static string LongPath(string path)
    {
        var n = NativeMethods.GetLongPathName(path, null, 0);
        if (n == 0) return path;
        var buf = new char[n];
        var m = NativeMethods.GetLongPathName(path, buf, n);
        return m == 0 || m >= n ? path : new string(buf, 0, (int)m);
    }

    /// <summary>True when the open handle is exactly the expected path (no junction, link or substitution on the way).</summary>
    public static bool IsAt(SafeFileHandle h, string expected) =>
        string.Equals(Normalise(LongPath(Path.GetFullPath(expected))), Normalise(FinalPath(h)), StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string p) => p.Length > 3 ? p.TrimEnd('\\') : p;

    /// <summary>\\server, \\?\, \\.\ and \??\ forms are all refused by callers.</summary>
    public static bool IsNetworkOrDevice(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)
        || path.StartsWith(@"\??\", StringComparison.Ordinal) || path.StartsWith("/??/", StringComparison.Ordinal);
}
