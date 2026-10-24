using System.Diagnostics;

namespace NitrOS.Live.Windows;

/// <summary>Runs a program, captures its output and kills the whole process tree at the timeout.</summary>
public static class ProcessRunner
{
    public const int TimeoutExitCode = -1;

    public static (int ExitCode, string StdOut, string StdErr) Run(string file, string args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = new Process { StartInfo = psi };
        p.Start();
        p.StandardInput.Close();
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeout))
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            p.WaitForExit(2000);
            return (TimeoutExitCode, Drain(outTask), Drain(errTask) + "timed out");
        }
        p.WaitForExit(); // flush async readers
        return (p.ExitCode, outTask.GetAwaiter().GetResult(), errTask.GetAwaiter().GetResult());
    }

    // After a kill, a surviving grandchild could hold the pipe open: never wait on it for long.
    private static string Drain(Task<string> t) => t.Wait(2000) ? t.Result : "";
}
