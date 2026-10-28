namespace NitrOS.Image.Tests.FirstBoot;

using NitrOS.Core.Model;
using NitrOS.Image.FirstBoot;
using NitrOS.Live.Systems;

public sealed class CmdTextTests
{
    [Fact]
    public void RegAdd_EveryKindRendersExpectedLine()
    {
        // Dword
        var dw = CmdText.RegAdd("HKLM\\SOFTWARE\\Test", "ValDw", new RegValue(RegistryKind.Dword, "1"));
        Assert.Equal("reg add \"HKLM\\SOFTWARE\\Test\" /v \"ValDw\" /t REG_DWORD /d 1 /f", dw);

        // Qword
        var qw = CmdText.RegAdd("HKLM\\SOFTWARE\\Test", "ValQw", new RegValue(RegistryKind.Qword, "1000"));
        Assert.Equal("reg add \"HKLM\\SOFTWARE\\Test\" /v \"ValQw\" /t REG_QWORD /d 1000 /f", qw);

        // String
        var sz = CmdText.RegAdd("HKLM\\SOFTWARE\\Test", "ValSz", new RegValue(RegistryKind.String, "hello"));
        Assert.Equal("reg add \"HKLM\\SOFTWARE\\Test\" /v \"ValSz\" /t REG_SZ /d \"hello\" /f", sz);

        // ExpandString
        var esz = CmdText.RegAdd("HKLM\\SOFTWARE\\Test", "ValEsz", new RegValue(RegistryKind.ExpandString, "path"));
        Assert.Equal("reg add \"HKLM\\SOFTWARE\\Test\" /v \"ValEsz\" /t REG_EXPAND_SZ /d \"path\" /f", esz);

        // MultiString
        var msz = CmdText.RegAdd("HKLM\\SOFTWARE\\Test", "ValMsz", new RegValue(RegistryKind.MultiString, "a\\0b"));
        Assert.Equal("reg add \"HKLM\\SOFTWARE\\Test\" /v \"ValMsz\" /t REG_MULTI_SZ /d \"a\\0b\" /f", msz);

        // Binary
        var bin = CmdText.RegAdd("HKLM\\SOFTWARE\\Test", "ValBin", new RegValue(RegistryKind.Binary, "03000000"));
        Assert.Equal("reg add \"HKLM\\SOFTWARE\\Test\" /v \"ValBin\" /t REG_BINARY /d 03000000 /f", bin);

        // Empty name -> /ve
        var ve = CmdText.RegAdd("HKLM\\SOFTWARE\\Test", "", new RegValue(RegistryKind.Dword, "0"));
        Assert.Equal("reg add \"HKLM\\SOFTWARE\\Test\" /ve /t REG_DWORD /d 0 /f", ve);
    }
}
