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

    [Fact]
    public void ParseCsvLine_HandlesQuotesAndCommas()
    {
        var cols = WinTaskScheduler.ParseCsvLine("\"\\A\\B,C\",\"N/A\",\"Disabled\"");
        Assert.Equal(new[] { "\\A\\B,C", "N/A", "Disabled" }, cols);
    }
}
