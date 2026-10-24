using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>Nothing here runs an uninstaller outside the VM lane: on this PC only registry reads, signature checks and temp-folder files.</summary>
public sealed class WinUninstallerTests : IDisposable
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";
    private static string Sys32 => Environment.SystemDirectory;
    private const string Guid1 = "{12345678-1234-1234-1234-1234567890AB}";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));

    public WinUninstallerTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static UninstallEntry Entry(string? quiet, string? uninstall, string publisher = "Contoso Ltd.") =>
        new(@"HKLM\Test", "Test App", publisher, "1.0", quiet, uninstall);

    private static UninstallEntry? Steam() =>
        new WinUninstaller().Find("Steam", "Valve Corporation").FirstOrDefault();

    // ---- On-PC, read-only ----

    [Fact]
    public void WinUninstaller_FindsSteamEntry()
    {
        var steam = Steam();
        if (steam is null) Assert.Skip("Steam is not installed on this PC");
        Assert.Equal("Steam", steam.DisplayName);
        Assert.Equal("Valve Corporation", steam.Publisher);
        Assert.StartsWith(@"HKLM\", steam.KeyPath);
        Assert.False(string.IsNullOrWhiteSpace(steam.UninstallString));
    }

    [Fact]
    public void WinUninstaller_SignerMatchesSteam()
    {
        var steam = Steam();
        if (steam is null) Assert.Skip("Steam is not installed on this PC");
        var exe = WinUninstaller.Parse(WinUninstaller.Compose(steam, null)).Executable;

        // The signature itself verifies as Valve's (read through a locked handle).
        var names = WinUninstaller.TrustedSignerNames(exe);
        Assert.NotNull(names);
        Assert.Contains(names!, n => WinUninstaller.NamesMatch(n, "Valve Corporation"));
        Assert.DoesNotContain(names!, n => WinUninstaller.NamesMatch(n, "Microsoft Corporation"));

        // Steam grants BUILTIN\Users full control on its folder, so a standard user could swap the uninstaller: refused.
        var u = new WinUninstaller();
        if (WinUninstaller.IsUserWritable(exe))
        {
            var e = Assert.Throws<UninstallRefusedException>(() => u.SignerMatches(steam, "Valve Corporation"));
            Assert.Equal(WinUninstaller.UserWritableDetail, e.Message);
        }
        else
        {
            Assert.True(u.SignerMatches(steam, "Valve Corporation"));
            Assert.False(u.SignerMatches(steam, "Microsoft Corporation"));
        }
    }

    [Fact]
    public void WinUninstaller_MsiExecIsMicrosoftSignedViaCatalog()
    {
        var names = WinUninstaller.TrustedSignerNames(Path.Combine(Sys32, "msiexec.exe"));
        Assert.NotNull(names);
        Assert.Contains(names!, n => WinUninstaller.NamesMatch(n, "Microsoft Corporation"));
    }

    [Fact]
    public void WinUninstaller_MadeUpMsiProductNeverMatches()
    {
        // A product code nobody installed has no cached package, so no publisher can match.
        Assert.Null(WinUninstaller.LocalPackage(Guid1));
        var u = new WinUninstaller();
        var msi = Entry(null, "MsiExec.exe /X" + Guid1, "Contoso Ltd.");
        Assert.False(u.SignerMatches(msi, "Contoso Ltd."));
        Assert.False(u.SignerMatches(msi, "Microsoft Corporation"));
    }

    [Fact]
    public void WinUninstaller_LocalPackageOfAnInstalledMsiProduct()
    {
        // Read-only: finds one MSI product in the 64-bit Uninstall key and resolves its cached .msi.
        using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var root = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        var code = root?.GetSubKeyNames().FirstOrDefault(n => Guid.TryParse(n, out _) && n.StartsWith('{')
            && WinUninstaller.LocalPackage(n) is { } pkg && File.Exists(pkg));
        if (code is null) Assert.Skip("No MSI product with a cached package on this PC");
        var package = WinUninstaller.LocalPackage(code)!;
        Assert.EndsWith(".msi", package, StringComparison.OrdinalIgnoreCase);
        Assert.False(WinUninstaller.IsUserWritable(package));
    }

    [Fact]
    public void WinUninstaller_ProgramInUserWritableFolderIsRefused()
    {
        var fake = Path.Combine(_dir, "uninstall.exe");
        File.WriteAllBytes(fake, [0x4D, 0x5A, 0, 0]);
        Assert.True(WinUninstaller.IsUserWritable(fake));
        var u = new WinUninstaller();
        var e = Assert.Throws<UninstallRefusedException>(() => u.SignerMatches(Entry(null, "\"" + fake + "\" /S"), "Contoso Ltd."));
        Assert.Equal("per-user program in a user-writable folder; remove it from Settings > Apps", e.Message);
    }

    [Fact]
    public void WinUninstaller_ProgramInWindowsFolderIsRefused()
    {
        var u = new WinUninstaller();
        var e = Assert.Throws<UninstallRefusedException>(() => u.SignerMatches(Entry(null, "\"" + Path.Combine(Sys32, "cmd.exe") + "\" /c exit"), "Microsoft Corporation"));
        Assert.Contains("Windows folder", e.Message);
        Assert.Throws<UninstallRefusedException>(() => WinUninstaller.CheckLocation(Path.Combine(Sys32, "cmd.exe"), false));
        WinUninstaller.CheckLocation(Path.Combine(Sys32, "msiexec.exe"), true);
    }

    [Fact]
    public void WinUninstaller_MissingOrUnparsableProgramDoesNotMatch()
    {
        var u = new WinUninstaller();
        Assert.False(u.SignerMatches(Entry(null, Path.Combine(_dir, "missing.exe")), "Contoso Ltd."));
        Assert.False(u.SignerMatches(Entry(null, "uninstall.exe /S"), "Contoso Ltd."));
        Assert.False(u.SignerMatches(Entry(null, null), "Contoso Ltd."));
        Assert.False(u.SignerMatches(Entry(null, @"\\server\share\un.exe"), "Contoso Ltd."));
    }

    [Fact]
    public void WinUninstaller_NoMatchForUnknownProgram()
    {
        Assert.Empty(new WinUninstaller().Find("NitrOS No Such Program 7f3a*", "Nobody"));
    }

    [Fact]
    public void OpenLocked_FileCannotBeReplacedWhileHeld()
    {
        // Pure file-handle test in the temp folder; nothing is executed.
        var target = Path.Combine(_dir, "held.exe");
        var other = Path.Combine(_dir, "other.exe");
        File.WriteAllText(target, "original");
        File.WriteAllText(other, "swapped");
        using (WinUninstaller.OpenLocked(target))
        {
            Blocked(() => File.Copy(other, target, overwrite: true));
            Blocked(() => File.Move(other, target, overwrite: true));
            Blocked(() => File.Delete(target));
            Blocked(() => File.Move(target, Path.Combine(_dir, "renamed.exe")));
            Assert.Equal("original", File.ReadAllText(target)); // reading is still allowed
        }
        File.Copy(other, target, overwrite: true);
        Assert.Equal("swapped", File.ReadAllText(target));
    }

    /// <summary>Sharing violations surface as IOException, a replace onto an open file as UnauthorizedAccessException.</summary>
    private static void Blocked(Action a)
    {
        var e = Record.Exception(a);
        Assert.True(e is IOException or UnauthorizedAccessException, "expected the change to be blocked, got " + (e?.GetType().Name ?? "no exception"));
    }

    // ---- Pure: write-permission rules (allow-list) ----

    private static readonly IReadOnlySet<string> Trusted = new HashSet<string>
    {
        "S-1-5-32-544", "S-1-5-18", "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464",
    };
    private static readonly SecurityIdentifier Admins = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);
    private static readonly SecurityIdentifier SomeGroup = new("S-1-5-21-1-2-3-1500");

    private static FileSecurity Sec(params FileSystemAccessRule[] rules)
    {
        var s = new FileSecurity();
        s.SetOwner(Admins);
        foreach (var r in rules) s.AddAccessRule(r);
        return s;
    }

    [Fact]
    public void OthersCanWrite_AnyUntrustedSidWithWriteRights()
    {
        var readOnly = Sec(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, AccessControlType.Allow),
            new FileSystemAccessRule(Admins, FileSystemRights.FullControl, AccessControlType.Allow),
            new FileSystemAccessRule(new SecurityIdentifier("S-1-5-18"), FileSystemRights.FullControl, AccessControlType.Allow));
        Assert.False(WinUninstaller.OthersCanWrite(readOnly, Trusted, WinUninstaller.FileWriteMask));

        // Not just the well-known user groups: any other SID (a custom group here) counts.
        Assert.True(WinUninstaller.OthersCanWrite(Sec(new FileSystemAccessRule(SomeGroup, FileSystemRights.Modify, AccessControlType.Allow)), Trusted, WinUninstaller.FileWriteMask));
        Assert.True(WinUninstaller.OthersCanWrite(Sec(new FileSystemAccessRule(Users, FileSystemRights.WriteData, AccessControlType.Allow)), Trusted, WinUninstaller.FileWriteMask));
        Assert.True(WinUninstaller.OthersCanWrite(Sec(new FileSystemAccessRule(SomeGroup, FileSystemRights.ChangePermissions, AccessControlType.Allow)), Trusted, WinUninstaller.FileWriteMask));
        Assert.True(WinUninstaller.OthersCanWrite(Sec(new FileSystemAccessRule(SomeGroup, FileSystemRights.TakeOwnership, AccessControlType.Allow)), Trusted, WinUninstaller.FileWriteMask));
        Assert.True(WinUninstaller.OthersCanWrite(Sec(new FileSystemAccessRule(SomeGroup, FileSystemRights.FullControl, AccessControlType.Allow)), Trusted, WinUninstaller.FileWriteMask));
        // TrustedInstaller is trusted.
        Assert.False(WinUninstaller.OthersCanWrite(Sec(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"), FileSystemRights.FullControl, AccessControlType.Allow)), Trusted, WinUninstaller.FileWriteMask));
    }

    [Fact]
    public void OthersCanWrite_UntrustedOwnerCounts()
    {
        var s = Sec(new FileSystemAccessRule(Admins, FileSystemRights.FullControl, AccessControlType.Allow));
        s.SetOwner(SomeGroup);
        Assert.True(WinUninstaller.OthersCanWrite(s, Trusted, WinUninstaller.FileWriteMask));
        s.SetOwner(new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"));
        Assert.False(WinUninstaller.OthersCanWrite(s, Trusted, WinUninstaller.FileWriteMask));
    }

    [Fact]
    public void OthersCanWrite_AncestorMaskIgnoresCreatingSubfolders()
    {
        // C:\ lets Authenticated Users create folders (AppendData on the root itself): that alone cannot swap an existing path.
        var root = new DirectorySecurity();
        root.SetOwner(Admins);
        root.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-11"), FileSystemRights.AppendData, AccessControlType.Allow));
        Assert.False(WinUninstaller.OthersCanWrite(root, Trusted, WinUninstaller.AncestorWriteMask));
        root.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-11"), FileSystemRights.DeleteSubdirectoriesAndFiles, AccessControlType.Allow));
        Assert.True(WinUninstaller.OthersCanWrite(root, Trusted, WinUninstaller.AncestorWriteMask));

        // The program's own folder: creating a file there plants a DLL next to it.
        var own = new DirectorySecurity();
        own.SetOwner(Admins);
        own.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.CreateFiles, AccessControlType.Allow));
        Assert.True(WinUninstaller.OthersCanWrite(own, Trusted, WinUninstaller.FolderWriteMask));
    }

    [Fact]
    public void OthersCanWrite_SkipsInheritOnlyEntries()
    {
        var d = new DirectorySecurity();
        d.SetOwner(Admins);
        d.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
        Assert.False(WinUninstaller.OthersCanWrite(d, Trusted, WinUninstaller.FolderWriteMask));
    }

    [Fact]
    public void IsUserWritable_TempFolderFileOwnedByTheTestUser()
    {
        // Temp-folder file: owned by (and writable for) the current user, who is not on the allow-list.
        var f = Path.Combine(_dir, "a.exe");
        File.WriteAllText(f, "x");
        Assert.True(WinUninstaller.IsUserWritable(f));
        // A system file is not.
        Assert.False(WinUninstaller.IsUserWritable(Path.Combine(Sys32, "msiexec.exe")));
    }

    // ---- Registry roots and pinned paths ----

    [Fact]
    public void Roots_AreMachineWideOnly()
    {
        var roots = WinUninstaller.Roots();
        Assert.Equal(2, roots.Count);
        Assert.All(roots, r => Assert.Equal(RegistryHive.LocalMachine, r.Hive));
        Assert.All(roots, r => Assert.StartsWith(@"HKLM\", r.Label));
        Assert.Contains(roots, r => r.View == RegistryView.Registry64);
        Assert.Contains(roots, r => r.View == RegistryView.Registry32);
    }

    [Fact]
    public void Find_NeverReturnsPerUserEntries()
    {
        // Read-only: whatever is installed per user on this PC, Find only reports machine-wide keys.
        Assert.All(new WinUninstaller().Find("*", "*"), e => Assert.StartsWith(@"HKLM\", e.KeyPath));
        using var hkcu = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        foreach (var sub in hkcu?.GetSubKeyNames() ?? [])
        {
            using var k = hkcu!.OpenSubKey(sub);
            if (k?.GetValue("DisplayName") is string name && k.GetValue("Publisher") is string pub && !string.IsNullOrWhiteSpace(name))
                Assert.DoesNotContain(new WinUninstaller().Find(name.Replace("*", ""), pub), e => e.KeyPath.StartsWith(@"HKCU\", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void RequireAt_RefusesAHandleReachedThroughAJunction()
    {
        var real = Path.Combine(_dir, "real");
        Directory.CreateDirectory(real);
        var exe = Path.Combine(real, "un.exe");
        File.WriteAllText(exe, "x");
        var link = Path.Combine(_dir, "link");
        Assert.Equal(0, ProcessRunner.Run("cmd.exe", ["/c", "mklink", "/J", link, real], TimeSpan.FromSeconds(20)).ExitCode);
        try
        {
            using (var direct = WinUninstaller.OpenLocked(exe)) WinUninstaller.RequireAt(direct, exe);
            var viaLink = Path.Combine(link, "un.exe");
            using var h = WinUninstaller.OpenLocked(viaLink);
            Assert.Throws<UninstallRefusedException>(() => WinUninstaller.RequireAt(h, viaLink));
        }
        finally { Directory.Delete(link); }
    }

    // ---- Pure parsing ----

    [Fact]
    public void Compose_PrefersQuietAndAppendsArgs()
    {
        Assert.Equal("q.exe /S --force", WinUninstaller.Compose(Entry("q.exe /S", "u.exe"), "--force"));
        Assert.Equal("u.exe", WinUninstaller.Compose(Entry("  ", "u.exe"), null));
        Assert.Equal("u.exe", WinUninstaller.Compose(Entry(null, "u.exe "), "  "));
        Assert.Throws<ArgumentException>(() => WinUninstaller.Compose(Entry(null, " "), null));
    }

    [Fact]
    public void Parse_QuotedPath()
    {
        var c = WinUninstaller.Parse("\"C:\\Program Files\\App\\unins000.exe\" /SILENT /NORESTART");
        Assert.Equal(@"C:\Program Files\App\unins000.exe", c.Executable);
        Assert.Equal(new[] { "/SILENT", "/NORESTART" }, c.Arguments);
        Assert.False(c.IsMsiExec);
        Assert.Null(c.ProductCode);
    }

    [Fact]
    public void Parse_UnquotedPathWithSpaces()
    {
        var c = WinUninstaller.Parse(@"C:\Program Files (x86)\Steam\uninstall.exe");
        Assert.Equal(@"C:\Program Files (x86)\Steam\uninstall.exe", c.Executable);
        Assert.Empty(c.Arguments);

        var d = WinUninstaller.Parse(@"C:\Program Files (x86)\Microsoft\Edge\Application\130.0\Installer\setup.exe --uninstall --system-level --force-uninstall");
        Assert.Equal(@"C:\Program Files (x86)\Microsoft\Edge\Application\130.0\Installer\setup.exe", d.Executable);
        Assert.Equal(new[] { "--uninstall", "--system-level", "--force-uninstall" }, d.Arguments);
    }

    [Fact]
    public void Parse_ExpandsEnvironmentVariablesInProgram()
    {
        var c = WinUninstaller.Parse("\"%SystemRoot%\\System32\\foo.exe\" /x");
        Assert.Equal(Path.Combine(Sys32, "foo.exe"), c.Executable, ignoreCase: true);
    }

    [Theory]
    [InlineData("MsiExec.exe /X" + Guid1, new[] { "/X" + Guid1 })]
    [InlineData("msiexec /x" + Guid1 + " /qn", new[] { "/X" + Guid1, "/qn" })]
    [InlineData("MsiExec.exe /X " + Guid1 + " /quiet /norestart", new[] { "/X" + Guid1, "/quiet", "/norestart" })]
    [InlineData("C:\\Windows\\System32\\MsiExec.exe /X" + Guid1 + " /passive", new[] { "/X" + Guid1, "/passive" })]
    [InlineData("MsiExec.exe /I" + Guid1, new[] { "/X" + Guid1 })]
    [InlineData("MsiExec.exe /I " + Guid1 + " /qb!", new[] { "/X" + Guid1, "/qb!" })]
    [InlineData("MsiExec.exe /X" + Guid1 + " /qb /qb- REBOOT=ReallySuppress", new[] { "/X" + Guid1, "/qb", "/qb-", "REBOOT=ReallySuppress" })]
    [InlineData("MsiExec.exe /x{12345678-1234-1234-1234-1234567890ab}", new[] { "/X" + Guid1 })]
    public void Parse_MsiExecAllowList(string command, string[] args)
    {
        var c = WinUninstaller.Parse(command);
        Assert.True(c.IsMsiExec);
        Assert.Equal(Path.Combine(Sys32, "msiexec.exe"), c.Executable, ignoreCase: true);
        Assert.Equal(args, c.Arguments);
        Assert.Equal(Guid1, c.ProductCode);
    }

    [Theory]
    [InlineData("MsiExec.exe /X" + Guid1 + " /i" + Guid1)]
    [InlineData("MsiExec.exe /X" + Guid1 + " /X" + Guid1)]
    [InlineData("MsiExec.exe /X")]
    [InlineData("MsiExec.exe /I")]
    [InlineData("MsiExec.exe /Xnot-a-guid")]
    [InlineData("MsiExec.exe /qn")]
    [InlineData("MsiExec.exe /fa" + Guid1)]
    [InlineData("MsiExec.exe /X" + Guid1 + " /y C:\\evil.dll")]
    [InlineData("MsiExec.exe /X" + Guid1 + " /z C:\\evil.dll")]
    [InlineData("MsiExec.exe /X" + Guid1 + " /package C:\\evil.msi")]
    [InlineData("MsiExec.exe /X" + Guid1 + " /l*v C:\\log.txt")]
    [InlineData("MsiExec.exe /X" + Guid1 + " TRANSFORMS=evil.mst")]
    [InlineData("MsiExec.exe /X" + Guid1 + " /p patch.msp")]
    [InlineData("MsiExec.exe /X" + Guid1 + " REBOOT=Force")]
    public void Parse_MsiExecEverythingElseThrows(string command)
    {
        Assert.Throws<NotSupportedException>(() => WinUninstaller.Parse(command));
    }

    [Fact]
    public void Parse_MsiExecElsewhereIsAnOrdinaryProgram()
    {
        var c = WinUninstaller.Parse(@"C:\Tools\msiexec.exe /X" + Guid1);
        Assert.False(c.IsMsiExec);
        Assert.Equal(@"C:\Tools\msiexec.exe", c.Executable);
    }

    [Theory]
    [InlineData("uninstall.exe /S")]
    [InlineData("rundll32 shell32.dll,Foo")]
    [InlineData("\"C:\\App\\remove.cmd\" /S")]
    [InlineData("\"C:\\App\\un.exe\"/S")]
    [InlineData("\"\\\\server\\share\\un.exe\" /S")]
    [InlineData("\"\\\\?\\C:\\App\\un.exe\" /S")]
    [InlineData("\"\\??\\C:\\App\\un.exe\" /S")]
    [InlineData("\\??\\C:\\App\\un.exe /S")]
    public void Parse_UnsupportedThrows(string command)
    {
        Assert.Throws<NotSupportedException>(() => WinUninstaller.Parse(command));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"C:\\App\\un.exe")]
    public void Parse_BadThrows(string command)
    {
        Assert.Throws<ArgumentException>(() => WinUninstaller.Parse(command));
    }

    [Fact]
    public void SplitArguments_WindowsRules()
    {
        Assert.Equal(new[] { "a", "b c", "d" }, WinUninstaller.SplitArguments(" a \"b c\"  d "));
        Assert.Equal(new[] { "PROP=a b" }, WinUninstaller.SplitArguments("PROP=\"a b\""));
        Assert.Equal(new[] { "a\"b" }, WinUninstaller.SplitArguments("a\\\"b"));
        Assert.Equal(new[] { "c:\\dir\\" }, WinUninstaller.SplitArguments("\"c:\\dir\\\\\""));
        Assert.Equal(new[] { "x\\y" }, WinUninstaller.SplitArguments("x\\y"));
        Assert.Equal(new[] { "say \"hi\"" }, WinUninstaller.SplitArguments("\"say \"\"hi\"\"\""));
        Assert.Equal(new[] { "" }, WinUninstaller.SplitArguments("\"\""));
        Assert.Empty(WinUninstaller.SplitArguments("   "));
        Assert.Equal(new[] { "a&b", "|", ">x" }, WinUninstaller.SplitArguments("a&b | >x"));
        Assert.Throws<ArgumentException>(() => WinUninstaller.SplitArguments("\"open"));
    }

    [Theory]
    [InlineData("Valve Corp.", "Valve Corporation", true)]
    [InlineData("ASUSTeK COMPUTER INC.", "ASUSTek COMPUTER INC.", true)]
    [InlineData("ASUSTeK COMPUTER INC.", "ASUS", true)]
    [InlineData("ASUSTeK Computer Incorporated", "ASUSTeK Computer Inc.", true)]
    [InlineData("Microsoft Corporation", "microsoft corporation", true)]
    [InlineData("Contoso Ltd", "Contoso Limited", true)]
    [InlineData("Contoso Co.", "Contoso Company", true)]
    [InlineData("Microsoft Windows", "Microsoft Corporation", false)]
    [InlineData("Valve GmbH", "Valve Corporation", false)]
    [InlineData("Valve Ltd", "Valve Corporation", false)]
    [InlineData("Valve", "Valve Corporation", false)]
    [InlineData("Valve Corp.", "Valve Software", false)]
    [InlineData("ASUS Fake Ltd", "ASUS", false)]
    [InlineData("Evil Valve Corp.", "Valve Corporation", false)]
    [InlineData("", "", false)]
    public void NamesMatch_Rules(string signer, string publisher, bool expected)
    {
        Assert.Equal(expected, WinUninstaller.NamesMatch(signer, publisher));
    }

    // ---- VM lane ----

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinUninstaller_RunsSignedUninstaller()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: runs a real uninstaller (set NITROS_VM=1 inside the VM)");
        var u = new WinUninstaller();
        var steam = u.Find("Steam", "Valve Corporation").FirstOrDefault();
        if (steam is null) Assert.Skip("Steam not installed in this VM");
        bool ok;
        try { ok = u.SignerMatches(steam, "Valve Corporation"); }
        catch (UninstallRefusedException) { Assert.Skip("Steam is in a user-writable folder in this VM, so it is refused by design"); return; }
        Assert.True(ok);
        Assert.Equal(0, u.Run(steam, "/S"));
        Assert.Empty(u.Find("Steam", "Valve Corporation"));
    }
}
