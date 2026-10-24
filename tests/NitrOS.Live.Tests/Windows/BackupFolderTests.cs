using System.Security.AccessControl;
using System.Security.Principal;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>Works only on folders inside the test's own temp folder, never ProgramData.</summary>
public sealed class BackupFolderTests : IDisposable
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";
    private static readonly SecurityIdentifier World = new(WellKnownSidType.WorldSid, null);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));

    public BackupFolderTests() => Directory.CreateDirectory(_dir);

    /// <summary>The test user still owns everything, so it may give itself access back before deleting.</summary>
    public void Dispose()
    {
        var me = WindowsIdentity.GetCurrent().User!;
        foreach (var d in new DirectoryInfo(_dir).GetDirectories()) GrantMe(d, me);
        foreach (var e in new DirectoryInfo(_dir).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            if ((e.Attributes & FileAttributes.ReparsePoint) != 0 && e is DirectoryInfo link) link.Delete();
        Directory.Delete(_dir, recursive: true);
    }

    private static void GrantMe(DirectoryInfo d, SecurityIdentifier me)
    {
        var s = new DirectorySecurity();
        s.SetAccessRuleProtection(true, false);
        s.AddAccessRule(new FileSystemAccessRule(me, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        d.SetAccessControl(s);
        if ((d.Attributes & FileAttributes.ReparsePoint) != 0) return;
        foreach (var f in d.GetFiles())
        {
            var fs = new FileSecurity();
            fs.SetAccessRuleProtection(true, false);
            fs.AddAccessRule(new FileSystemAccessRule(me, FileSystemRights.FullControl, AccessControlType.Allow));
            f.SetAccessControl(fs);
        }
        foreach (var sub in d.GetDirectories()) GrantMe(sub, me);
    }

    private static void AssertLockedDown(FileSystemSecurity sec, string what)
    {
        Assert.True(sec.AreAccessRulesProtected, what + ": inheritance from the parent must be off");
        var rules = sec.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        Assert.All(rules, r =>
        {
            Assert.Equal(AccessControlType.Allow, r.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, r.FileSystemRights);
            Assert.False(r.IsInherited);
        });
        Assert.Equal(
            new[] { BackupFolder.Administrators.Value, BackupFolder.LocalSystem.Value }.OrderBy(x => x),
            rules.Select(r => r.IdentityReference.Value).Distinct().OrderBy(x => x));
    }

    private static void AssertLockedDown(string dir) => AssertLockedDown(new DirectoryInfo(dir).GetAccessControl(), dir);

    private static void Weaken(string dir)
    {
        var weak = new DirectoryInfo(dir).GetAccessControl();
        weak.AddAccessRule(new FileSystemAccessRule(World, FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(dir).SetAccessControl(weak);
    }

    [Fact]
    public void Ensure_CreatesBothLevelsForAdministratorsAndSystemOnly()
    {
        var root = Path.Combine(_dir, "NitrOS");
        var path = Path.Combine(root, "backups");
        Assert.Empty(BackupFolder.Ensure(path, setOwner: false));
        AssertLockedDown(root);
        AssertLockedDown(path);
    }

    [Fact]
    public void Ensure_TightensPermissiveParentAndPlantedContent()
    {
        var root = Path.Combine(_dir, "NitrOS");
        var path = Path.Combine(root, "backups");
        Directory.CreateDirectory(Path.Combine(root, "other"));
        Directory.CreateDirectory(path);
        Weaken(root);
        var planted = Path.Combine(path, "planted.json");
        File.WriteAllText(planted, "{}");
        var fileSec = new FileInfo(planted).GetAccessControl();
        fileSec.AddAccessRule(new FileSystemAccessRule(World, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(planted).SetAccessControl(fileSec);

        Assert.Empty(BackupFolder.Ensure(path, setOwner: false));

        AssertLockedDown(root);
        AssertLockedDown(path);
        AssertLockedDown(Path.Combine(root, "other"));
        AssertLockedDown(new FileInfo(planted).GetAccessControl(), planted);
    }

    [Fact]
    public void Ensure_RefusesJunctionAnywhereInThePath()
    {
        var real = Path.Combine(_dir, "real");
        Directory.CreateDirectory(real);
        var link = Path.Combine(_dir, "link");
        Assert.Equal(0, ProcessRunner.Run("cmd.exe", ["/c", "mklink", "/J", link, real], TimeSpan.FromSeconds(20)).ExitCode);

        // The junction as the NitrOS level, and as the backups level.
        Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(Path.Combine(link, "backups"), setOwner: false));
        Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(link, setOwner: false));
        Assert.Empty(Directory.GetFileSystemEntries(real));
        Assert.False(new DirectoryInfo(real).GetAccessControl().AreAccessRulesProtected);
    }

    [Fact]
    public void Ensure_RefusesJunctionInsideTheFolder()
    {
        var root = Path.Combine(_dir, "NitrOS");
        var path = Path.Combine(root, "backups");
        Directory.CreateDirectory(path);
        var outside = Path.Combine(_dir, "outside");
        Directory.CreateDirectory(outside);
        Assert.Equal(0, ProcessRunner.Run("cmd.exe", ["/c", "mklink", "/J", Path.Combine(path, "link"), outside], TimeSpan.FromSeconds(20)).ExitCode);

        Assert.Throws<InvalidOperationException>(() => BackupFolder.Ensure(path, setOwner: false));
        Assert.False(new DirectoryInfo(outside).GetAccessControl().AreAccessRulesProtected);
    }

    [Fact]
    public void Ensure_SkipsHardLinkedFiles()
    {
        var root = Path.Combine(_dir, "NitrOS");
        var path = Path.Combine(root, "backups");
        Directory.CreateDirectory(path);
        var elsewhere = Path.Combine(_dir, "elsewhere.txt");
        File.WriteAllText(elsewhere, "x");
        var linked = Path.Combine(path, "linked.json");
        Assert.Equal(0, ProcessRunner.Run("cmd.exe", ["/c", "mklink", "/H", linked, elsewhere], TimeSpan.FromSeconds(20)).ExitCode);
        Assert.Equal(2u, BackupFolder.LinkCount(elsewhere));
        var before = new FileInfo(elsewhere).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);

        var skipped = BackupFolder.Ensure(path, setOwner: false);

        Assert.Equal(new[] { linked }, skipped);
        Assert.Equal(before, new FileInfo(elsewhere).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
        AssertLockedDown(path);
    }

    [Fact]
    public void Ensure_RejectsBadPaths()
    {
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure("backups"));
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure(@"C:\backups"));
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure(@"\\server\share\NitrOS\backups"));
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
        {
            AssertLockedDown(level);
            Assert.Equal(BackupFolder.Administrators, new DirectoryInfo(level).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
        }
    }
}
