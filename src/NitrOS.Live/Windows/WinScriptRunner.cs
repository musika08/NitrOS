using System.Text;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>
/// Runs the body with Windows PowerShell as
/// <c>powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand &lt;base64 UTF-16LE&gt;</c>.
/// No temporary file is written, so nothing can be swapped on disk. Timeout: 10 minutes.
/// </summary>
public sealed class WinScriptRunner : IScriptRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Windows limits a command line to 32,767 characters; leave room for the program path and the other switches.</summary>
    public const int MaxEncodedLength = 32_000;

    internal TimeSpan Timeout { get; init; } = DefaultTimeout;

    public int Run(string body)
    {
        var encoded = Encode(body);
        var ps = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        var (code, _, _) = ProcessRunner.Run(ps, ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded], Timeout);
        return code;
    }

    /// <summary>Base64 of the UTF-16LE body, as -EncodedCommand expects. Throws when it would not fit on a command line.</summary>
    public static string Encode(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(body));
        if (encoded.Length > MaxEncodedLength)
            throw new ArgumentException($"Script is too long to pass as an encoded command ({body.Length} characters)", nameof(body));
        return encoded;
    }
}
