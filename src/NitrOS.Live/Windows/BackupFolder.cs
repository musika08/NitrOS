using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NitrOS.Live.Windows;

public enum FolderState { Missing, Secure }

/// <summary>
/// The journal folder and its NitrOS parent (%ProgramData%\NitrOS and %ProgramData%\NitrOS\backups), writable only by
/// Administrators and SYSTEM, so a standard user cannot plant a journal that an elevated undo would replay.
/// A level that already exists is used only if it is already secure (owner Administrators or SYSTEM, and no other SID
/// than Administrators, SYSTEM or TrustedInstaller can write to it or take it over); otherwise Ensure throws and changes
/// nothing. A missing level is created by this call with the protected ACL in place from the start (CreateDirectoryW with
/// a security descriptor), then re-applied with SetSecurityInfo on a handle opened without following links, after
/// checking the handle is that very directory. No path component may be a junction or link.
/// </summary>
public static class BackupFolder
{
    public static readonly SecurityIdentifier Administrators = SecureFs.Administrators;
    public static readonly SecurityIdentifier LocalSystem = SecureFs.LocalSystem;

    /// <summary>%ProgramData%\NitrOS\backups</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "backups");

    public static string InsecureMessage(string path) =>
        $"The NitrOS data folder was created or changed by another account. Delete {path} as an administrator and run NitrOS again.";

    /// <summary>Secures the folder and its parent. Needs an elevated process. Returns nothing to report (kept for callers): existing content is never re-permissioned.</summary>
    /// <summary>Checks whether the folder and its parent are secure without creating or modifying either.</summary>
    public static FolderState Check(string path) => Check(path, Administrators);

    internal static FolderState Check(string path, SecurityIdentifier owner)
    {
        if (!Path.IsPathFullyQualified(path) || SecureFs.IsNetworkOrDevice(path))
            throw new ArgumentException($"Backup folder '{path}' must be an absolute local path", nameof(path));
        var full = Path.GetFullPath(path).TrimEnd('\\');
        var parent = Path.GetDirectoryName(full) ?? throw new ArgumentException($"Backup folder '{path}' has no parent", nameof(path));
        if (Path.GetDirectoryName(parent) is null) throw new ArgumentException($"Backup folder '{path}' is too close to the drive root", nameof(path));

        RefuseReparseComponents(full);

        bool parentExists = Directory.Exists(parent) || File.Exists(parent);
        bool fullExists = Directory.Exists(full) || File.Exists(full);

        if (!parentExists && !fullExists) return FolderState.Missing;

        if (parentExists) CheckSecure(parent, owner);
        if (fullExists) CheckSecure(full, owner);

        return (parentExists && fullExists) ? FolderState.Secure : FolderState.Missing;
    }

    public static IReadOnlyList<string> Ensure(string path) => Ensure(path, Administrators);

    /// <summary>
    /// <paramref name="owner"/> is the SID that owns and fully controls the folders next to SYSTEM: BUILTIN\Administrators
    /// in production. Tests on a non-elevated account pass their own SID, which stands in for Administrators.
    /// </summary>
    internal static IReadOnlyList<string> Ensure(string path, SecurityIdentifier owner)
    {
        if (!Path.IsPathFullyQualified(path) || SecureFs.IsNetworkOrDevice(path))
            throw new ArgumentException($"Backup folder '{path}' must be an absolute local path", nameof(path));
        var full = Path.GetFullPath(path).TrimEnd('\\');
        var parent = Path.GetDirectoryName(full) ?? throw new ArgumentException($"Backup folder '{path}' has no parent", nameof(path));
        if (Path.GetDirectoryName(parent) is null) throw new ArgumentException($"Backup folder '{path}' is too close to the drive root", nameof(path));
        string[] levels = [parent, full];

        RefuseReparseComponents(full);
        // Check every existing level before changing anything.
        foreach (var level in levels)
            if (Directory.Exists(level) || File.Exists(level)) CheckSecure(level, owner);

        foreach (var level in levels)
            if (!Directory.Exists(level)) CreateSecure(level, owner);

        foreach (var level in levels) CheckSecure(level, owner);
        return [];
    }

    /// <summary>Throws when the path or any existing parent up to the drive root is a junction or link.</summary>
    internal static void RefuseReparseComponents(string full)
    {
        for (var p = full; p is not null; p = Path.GetDirectoryName(p))
        {
            var info = new DirectoryInfo(p);
            if (!info.Exists) continue;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Refusing the backup folder: '{p}' is a junction or link");
        }
    }

    /// <summary>The trusted SIDs: Administrators, SYSTEM, TrustedInstaller, plus the stand-in owner.</summary>
    private static IReadOnlySet<string> Trusted(SecurityIdentifier owner) =>
        new HashSet<string>(SecureFs.TrustedSids(), StringComparer.OrdinalIgnoreCase) { owner.Value };

