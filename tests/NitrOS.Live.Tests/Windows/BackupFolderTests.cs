using System.Security.AccessControl;
using System.Security.Principal;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>Works only on folders inside the test's own temp folder, never ProgramData.</summary>
public sealed class BackupFolderTests : IDisposable
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));

    public BackupFolderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        // The test user still owns everything, so it may restore its own access before deleting.
        var me = WindowsIdentity.GetCurrent().User!;
        foreach (var d in Directory.Exists(_dir) ? new DirectoryInfo(_dir).EnumerateDirectories("*", SearchOption.TopDirectoryOnly) : [])
            GrantMe(d, me);
        Directory.Delete(_dir, recursive: true);
    }

    private static void GrantMe(DirectoryInfo d, SecurityIdentifier me)
    {
        var s = new DirectorySecurity();
        s.SetAccessRuleProtection(true, false);
        s.AddAccessRule(new FileSystemAccessRule(me, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        d.SetAccessControl(s);
        foreach (var f in d.GetFiles("*", new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = true }))
        {
            var fs = new FileSecurity();
            fs.SetAccessRuleProtection(false, false);
            f.SetAccessControl(fs);
        }
    }

    private static void AssertLockedDown(string path)
    {
        var sec = new DirectoryInfo(path).GetAccessControl();
        Assert.True(sec.AreAccessRulesProtected, "inheritance from the parent must be off");
        var rules = sec.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        Assert.Equal(2, rules.Count);
        Assert.All(rules, r =>
        {
            Assert.Equal(AccessControlType.Allow, r.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, r.FileSystemRights);
            Assert.False(r.IsInherited);
        });
        Assert.Equal(
            new[] { BackupFolder.Administrators.Value, BackupFolder.LocalSystem.Value }.OrderBy(x => x),
            rules.Select(r => r.IdentityReference.Value).OrderBy(x => x));
    }

    [Fact]
    public void Ensure_CreatesFolderForAdministratorsAndSystemOnly()
    {
        var path = Path.Combine(_dir, "backups");
        BackupFolder.Ensure(path, setOwner: false);
        Assert.True(Directory.Exists(path));
        AssertLockedDown(path);
    }

    [Fact]
    public void Ensure_TightensExistingWeakFolderAndItsFiles()
    {
        var path = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(path);
        var planted = Path.Combine(path, "planted.json");
        File.WriteAllText(planted, "{}");
        // Give Everyone write access, as a planted folder might have.
        var weak = new DirectoryInfo(path).GetAccessControl();
        weak.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(weak);
        var fileSec = new FileInfo(planted).GetAccessControl();
        fileSec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(planted).SetAccessControl(fileSec);

        BackupFolder.Ensure(path, setOwner: false);

        AssertLockedDown(path);
        var fileRules = new FileInfo(planted).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        Assert.All(fileRules, r => Assert.True(r.IsInherited, "the planted file must keep no explicit permission"));
        Assert.DoesNotContain(fileRules, r => r.IdentityReference.Value == new SecurityIdentifier(WellKnownSidType.WorldSid, null).Value);
    }

    [Fact]
    public void Ensure_RejectsRelativePath()
    {
        Assert.Throws<ArgumentException>(() => BackupFolder.Ensure("backups"));
    }

    [Fact]
    public void DefaultPath_IsUnderProgramData()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "backups"), WinSystems.BackupPath);
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinSystems_Create_LocksDownProgramDataFolder()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: creates and locks %ProgramData%\\NitrOS\\backups (set NITROS_VM=1 inside the elevated VM)");
        var systems = WinSystems.Create();
        Assert.NotNull(systems.RestorePoint);
        AssertLockedDown(WinSystems.BackupPath);
        Assert.Equal(BackupFolder.Administrators, new DirectoryInfo(WinSystems.BackupPath).GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
    }
}
