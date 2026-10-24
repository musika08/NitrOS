using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace NitrOS.Live.Windows;

/// <summary>Runs a program, captures its output and kills the whole process tree at the timeout.</summary>
public static class ProcessRunner
{
    public const int TimeoutExitCode = -1;
    private static readonly TimeSpan MinDrain = TimeSpan.FromSeconds(2);

    /// <summary>Runs with an argument list: each argument is passed verbatim, so nothing can be injected.</summary>
    public static (int ExitCode, string StdOut, string StdErr) Run(string file, IReadOnlyList<string> args, TimeSpan timeout)
    {
        var psi = NewStartInfo(file);
        foreach (var a in args) psi.ArgumentList.Add(a);
        return Run(psi, timeout);
    }

    /// <summary>Raw command-line overload, for callers that fully control the text.</summary>
    public static (int ExitCode, string StdOut, string StdErr) Run(string file, string args, TimeSpan timeout)
    {
        var psi = NewStartInfo(file);
        psi.Arguments = args;
        return Run(psi, timeout);
    }

    private static ProcessStartInfo NewStartInfo(string file) => new(file)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    private static (int ExitCode, string StdOut, string StdErr) Run(ProcessStartInfo psi, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        using var p = new Process { StartInfo = psi };
        p.Start();
        p.StandardInput.Close();
        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        var outTask = Task.Run(() => Pump(p.StandardOutput, outBuf));
        var errTask = Task.Run(() => Pump(p.StandardError, errBuf));

        var timedOut = !p.WaitForExit(timeout);
        if (timedOut)
        {
            try { p.Kill(entireProcessTree: true); }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception or AggregateException or NotSupportedException) { }
            p.WaitForExit(2000);
        }

        // A detached grandchild can keep the pipes open: wait only for the rest of the timeout (at least 2 s), then take what we have.
        var drain = timeout - sw.Elapsed;
        if (drain < MinDrain || timedOut) drain = MinDrain;
        Task.WaitAll(new[] { outTask, errTask }, drain);

        var stdout = Snapshot(outBuf);
        var stderr = Snapshot(errBuf);
        if (timedOut) return (TimeoutExitCode, stdout, stderr + "timed out");
        return (p.ExitCode, stdout, stderr);
    }

    private static void Pump(StreamReader reader, StringBuilder buf)
    {
        var chunk = new char[4096];
        try
        {
            int n;
            while ((n = reader.Read(chunk, 0, chunk.Length)) > 0)
                lock (buf) buf.Append(chunk, 0, n);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    private static string Snapshot(StringBuilder buf)
    {
        lock (buf) return buf.ToString();
    }
}
