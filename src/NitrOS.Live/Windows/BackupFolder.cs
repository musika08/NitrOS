using System.Security.AccessControl;
using System.Security.Principal;

namespace NitrOS.Live.Windows;

/// <summary>
/// Makes the journal folder writable only by Administrators and SYSTEM, so a standard user cannot plant a journal
/// that an elevated undo would replay. An existing folder with a weaker ACL is tightened; anything already inside it
/// loses its explicit permissions and inherits the folder's.
/// </summary>
public static class BackupFolder
{
    public static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    public static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>%ProgramData%\NitrOS\backups</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "backups");

    public static void Ensure(string path) => Ensure(path, setOwner: true);

    /// <summary>
    /// setOwner makes Administrators the owner (needs an elevated process). Tests on a non-elevated account pass false;
    /// the owner then stays the current user, who keeps the owner's implicit right to read and change the ACL.
    /// </summary>
    internal static void Ensure(string path, bool setOwner)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException($"Backup folder '{path}' is not absolute", nameof(path));
        var dir = new DirectoryInfo(path);
        if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"Backup folder '{path}' is a junction or link");
        var existed = dir.Exists;
        if (!existed) dir.Create();

        // List existing children first: once the folder is locked, a non-elevated caller can no longer list it.
        var children = existed ? ExistingChildren(dir) : [];
        dir.SetAccessControl(ProtectedSecurity(setOwner));
        ResetChildren(children, setOwner);
    }

    private static DirectorySecurity ProtectedSecurity(bool setOwner)
    {
        var sec = new DirectorySecurity();
        sec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        sec.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        sec.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        if (setOwner) sec.SetOwner(Administrators);
        return sec;
    }

    private static List<FileSystemInfo> ExistingChildren(DirectoryInfo dir)
    {
        var list = dir.EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false }).ToList();
        foreach (var e in list)
            if ((e.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Backup folder contains the junction or link '{e.FullName}'");
        return list;
    }

    private static void ResetChildren(List<FileSystemInfo> children, bool setOwner)
    {
        foreach (var e in children)
        {
            if (e is DirectoryInfo d)
            {
                var s = new DirectorySecurity();
                s.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
                if (setOwner) s.SetOwner(Administrators);
                d.SetAccessControl(s);
            }
            else if (e is FileInfo f)
            {
                var s = new FileSecurity();
                s.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
                if (setOwner) s.SetOwner(Administrators);
                f.SetAccessControl(s);
            }
        }
    }
}
