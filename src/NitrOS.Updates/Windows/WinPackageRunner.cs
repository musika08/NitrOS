using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using NitrOS.Live.Windows;

namespace NitrOS.Updates.Windows;

public sealed class WinPackageRunner : NitrOS.Updates.Installing.IPackageRunner
{
    public int Run(string path, SafeFileHandle lockedFile, string? arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            var args = WinUninstaller.SplitArguments(arguments);
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start process {path}");

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Process {path} timed out after {timeout}.");
        }

        return process.ExitCode;
    }

    public void StartInteractive(string path, SafeFileHandle lockedFile)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        Process.Start(psi);
    }
}
