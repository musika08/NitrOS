using System.Diagnostics;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

public class ProcessRunnerTests
{
    [Fact]
    public void Run_CapturesStdoutStderrAndExitCode()
    {
        var r = ProcessRunner.Run("cmd.exe", "/c \"echo out& echo err 1>&2& exit 3\"", TimeSpan.FromSeconds(20));
        Assert.Equal(3, r.ExitCode);
        Assert.Contains("out", r.StdOut);
        Assert.Contains("err", r.StdErr);
    }

    [Fact]
    public void Run_KillsAtTimeoutAndDoesNotHang()
    {
        var sw = Stopwatch.StartNew();
        var r = ProcessRunner.Run("cmd.exe", "/c ping -n 30 127.0.0.1", TimeSpan.FromSeconds(1));
        Assert.Equal(ProcessRunner.TimeoutExitCode, r.ExitCode);
        Assert.InRange(sw.Elapsed.TotalSeconds, 0.5, 12);
    }

    [Fact]
    public void Run_ArgumentListPassesMetacharactersVerbatim()
    {
        // With an argument list the quote and ampersand reach cmd as one literal argument, not as extra commands.
        var r = ProcessRunner.Run("cmd.exe", new[] { "/c", "echo", "a\" & echo INJECTED" }, TimeSpan.FromSeconds(20));
        Assert.Equal(1, r.StdOut.Split('\n').Count(l => l.Contains("INJECTED")));
    }

    [Fact]
    public void Run_DoesNotHangWhenDetachedGrandchildHoldsThePipe()
    {
        var sw = Stopwatch.StartNew();
        // cmd exits at once; the detached ping inherits stdout and keeps the pipe open for ~8 s.
        var r = ProcessRunner.Run("cmd.exe", "/c start /b ping -n 8 127.0.0.1", TimeSpan.FromSeconds(3));
        Assert.True(sw.Elapsed.TotalSeconds < 6.5, "took " + sw.Elapsed);
        Assert.NotEqual(ProcessRunner.TimeoutExitCode, r.ExitCode);
    }
}
