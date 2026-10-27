using System.Diagnostics;

namespace NitrOS.Live.Windows;

public sealed class WinXboxRestore : IXboxRestoreSteps
{
    public bool RestoreStore(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Running wsreset.exe -i to register Microsoft Store...");
        var exitCode = RunProcess("wsreset.exe", "-i", TimeSpan.FromSeconds(30), ct);
        if (exitCode == 0) return true;

        progress?.Report("wsreset exited with code " + exitCode + ", trying winget for Microsoft Store...");
        return RunProcess("winget.exe", "install --id 9MWPM2CQNLHN --source msstore --accept-source-agreements --accept-package-agreements --silent", TimeSpan.FromMinutes(3), ct) == 0;
    }

    public bool RestoreXboxSignIn(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Installing Xbox Identity Provider via winget...");
        return RunProcess("winget.exe", "install --id 9WZDNCRD1HKW --source msstore --accept-source-agreements --accept-package-agreements --silent", TimeSpan.FromMinutes(3), ct) == 0;
    }

    public bool RestoreGameBar(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Installing Xbox Game Bar via winget...");
        return RunProcess("winget.exe", "install --id 9NZKPSTSNW4P --source msstore --accept-source-agreements --accept-package-agreements --silent", TimeSpan.FromMinutes(3), ct) == 0;
    }

    public bool RestoreGamingServices(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Installing Gaming Services via winget...");
        var code = RunProcess("winget.exe", "install Microsoft.GamingServices --source msstore --accept-source-agreements --accept-package-agreements --silent", TimeSpan.FromMinutes(3), ct);
        if (code == 0) return true;

        progress?.Report("winget failed, attempting Gaming Services package registration...");
        return RunProcess("powershell.exe", "-NoProfile -Command \"Get-AppxPackage -AllUsers *GamingServices* | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register \\\"$($_.InstallLocation)\\AppXManifest.xml\\\"}\"", TimeSpan.FromSeconds(30), ct) == 0;
    }

    public bool RestoreXboxApp(IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report("Installing Xbox App via winget...");
        return RunProcess("winget.exe", "install --id 9MV0B5HZVK9Z --source msstore --accept-source-agreements --accept-package-agreements --silent", TimeSpan.FromMinutes(5), ct) == 0;
    }

    private static int RunProcess(string fileName, string arguments, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return -1;

            if (process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                return process.ExitCode;
            }

            try { process.Kill(true); } catch { /* ignore */ }
            return -2;
        }
        catch
        {
            return -1;
        }
    }
}