    /// <summary>Rights on a folder that let someone add, change, remove or take over what is in it.</summary>
    public const int FolderWriteMask = SecureFs.WriteData | SecureFs.AppendData | SecureFs.WriteEa | SecureFs.DeleteChild
        | SecureFs.WriteAttributes | SecureFs.DeleteRight | SecureFs.WriteDac | SecureFs.WriteOwner | SecureFs.GenericAll | SecureFs.GenericWrite;

    /// <summary>
    /// Secure means: owner is Administrators or SYSTEM (or the stand-in), there is a DACL, and no allow entry (inherit-only
    /// included, as it shapes new journals) gives any other SID than Administrators, SYSTEM or TrustedInstaller a write right.
    /// </summary>
    public static bool IsSecure(DirectorySecurity security, SecurityIdentifier owner)
    {
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier o
            || !(o == Administrators || o == LocalSystem || o == owner)) return false;
        return !SecureFs.OthersCanWrite(security, Trusted(owner), FolderWriteMask, includeInheritOnly: true);
    }

    private static void CheckSecure(string level, SecurityIdentifier owner)
    {
        using var h = SecureFs.OpenNoFollow(level, NativeMethods.READ_CONTROL | NativeMethods.FILE_READ_ATTRIBUTES);
        var attrs = SecureFs.Attributes(h);
        if ((attrs & NativeMethods.FILE_ATTRIBUTE_REPARSE_POINT) != 0 || (attrs & NativeMethods.FILE_ATTRIBUTE_DIRECTORY) == 0
            || !SecureFs.IsAt(h, level) || !IsSecure(SecureFs.ReadSecurity<DirectorySecurity>(h), owner))
            throw new InvalidOperationException(InsecureMessage(level));
    }

    private static void CreateSecure(string level, SecurityIdentifier owner)
    {
        var sd = Descriptor(owner);
        var pin = GCHandle.Alloc(sd, GCHandleType.Pinned);
        try
        {
            var sa = new NativeMethods.SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>(),
                lpSecurityDescriptor = pin.AddrOfPinnedObject(),
            };
            if (!NativeMethods.CreateDirectory(level, ref sa))
            {
                var err = Marshal.GetLastPInvokeError();
                // Someone else created it between the check and now: use it only if it is secure.
                if (err == NativeMethods.ERROR_ALREADY_EXISTS) { CheckSecure(level, owner); return; }
                throw new Win32Exception(err, $"Could not create '{level}'");
            }
        }
        finally { pin.Free(); }

        using var h = SecureFs.OpenNoFollow(level, NativeMethods.READ_CONTROL | NativeMethods.WRITE_DAC | NativeMethods.WRITE_OWNER | NativeMethods.FILE_READ_ATTRIBUTES);
        var attrs = SecureFs.Attributes(h);
        if ((attrs & NativeMethods.FILE_ATTRIBUTE_REPARSE_POINT) != 0 || (attrs & NativeMethods.FILE_ATTRIBUTE_DIRECTORY) == 0 || !SecureFs.IsAt(h, level))
            throw new InvalidOperationException(InsecureMessage(level));
        var raw = new RawSecurityDescriptor(sd, 0);
        var ownerBytes = new byte[raw.Owner!.BinaryLength];
        raw.Owner.GetBinaryForm(ownerBytes, 0);
        var dacl = new byte[raw.DiscretionaryAcl!.BinaryLength];
        raw.DiscretionaryAcl.GetBinaryForm(dacl, 0);
        var rc = NativeMethods.SetSecurityInfo(h, NativeMethods.SE_FILE_OBJECT,
            NativeMethods.OWNER_SECURITY_INFORMATION | NativeMethods.DACL_SECURITY_INFORMATION | NativeMethods.PROTECTED_DACL_SECURITY_INFORMATION,
            ownerBytes, IntPtr.Zero, dacl, IntPtr.Zero);
        if (rc != 0) throw new Win32Exception(rc, $"Could not secure '{level}'");
    }

    /// <summary>Protected DACL: the owner SID and SYSTEM full control, inherited by new files and folders; owner set.</summary>
    private static byte[] Descriptor(SecurityIdentifier owner)
    {
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var sec = new DirectorySecurity();
        sec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        sec.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        sec.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        sec.SetOwner(owner);
        return sec.GetSecurityDescriptorBinaryForm();
    }

    /// <summary>The number of hard links to the file (1 for an ordinary file).</summary>
    internal static uint LinkCount(string file)
    {
        using var h = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!NativeMethods.GetFileInformationByHandle(h, out var info)) throw new Win32Exception($"Could not read file information of '{file}'");
        return info.nNumberOfLinks;
    }
}
