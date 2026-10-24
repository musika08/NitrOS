using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>The scripts here only write inside the test's own temp folder.</summary>
public sealed class WinScriptRunnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));

    public WinScriptRunnerTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Quote(string path) => "'" + path.Replace("'", "''") + "'";

    [Fact]
    public void Run_ExecutesBodyAndReturnsExitCode()
    {
        var output = Path.Combine(_dir, "out.txt");
        var body = $"Set-Content -LiteralPath {Quote(output)} -Value 'héllo' -Encoding UTF8\r\nexit 3";
        var code = new WinScriptRunner(_dir).Run(body);

        Assert.Equal(3, code);
        Assert.Equal("héllo", File.ReadAllText(output).Trim());
        // The temporary script folder is cleaned up; only the script's own output remains.
        Assert.Equal(new[] { output }, Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public void Run_SuccessIsZero()
    {
        Assert.Equal(0, new WinScriptRunner(_dir).Run("$x = 1 + 1"));
    }

    [Fact]
    public void Run_TimesOut()
    {
        var runner = new WinScriptRunner(_dir) { Timeout = TimeSpan.FromSeconds(2) };
        Assert.Equal(ProcessRunner.TimeoutExitCode, runner.Run("Start-Sleep -Seconds 30"));
    }
}
