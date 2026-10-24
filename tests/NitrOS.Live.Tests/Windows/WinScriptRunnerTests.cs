using System.Text;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>The scripts here only write inside the test's own temp folder.</summary>
public sealed class WinScriptRunnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));

    public WinScriptRunnerTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Quote(string path) => "'" + path.Replace("'", "''") + "'";

    private static string[] ScriptLeftovers() =>
        Directory.GetDirectories(Path.GetTempPath(), "NitrOS-script-*");

    [Fact]
    public void Run_ExecutesBodyAndReturnsExitCode()
    {
        var before = ScriptLeftovers();
        var output = Path.Combine(_dir, "out.txt");
        var body = $"Set-Content -LiteralPath {Quote(output)} -Value ('héllo \"quoted\" ' + [char]0x263A) -Encoding UTF8\r\nexit 3";
        var code = new WinScriptRunner().Run(body);

        Assert.Equal(3, code);
        Assert.Equal("héllo \"quoted\" ☺", File.ReadAllText(output).Trim());
        Assert.Equal(new[] { output }, Directory.GetFileSystemEntries(_dir));
        Assert.Equal(before, ScriptLeftovers()); // no temporary script file any more
    }

    [Fact]
    public void Run_SuccessIsZero()
    {
        Assert.Equal(0, new WinScriptRunner().Run("$x = 1 + 1"));
    }

    [Fact]
    public void Run_TimesOut()
    {
        var runner = new WinScriptRunner { Timeout = TimeSpan.FromSeconds(2) };
        Assert.Equal(ProcessRunner.TimeoutExitCode, runner.Run("Start-Sleep -Seconds 30"));
    }

    [Fact]
    public void Encode_IsBase64Utf16LeAndBoundsLength()
    {
        Assert.Equal("exit 3", Encoding.Unicode.GetString(Convert.FromBase64String(WinScriptRunner.Encode("exit 3"))));
        Assert.Throws<ArgumentException>(() => WinScriptRunner.Encode(new string('x', 20_000)));
        Assert.Throws<ArgumentNullException>(() => WinScriptRunner.Encode(null!));
    }
}
