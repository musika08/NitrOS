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
}
