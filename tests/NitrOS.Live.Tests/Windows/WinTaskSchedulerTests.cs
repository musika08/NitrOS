using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>Creates and deletes only the task \NitrOS.Tests\Dummy, and the \NitrOS.Tests folder once it is empty.</summary>
public sealed class WinTaskSchedulerTests : IDisposable
{
    private const string Task = @"\NitrOS.Tests\Dummy";
    private const string Folder = "NitrOS.Tests";

    public void Dispose()
    {
        ProcessRunner.Run("schtasks.exe", $"/Delete /TN \"{Task}\" /F", TimeSpan.FromSeconds(30));
        DeleteSandboxFolderIfEmpty();
    }

    /// <summary>Deletes \NitrOS.Tests when it holds no task (hidden ones included) and no subfolder. No other folder is touched.</summary>
    private static void DeleteSandboxFolderIfEmpty()
    {
        try
        {
            if (SandboxFolder() is not { } folder) return;
            const int TaskEnumHidden = 1;
            var tasks = Get(Call(folder, "GetTasks", TaskEnumHidden)!, "Count");
            var subfolders = Get(Call(folder, "GetFolders", 0)!, "Count");
            if (Convert.ToInt32(tasks) != 0 || Convert.ToInt32(subfolders) != 0) return;
            var root = Call(Scheduler(), "GetFolder", @"\")!;
            Call(root, "DeleteFolder", Folder, 0);
        }
        catch (Exception e) when (e is System.Reflection.TargetInvocationException or System.Runtime.InteropServices.COMException)
        {
            // best effort: a folder that cannot be deleted is left as it is
        }
    }

    /// <summary>The Task Scheduler COM object (Schedule.Service), connected to this PC.</summary>
    private static object Scheduler()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!;
        var service = Activator.CreateInstance(type)!;
        Call(service, "Connect");
        return service;
    }

    private static object? Call(object target, string method, params object[] args) =>
        target.GetType().InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, target, args);

    private static object? Get(object target, string property) =>
        target.GetType().InvokeMember(property, System.Reflection.BindingFlags.GetProperty, null, target, null);

    /// <summary>The \NitrOS.Tests folder, or null when it does not exist.</summary>
    private static object? SandboxFolder()
    {
        try { return Call(Scheduler(), "GetFolder", @"\" + Folder); }
        catch (Exception e) when (e is System.Reflection.TargetInvocationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    [Fact]
    public void Dispose_RemovesTheEmptySandboxFolder()
    {
        new WinTaskSchedulerTests().Dispose();
        Assert.Null(SandboxFolder());
    }

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
