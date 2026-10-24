using System.Text;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>
/// Writes the body to a .ps1 in a fresh folder and runs it with Windows PowerShell
/// (-NoProfile -NonInteractive -ExecutionPolicy Bypass -File). The folder is deleted afterwards. Timeout: 10 minutes.
/// </summary>
public sealed class WinScriptRunner(string? workFolder = null) : IScriptRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    private readonly string _workFolder = workFolder ?? Path.GetTempPath();

    internal TimeSpan Timeout { get; init; } = DefaultTimeout;

    public int Run(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var dir = Path.Combine(_workFolder, "NitrOS-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "script.ps1");
            // A byte-order mark makes Windows PowerShell 5.1 read the file as UTF-8.
            File.WriteAllText(file, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var ps = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            var (code, _, _) = ProcessRunner.Run(ps, ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", file], Timeout);
            return code;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
