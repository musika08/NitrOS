using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>Creates and deletes only the task \NitrOS.Tests\Dummy.</summary>
public sealed class WinTaskSchedulerTests : IDisposable
{
    private const string Task = @"\NitrOS.Tests\Dummy";

    public void Dispose() => ProcessRunner.Run("schtasks.exe", $"/Delete /TN \"{Task}\" /F", TimeSpan.FromSeconds(30));

    [Fact]
    public void WinTaskScheduler_TogglesSandboxTask()
    {
        var sch = new WinTaskScheduler();
        var (code, _, err) = ProcessRunner.Run("schtasks.exe",
            $"/Create /TN \"{Task}\" /TR \"cmd /c exit\" /SC ONCE /ST 00:00 /F", TimeSpan.FromSeconds(30));
        Assert.True(code == 0, "could not create sandbox task: " + err);

        Assert.True(sch.Get(Task)!.Enabled);
        sch.SetEnabled(Task, false);
        Assert.False(sch.Get(Task)!.Enabled);
        sch.SetEnabled(Task, true);
        Assert.True(sch.Get(Task)!.Enabled);

        ProcessRunner.Run("schtasks.exe", $"/Delete /TN \"{Task}\" /F", TimeSpan.FromSeconds(30));
        Assert.Null(sch.Get(Task));
    }

    [Fact]
    public void WinTaskScheduler_MissingTaskIsNull()
    {
        Assert.Null(new WinTaskScheduler().Get(@"\NitrOS.Tests\DoesNotExist"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NoLeadingSlash")]
    [InlineData("\\A\\")]
    [InlineData("\\A\"B")]
    public void BadTaskPaths_ThrowBeforeAnyProcessRuns(string path)
    {
        var sch = new WinTaskScheduler();
        Assert.Throws<ArgumentException>(() => sch.Get(path));
        Assert.Throws<ArgumentException>(() => sch.SetEnabled(path, false));
    }

    [Fact]
    public void ParseEnabled_TrueFalseAndAbsent()
    {
        const string ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        string Xml(string settings) => $"<?xml version=\"1.0\" encoding=\"UTF-16\"?><Task xmlns=\"{ns}\"><Triggers><TimeTrigger><Enabled>false</Enabled></TimeTrigger></Triggers>{settings}</Task>";
        Assert.False(WinTaskScheduler.ParseEnabled(Xml("<Settings><Enabled>false</Enabled></Settings>")));
        Assert.True(WinTaskScheduler.ParseEnabled(Xml("<Settings><Enabled>true</Enabled></Settings>")));
        Assert.True(WinTaskScheduler.ParseEnabled(Xml("<Settings></Settings>")));
        Assert.True(WinTaskScheduler.ParseEnabled(Xml("")));
    }

    [Fact]
    public void ClassifyQueryExit_SeparatesOkMissingAndFailure()
    {
        Assert.Equal(WinTaskScheduler.ExitClass.Ok, WinTaskScheduler.ClassifyQueryExit(0));
        Assert.Equal(WinTaskScheduler.ExitClass.MaybeNotFound, WinTaskScheduler.ClassifyQueryExit(1));
        Assert.Equal(WinTaskScheduler.ExitClass.Failed, WinTaskScheduler.ClassifyQueryExit(ProcessRunner.TimeoutExitCode));
        Assert.Equal(WinTaskScheduler.ExitClass.Failed, WinTaskScheduler.ClassifyQueryExit(5));
    }

    [Fact]
    public void ParseCsvLine_HandlesQuotesAndCommas()
    {
        var cols = WinTaskScheduler.ParseCsvLine("\"\\A\\B,C\",\"N/A\",\"Disabled\"");
        Assert.Equal(new[] { "\\A\\B,C", "N/A", "Disabled" }, cols);
    }
}
