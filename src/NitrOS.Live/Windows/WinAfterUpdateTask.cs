using System.Text;
using NitrOS.Live.Launch;

namespace NitrOS.Live.Windows;

public sealed class WinAfterUpdateTask(string taskPath = @"\NitrOS\CheckAfterUpdates") : IAfterUpdateTask
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public bool IsRegistered()
    {
        var (code, _, _) = ProcessRunner.Run("schtasks.exe", ["/Query", "/TN", taskPath], Timeout);
        return code == 0;
    }

    public void Register(string exePath, string userSid)
    {
        var xml = AfterUpdateTaskXml.Build(exePath, userSid);
        var tempFile = Path.Combine(Path.GetTempPath(), $"nitros-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(tempFile, xml, Encoding.Unicode);
            var (code, stdout, stderr) = ProcessRunner.Run("schtasks.exe", ["/Create", "/TN", taskPath, "/XML", tempFile, "/F"], Timeout);
            if (code != 0)
                throw new InvalidOperationException($"schtasks /Create failed with code {code}: {stderr}");
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { /* ignore */ }
        }
    }

    public void Remove()
    {
        ProcessRunner.Run("schtasks.exe", ["/Delete", "/TN", taskPath, "/F"], Timeout);
    }
}
