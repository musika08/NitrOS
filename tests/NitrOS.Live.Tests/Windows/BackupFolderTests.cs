using System.Security.AccessControl;
using System.Security.Principal;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>
/// Works only on folders inside the test's own temp folder, never ProgramData. The test account is not elevated, so its
/// own SID stands in for BUILTIN\Administrators (internal Ensure overload).
/// </summary>
public sealed class BackupFolderTests : IDisposable
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";
    private static readonly SecurityIdentifier World = new(WellKnownSidType.WorldSid, null);
    private static readonly SecurityIdentifier Me = WindowsIdentity.GetCurrent().User!;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));

    public BackupFolderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var e in new DirectoryInfo(_dir).EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            if ((e.Attributes & FileAttributes.ReparsePoint) != 0 && e is DirectoryInfo link) link.Delete();
        Directory.Delete(_dir, recursive: true);
    }

    private static string Sddl(string dir) =>
        new DirectoryInfo(dir).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner);

    private static void AssertLockedDown(string dir, SecurityIdentifier owner)
    {
        var sec = new DirectoryInfo(dir).GetAccessControl();
        Assert.True(sec.AreAccessRulesProtected, dir + ": inheritance from the parent must be off");
        Assert.Equal(owner, sec.GetOwner(typeof(SecurityIdentifier)));
        var rules = sec.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        Assert.All(rules, r =>
        {
            Assert.Equal(AccessControlType.Allow, r.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, r.FileSystemRights);
            Assert.False(r.IsInherited);
        });
        Assert.Equal(new[] { owner.Value, BackupFolder.LocalSystem.Value }.OrderBy(x => x),
            rules.Select(r => r.IdentityReference.Value).Distinct().OrderBy(x => x));
    }

    private static void Weaken(string dir)
    {
        var weak = new DirectoryInfo(dir).GetAccessControl();
        weak.AddAccessRule(new FileSystemAccessRule(World, FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(dir).SetAccessControl(weak);
    }

    [Fact]
    public void Ensure_CreatesBothLevelsLockedDown()
    {
        var root = Path.Combine(_dir, "NitrOS");
        var path = Path.Combine(root, "backups");
        Assert.Empty(BackupFolder.Ensure(path, Me));
        AssertLockedDown(root, Me);
        AssertLockedDown(path, Me);
    }

    [Fact]
    public void Ensure_ReusesAlreadySecureFoldersWithoutChanges()
    {
        var root = Path.Combine(_dir, "NitrOS");
        var path = Path.Combine(root, "backups");
        BackupFolder.Ensure(path, Me);
        var (r, p) = (Sddl(root), Sddl(path));
        BackupFolder.Ensure(path, Me);
        Assert.Equal(r, Sddl(root));
        Assert.Equal(p, Sddl(path));
    }

    [Fact]
    public void Ensure_RefusesPermissiveExistingParentAndChangesNothing()
    {
        var root = Path.Combine(_dir, "NitrOS");
        Directory.CreateDirectory(root);
        Weaken(root);
        var before = Sddl(root);

        var e = Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(Path.Combine(root, "backups"), Me));

        Assert.Equal(BackupFolder.InsecureMessage(root), e.Message);
        Assert.StartsWith("The NitrOS data folder was created or changed by another account. Delete ", e.Message);
        Assert.Equal(before, Sddl(root));
        Assert.False(Directory.Exists(Path.Combine(root, "backups")));
    }

    [Fact]
    public void Ensure_RefusesPermissiveExistingBackupsAndChangesNothing()
    {
        var root = Path.Combine(_dir, "NitrOS");
        var path = Path.Combine(root, "backups");
        Directory.CreateDirectory(path);
        Weaken(path);
        var (r, p) = (Sddl(root), Sddl(path));

        var e = Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(path, Me));

        Assert.Equal(BackupFolder.InsecureMessage(path), e.Message);
        Assert.Equal(r, Sddl(root));
        Assert.Equal(p, Sddl(path));
    }

    [Fact]
    public void Ensure_RefusesAFileWhereTheFolderShouldBe()
    {
        var root = Path.Combine(_dir, "NitrOS");
        File.WriteAllText(root, "x");
        Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(Path.Combine(root, "backups"), Me));
        Assert.Equal("x", File.ReadAllText(root));
    }

    [Fact]
    public void IsSecure_Rules()
    {
        DirectorySecurity Make(SecurityIdentifier owner, params FileSystemAccessRule[] rules)
        {
            var s = new DirectorySecurity();
            s.SetAccessRuleProtection(true, false);
            s.SetOwner(owner);
            foreach (var r in rules) s.AddAccessRule(r);
            return s;
        }
        const InheritanceFlags ci = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var admins = BackupFolder.Administrators;
        var system = BackupFolder.LocalSystem;
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var ti = new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");
        FileSystemAccessRule Full(SecurityIdentifier s) => new(s, FileSystemRights.FullControl, ci, PropagationFlags.None, AccessControlType.Allow);

        Assert.True(BackupFolder.IsSecure(Make(admins, Full(admins), Full(system)), admins));
        Assert.True(BackupFolder.IsSecure(Make(system, Full(admins), Full(system), Full(ti)), admins));
        Assert.True(BackupFolder.IsSecure(Make(admins, Full(admins), new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute, ci, PropagationFlags.None, AccessControlType.Allow)), admins));
        // Untrusted owner.
        Assert.False(BackupFolder.IsSecure(Make(users, Full(admins)), admins));
        Assert.False(BackupFolder.IsSecure(Make(ti, Full(admins)), admins));
        // Any other SID with a write right, inherit-only included (it would apply to new journals).
        Assert.False(BackupFolder.IsSecure(Make(admins, Full(admins), new FileSystemAccessRule(users, FileSystemRights.CreateFiles, AccessControlType.Allow)), admins));
        Assert.False(BackupFolder.IsSecure(Make(admins, Full(admins), new FileSystemAccessRule(users, FileSystemRights.Modify, ci, PropagationFlags.InheritOnly, AccessControlType.Allow)), admins));
        Assert.False(BackupFolder.IsSecure(Make(admins, Full(admins), new FileSystemAccessRule(new SecurityIdentifier("S-1-3-0"), FileSystemRights.FullControl, ci, PropagationFlags.InheritOnly, AccessControlType.Allow)), admins));
        Assert.False(BackupFolder.IsSecure(Make(admins, Full(admins), new FileSystemAccessRule(users, FileSystemRights.TakeOwnership, AccessControlType.Allow)), admins));
    }

    [Fact]
    public void Ensure_RefusesJunctionAnywhereInThePath()
    {
        var real = Path.Combine(_dir, "real");
        Directory.CreateDirectory(real);
        var before = Sddl(real);
        var link = Path.Combine(_dir, "link");
        Assert.Equal(0, ProcessRunner.Run("cmd.exe", ["/c", "mklink", "/J", link, real], TimeSpan.FromSeconds(20)).ExitCode);

        // The junction as the NitrOS level, and as the backups level.
        Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(Path.Combine(link, "backups"), Me));
        Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(link, Me));
        Assert.Empty(Directory.GetFileSystemEntries(real));
        Assert.Equal(before, Sddl(real));
    }

    [Fact]
    public void Ensure_RejectsBadPaths()
    {
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure("backups"));
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure(@"C:\backups"));
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure(@"\\server\share\NitrOS\backups"));
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure(@"\??\C:\Temp\NitrOS\backups"));
    }

    [Fact]
    public void LinkCount_CountsHardLinks()
    {
        var a = Path.Combine(_dir, "a.json");
        File.WriteAllText(a, "{}");
        Assert.Equal(1u, BackupFolder.LinkCount(a));
        Assert.Equal(0, ProcessRunner.Run("cmd.exe", ["/c", "mklink", "/H", Path.Combine(_dir, "b.json"), a], TimeSpan.FromSeconds(20)).ExitCode);
        Assert.Equal(2u, BackupFolder.LinkCount(a));
    }

    [Fact]
    public void DefaultPath_IsUnderProgramData()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "backups"), WinSystems.BackupPath);
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinSystems_Create_LocksDownProgramDataFolders()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: creates and locks %ProgramData%\\NitrOS\\backups (set NITROS_VM=1 inside the elevated VM)");
        var systems = WinSystems.Create();
        Assert.NotNull(systems.RestorePoint);
        foreach (var level in new[] { Path.GetDirectoryName(WinSystems.BackupPath)!, WinSystems.BackupPath })
            AssertLockedDown(level, BackupFolder.Administrators);
    }
}
