using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

public sealed class WinRestarter : IRestarter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public void Restart()
    {
        var (code, stdout, stderr) = ProcessRunner.Run("shutdown.exe", ["/r", "/t", "0", "/d", "p:2:4"], Timeout);
        if (code != 0)
            throw new InvalidOperationException($"shutdown.exe failed with exit code {code}: {stderr}");
    }
}
