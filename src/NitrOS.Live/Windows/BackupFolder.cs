using System.ComponentModel;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NitrOS.Live.Windows;

/// <summary>
/// Locks the journal folder and its NitrOS parent (%ProgramData%\NitrOS and %ProgramData%\NitrOS\backups) so only
/// Administrators and SYSTEM can write, and a standard user cannot plant a journal that an elevated undo would replay.
/// Every component of the path must not be a junction or link. Each level, and everything already inside, gets owner
/// Administrators and a protected DACL (Administrators + SYSTEM full control). Permissions are written with
/// SetFileSecurityW, which does not propagate to children, and files with more than one hard link are skipped (and
/// returned) so re-permissioning can never reach a file elsewhere on the disk.
/// </summary>
public static class BackupFolder
{
    public static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    public static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>%ProgramData%\NitrOS\backups</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "backups");

    /// <summary>Secures the folder and its parent. Returns the hard-linked files that were skipped. Needs an elevated process.</summary>
    public static IReadOnlyList<string> Ensure(string path) => Ensure(path, setOwner: true);

    /// <summary>
    /// setOwner makes Administrators the owner (needs an elevated process). Tests on a non-elevated account pass false;
    /// the owner then stays the current user, who keeps the owner's implicit right to read and change the ACL.
    /// </summary>
    internal static IReadOnlyList<string> Ensure(string path, bool setOwner)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException($"Backup folder '{path}' is not absolute", nameof(path));
        var full = Path.GetFullPath(path).TrimEnd('\\');
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException($"Backup folder '{path}' is a network or device path", nameof(path));
        var parent = Path.GetDirectoryName(full) ?? throw new ArgumentException($"Backup folder '{path}' has no parent", nameof(path));
        if (Path.GetDirectoryName(parent) is null) throw new ArgumentException($"Backup folder '{path}' is too close to the drive root", nameof(path));

        RefuseReparseComponents(full);
        Directory.CreateDirectory(full);
        RefuseReparseComponents(full);

        // List first: once a level is locked, a non-elevated caller can no longer list it.
        var children = new List<FileSystemInfo>();
        foreach (var e in new DirectoryInfo(parent).EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false }))
        {
            if ((e.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"NitrOS folder contains the junction or link '{e.FullName}'");
            if (!string.Equals(e.FullName.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase)) children.Add(e);
        }
        var skipped = children.OfType<FileInfo>().Where(f => LinkCount(f.FullName) > 1).Select(f => f.FullName).ToList();

        Apply(parent, Descriptor(isDirectory: true, setOwner), setOwner);
        Apply(full, Descriptor(isDirectory: true, setOwner), setOwner);
        var dirSd = Descriptor(isDirectory: true, setOwner);
        var fileSd = Descriptor(isDirectory: false, setOwner);
        foreach (var c in children)
        {
            if (c is FileInfo && skipped.Contains(c.FullName, StringComparer.OrdinalIgnoreCase)) continue;
            Apply(c.FullName, c is DirectoryInfo ? dirSd : fileSd, setOwner);
        }
        return skipped;
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

    /// <summary>Protected DACL: Administrators and SYSTEM full control (inheritable on folders); owner Administrators when asked.</summary>
    private static byte[] Descriptor(bool isDirectory, bool setOwner)
    {
        var inherit = isDirectory ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None;
        FileSystemSecurity sec = isDirectory ? new DirectorySecurity() : new FileSecurity();
        sec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        sec.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        sec.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        sec.SetOwner(setOwner ? Administrators : WindowsIdentity.GetCurrent().User!);
        return sec.GetSecurityDescriptorBinaryForm();
    }

    private static void Apply(string path, byte[] descriptor, bool setOwner)
    {
        var info = NativeMethods.DACL_SECURITY_INFORMATION | NativeMethods.PROTECTED_DACL_SECURITY_INFORMATION;
        if (setOwner) info |= NativeMethods.OWNER_SECURITY_INFORMATION;
        if (!NativeMethods.SetFileSecurity(path, info, descriptor))
            throw new Win32Exception($"Could not secure '{path}'");
    }

    /// <summary>The number of hard links to the file (1 for an ordinary file).</summary>
    internal static uint LinkCount(string file)
    {
        using var h = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!NativeMethods.GetFileInformationByHandle(h, out var info)) throw new Win32Exception($"Could not read file information of '{file}'");
        return info.nNumberOfLinks;
    }
}
