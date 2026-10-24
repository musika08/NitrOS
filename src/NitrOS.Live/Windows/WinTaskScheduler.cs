using System.Text;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

public sealed class WinTaskScheduler : ITaskScheduler
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public TaskInfo? Get(string path)
    {
        var (code, stdout, _) = ProcessRunner.Run("schtasks.exe", $"/Query /TN \"{path}\" /FO CSV /NH", Timeout);
        if (code != 0) return null;
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var cols = ParseCsvLine(line.TrimEnd('\r'));
            if (cols.Count < 3) continue;
            return new TaskInfo(!cols[2].Equals("Disabled", StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    public void SetEnabled(string path, bool enabled)
    {
        var (code, _, err) = ProcessRunner.Run("schtasks.exe", $"/Change /TN \"{path}\" /{(enabled ? "ENABLE" : "DISABLE")}", Timeout);
        if (code != 0) throw new InvalidOperationException($"schtasks failed for '{path}' (exit {code}): {err.Trim()}");
    }

    public static List<string> ParseCsvLine(string line)
    {
        var cols = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { cols.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        cols.Add(sb.ToString());
        return cols;
    }
}
