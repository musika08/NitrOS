using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

public class WinUninstallerTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";
    private static string Sys32 => Environment.SystemDirectory;
    private const string Guid1 = "{12345678-1234-1234-1234-1234567890AB}";

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
        var u = new WinUninstaller();
        Assert.True(u.SignerMatches(steam, "Valve Corporation"));
        Assert.False(u.SignerMatches(steam, "Microsoft Corporation"));
    }

    [Fact]
    public void WinUninstaller_MsiExecIsMicrosoftSignedViaCatalog()
    {
        // Reads msiexec.exe's catalog signature only; nothing runs.
        var names = WinUninstaller.TrustedSignerNames(Path.Combine(Sys32, "msiexec.exe"));
        Assert.NotNull(names);
        Assert.Contains(names!, n => WinUninstaller.NamesMatch(n, "Microsoft Corporation"));

        var u = new WinUninstaller();
        var msi = Entry(null, "MsiExec.exe /X" + Guid1, "Contoso Ltd.");
        Assert.True(u.SignerMatches(msi, "Contoso Ltd."));
        Assert.False(u.SignerMatches(msi, "Fabrikam Inc."));
    }

    [Fact]
    public void WinUninstaller_UnsignedOrMissingProgramDoesNotMatch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fake = Path.Combine(dir, "uninstall.exe");
            File.WriteAllBytes(fake, [0x4D, 0x5A, 0, 0]);
            var u = new WinUninstaller();
            Assert.False(u.SignerMatches(Entry(null, "\"" + fake + "\" /S"), "Contoso Ltd."));
            Assert.False(u.SignerMatches(Entry(null, Path.Combine(dir, "missing.exe")), "Contoso Ltd."));
            Assert.False(u.SignerMatches(Entry(null, "uninstall.exe /S"), "Contoso Ltd."));
            Assert.False(u.SignerMatches(Entry(null, null), "Contoso Ltd."));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void WinUninstaller_NoMatchForUnknownProgram()
    {
        Assert.Empty(new WinUninstaller().Find("NitrOS No Such Program 7f3a*", "Nobody"));
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
    [InlineData("msiexec /x" + Guid1 + " /qn", new[] { "/x" + Guid1, "/qn" })]
    [InlineData("MsiExec.exe /X " + Guid1 + " /quiet /norestart", new[] { "/X", Guid1, "/quiet", "/norestart" })]
    [InlineData("C:\\Windows\\System32\\MsiExec.exe /X" + Guid1 + " /passive", new[] { "/X" + Guid1, "/passive" })]
    public void Parse_MsiExecUninstall(string command, string[] args)
    {
        var c = WinUninstaller.Parse(command);
        Assert.True(c.IsMsiExec);
        Assert.Equal(Path.Combine(Sys32, "msiexec.exe"), c.Executable, ignoreCase: true);
        Assert.Equal(args, c.Arguments);
    }

    [Theory]
    [InlineData("MsiExec.exe /I" + Guid1)]
    [InlineData("MsiExec.exe /X" + Guid1 + " /i" + Guid1)]
    [InlineData("MsiExec.exe /X")]
    [InlineData("MsiExec.exe /Xnot-a-guid")]
    [InlineData("MsiExec.exe /qn")]
    [InlineData("MsiExec.exe /fa" + Guid1)]
    [InlineData("MsiExec.exe /X" + Guid1 + " /X" + Guid1)]
    public void Parse_MsiExecOtherOperationsThrow(string command)
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
    [InlineData("ASUSTeK Computer Inc.", "ASUSTeK Computer Inc.", true)]
    [InlineData("Microsoft Corporation", "microsoft corporation", true)]
    [InlineData("Microsoft Windows", "Microsoft Corporation", false)]
    [InlineData("Valve Corp.", "Valve Software", false)]
    [InlineData("ASUS Fake Ltd", "ASUS", false)]
    [InlineData("Evil Valve Corp.", "Valve Corporation", false)]
    [InlineData("", "", false)]
    [InlineData("Corp.", "Corporation", false)]
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
        Assert.True(u.SignerMatches(steam, "Valve Corporation"));
        Assert.Equal(0, u.Run(steam, "/S"));
        Assert.Empty(u.Find("Steam", "Valve Corporation"));
    }
}
