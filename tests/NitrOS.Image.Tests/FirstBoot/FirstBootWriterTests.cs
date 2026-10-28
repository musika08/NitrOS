namespace NitrOS.Image.Tests.FirstBoot;

using System;
using System.IO;
using NitrOS.Core.Model;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Tests.Fakes;
using NitrOS.Live.Systems;

public sealed class FirstBootWriterTests
{
    [Fact]
    public void SetupComplete_CreatesBlockWhenMissing()
    {
        var files = new FakeImageFiles();
        var rendered = new RenderedScripts("echo machine", null, [], []);

        FirstBootWriter.WriteFiles(rendered, "M:\\mount", files);

        var setupComplete = files.ReadText("M:\\mount\\Windows\\Setup\\Scripts\\SetupComplete.cmd");
        Assert.NotNull(setupComplete);
        Assert.Contains(FirstBootWriter.BeginMarker, setupComplete);
        Assert.Contains("call \"%~dp0NitrOS\\machine.cmd\" SetupComplete", setupComplete);
        Assert.Contains(FirstBootWriter.EndMarker, setupComplete);
    }

    [Fact]
    public void SetupComplete_KeepsOemLinesAndReplacesOnlyOurBlock()
    {
        var files = new FakeImageFiles();
        var oemOriginal = "@echo off\r\necho OEM setup running\r\ncall C:\\oem\\setup.exe\r\n";
        files.WriteText("M:\\mount\\Windows\\Setup\\Scripts\\SetupComplete.cmd", oemOriginal);

        var rendered = new RenderedScripts("echo machine", null, [], []);
        FirstBootWriter.WriteFiles(rendered, "M:\\mount", files);

        var content = files.ReadText("M:\\mount\\Windows\\Setup\\Scripts\\SetupComplete.cmd");
        Assert.NotNull(content);
        Assert.Contains("OEM setup running", content);
        Assert.Contains(FirstBootWriter.BeginMarker, content);

        // Run second time with different machine command -> OEM lines still intact and single block
        var rendered2 = new RenderedScripts("echo machine v2", null, [], []);
        FirstBootWriter.WriteFiles(rendered2, "M:\\mount", files);

        var content2 = files.ReadText("M:\\mount\\Windows\\Setup\\Scripts\\SetupComplete.cmd");
        Assert.NotNull(content2);
        Assert.Contains("OEM setup running", content2);

        // Exactly one begin marker
        var firstIdx = content2.IndexOf(FirstBootWriter.BeginMarker, StringComparison.Ordinal);
        var lastIdx = content2.LastIndexOf(FirstBootWriter.BeginMarker, StringComparison.Ordinal);
        Assert.Equal(firstIdx, lastIdx);
    }

    [Fact]
    public void SetupComplete_RewriteIsByteIdentical()
    {
        var files = new FakeImageFiles();
        var rendered = new RenderedScripts("echo machine", null, [], []);

        FirstBootWriter.WriteFiles(rendered, "M:\\mount", files);
        var first = files.ReadText("M:\\mount\\Windows\\Setup\\Scripts\\SetupComplete.cmd");

        FirstBootWriter.WriteFiles(rendered, "M:\\mount", files);
        var second = files.ReadText("M:\\mount\\Windows\\Setup\\Scripts\\SetupComplete.cmd");

        Assert.Equal(first, second);
    }

    [Fact]
    public void RunOnce_CommandLinesUnder260AndExpandString()
    {
        var reg = new FakeOfflineRegistry();
        using var software = reg.Load("C:\\SOFTWARE", "NitrOS.Image.SOFTWARE");
        using var defaultUser = reg.Load("C:\\NTUSER.DAT", "NitrOS.Image.DEFAULTUSER");

        var rendered = new RenderedScripts("echo machine", "echo user", [], []);
        FirstBootWriter.WriteRunOnce(rendered, software, defaultUser);

        var machVal = software.GetValue("Microsoft\\Windows\\CurrentVersion\\RunOnce", FirstBootWriter.MachineRunOnceName);
        Assert.NotNull(machVal);
        Assert.Equal(RegistryKind.ExpandString, machVal.Kind);
        Assert.True(machVal.Data.Length <= 260, $"Machine command line length {machVal.Data.Length} > 260");

        var userVal = defaultUser.GetValue("Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce", FirstBootWriter.UserRunOnceName);
        Assert.NotNull(userVal);
        Assert.Equal(RegistryKind.ExpandString, userVal.Kind);
        Assert.True(userVal.Data.Length <= 260, $"User command line length {userVal.Data.Length} > 260");
    }

    [Fact]
    public void RunOnce_UserValueOnlyWhenUserItems()
    {
        var reg = new FakeOfflineRegistry();
        using var software = reg.Load("C:\\SOFTWARE", "NitrOS.Image.SOFTWARE");
        using var defaultUser = reg.Load("C:\\NTUSER.DAT", "NitrOS.Image.DEFAULTUSER");

        var rendered = new RenderedScripts("echo machine", null, [], []);
        FirstBootWriter.WriteRunOnce(rendered, software, defaultUser);

        Assert.NotNull(software.GetValue("Microsoft\\Windows\\CurrentVersion\\RunOnce", FirstBootWriter.MachineRunOnceName));
        Assert.Null(defaultUser.GetValue("Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce", FirstBootWriter.UserRunOnceName));
    }
}
