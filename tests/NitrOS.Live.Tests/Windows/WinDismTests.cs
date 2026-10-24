using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

public class WinDismTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    private const string FeatureInfo = """

        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Image Version: 10.0.26100.1

        Feature Information:

        Feature Name : Printing-PrintToPDFServices-Features
        Display Name : Microsoft Print to PDF
        Description : Provides binaries on the system for creating .pdf files
        Restart Required : Possible
        State : {0}

        Custom Properties:

        (No custom properties found)

        The operation completed successfully.
        """;

    [Fact]
    public void WinDism_ReportsFeatureState()
    {
        // Read-only query. On this PC DISM refuses online servicing (not elevated, or access denied): that must surface as the access-denied detail.
        string? state;
        try { state = new WinDism().FeatureState("Printing-PrintToPDFServices-Features"); }
        catch (DismAccessDeniedException e)
        {
            Assert.Equal("DISM access denied (see notes-for-later-plans)", e.Message);
            return;
        }
        Assert.Contains(state, new[] { "Enabled", "Disabled", "EnablePending", "DisablePending", "DisabledWithPayloadRemoved" });
    }

    [Theory]
    [InlineData("Enabled", "Enabled")]
    [InlineData("Disabled", "Disabled")]
    [InlineData("Enable Pending", "EnablePending")]
    [InlineData("Disable Pending", "DisablePending")]
    [InlineData("Disabled with Payload Removed", "DisabledWithPayloadRemoved")]
    [InlineData("Not Present", "NotPresent")]
    [InlineData("Installed", "Installed")]
    [InlineData("Install Pending", "InstallPending")]
    [InlineData("Uninstall Pending", "UninstallPending")]
    [InlineData("  staged ", "Staged")]
    public void NormaliseState_PlainWords(string raw, string expected)
    {
        Assert.Equal(expected, WinDism.NormaliseState(raw));
    }

    [Fact]
    public void NormaliseState_UnknownThrows()
    {
        Assert.Throws<InvalidOperationException>(() => WinDism.NormaliseState("Half Enabled"));
    }

    [Fact]
    public void ParseQuery_ReadsStateLine()
    {
        Assert.Equal("Enabled", WinDism.ParseQuery(0, string.Format(FeatureInfo, "Enabled")));
        Assert.Equal("DisablePending", WinDism.ParseQuery(0, string.Format(FeatureInfo, "Disable Pending")));
        // "Restart Required" must not be mistaken for the State line.
        Assert.Equal("Disabled", WinDism.ParseQuery(0, string.Format(FeatureInfo, "Disabled")));
    }

    [Fact]
    public void ParseQuery_NotFoundIsNull()
    {
        const string unknown = "Error: 0x800f080c\r\n\r\nFeature name Foo is unknown.\r\nA Windows feature name was not recognized.";
        Assert.Null(WinDism.ParseQuery(unchecked((int)0x800F080C), unknown));
        Assert.Null(WinDism.ParseQuery(87, "Error: 87\r\nThe capability was not found."));
    }

    [Theory]
    [InlineData(740, "Error: 740\r\n\r\nElevated permissions are required to run DISM.")]
    [InlineData(5, "Error: 5\r\n\r\nAccess is denied.")]
    [InlineData(unchecked((int)0x80070005), "Error: 0x80070005")]
    [InlineData(1, "Error: 5\r\nAccess is denied.")]
    public void AccessDenied_ThrowsWithDetail(int code, string output)
    {
        var e = Assert.Throws<DismAccessDeniedException>(() => WinDism.ParseQuery(code, output));
        Assert.Equal(WinDism.AccessDeniedDetail, e.Message);
        Assert.Throws<DismAccessDeniedException>(() => WinDism.ClassifyChange(code, output));
    }

    [Fact]
    public void ParseQuery_OtherFailuresThrow()
    {
        Assert.Throws<InvalidOperationException>(() => WinDism.ParseQuery(87, "Error: 87\r\nThe parameter is incorrect."));
        Assert.Throws<InvalidOperationException>(() => WinDism.ParseQuery(0, "no state here"));
    }

    [Fact]
    public void ParseQuery_AccessDeniedTextInDescriptionIsIgnoredOnSuccess()
    {
        var text = string.Format(FeatureInfo, "Enabled").Replace("Provides binaries", "Access is denied to nobody; provides binaries");
        Assert.Equal("Enabled", WinDism.ParseQuery(0, text));
    }

    [Fact]
    public void ClassifyChange_Outcomes()
    {
        Assert.Equal(DismOutcome.Done, WinDism.ClassifyChange(0, "The operation completed successfully."));
        Assert.Equal(DismOutcome.RebootRequired, WinDism.ClassifyChange(3010, "The restart is required."));
        Assert.Equal(DismOutcome.Deferred, WinDism.ClassifyChange(0, "Feature state: Disable Pending"));
        Assert.Equal(DismOutcome.Deferred, WinDism.ClassifyChange(0, "Uninstall Pending"));
        Assert.Equal(DismOutcome.NotPresent, WinDism.ClassifyChange(unchecked((int)0x800F080C), "Feature name X is unknown."));
        Assert.Equal(DismOutcome.Failed, WinDism.ClassifyChange(87, "Error: 87 The parameter is incorrect."));
        Assert.Equal(DismOutcome.Failed, WinDism.ClassifyChange(ProcessRunner.TimeoutExitCode, "timed out"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Foo /Online")]
    [InlineData("Foo\"Bar")]
    [InlineData("/Remove")]
    public void BadNames_ThrowBeforeDismRuns(string bad)
    {
        var d = new WinDism();
        Assert.Throws<ArgumentException>(() => d.FeatureState(bad));
        Assert.Throws<ArgumentException>(() => d.DisableFeature(bad));
        Assert.Throws<ArgumentException>(() => d.EnableFeature(bad));
        Assert.Throws<ArgumentException>(() => d.CapabilityState(bad));
        Assert.Throws<ArgumentException>(() => d.RemoveCapability(bad));
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinDism_DisableEnableFeature_RoundTrip()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: changes a real Windows feature (set NITROS_VM=1 inside the VM)");
        var d = new WinDism();
        const string f = "Printing-PrintToPDFServices-Features";
        Assert.Equal("Enabled", d.FeatureState(f));
        try
        {
            Assert.Contains(d.DisableFeature(f), new[] { DismOutcome.Done, DismOutcome.RebootRequired, DismOutcome.Deferred });
            Assert.Contains(d.FeatureState(f), new[] { "Disabled", "DisablePending" });
        }
        finally { d.EnableFeature(f); }
        Assert.Contains(d.FeatureState(f), new[] { "Enabled", "EnablePending" });
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinDism_RemoveCapability()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: removes a real Windows capability (set NITROS_VM=1 inside the VM)");
        var d = new WinDism();
        const string c = "App.StepsRecorder~~~~0.0.1.0";
        if (d.CapabilityState(c) is not "Installed") Assert.Skip("Steps Recorder capability not installed in this VM");
        Assert.Contains(d.RemoveCapability(c), new[] { DismOutcome.Done, DismOutcome.RebootRequired, DismOutcome.Deferred });
    }
}
