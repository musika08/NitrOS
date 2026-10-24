using System.Text;
using System.Xml.Linq;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

public sealed class WinTaskScheduler : ITaskScheduler
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Exit 0 is success; exit 1 is schtasks' generic error (which includes "task not found"); anything else is a real failure.</summary>
    public enum ExitClass { Ok, MaybeNotFound, Failed }

    public static ExitClass ClassifyQueryExit(int exitCode) => exitCode switch
    {
        0 => ExitClass.Ok,
        1 => ExitClass.MaybeNotFound,
        _ => ExitClass.Failed,
    };

    public TaskInfo? Get(string path)
    {
        ValidatePath(path);
        var (code, stdout, err) = ProcessRunner.Run("schtasks.exe", new[] { "/Query", "/TN", path, "/XML" }, Timeout);
        switch (ClassifyQueryExit(code))
        {
            case ExitClass.Ok:
                return new TaskInfo(ParseEnabled(stdout));
            case ExitClass.MaybeNotFound:
                // Exit 1 also covers access denied etc.; the message text is localised, so confirm against the full task list.
                if (!ListedTasks().Contains(path, StringComparer.OrdinalIgnoreCase)) return null;
                throw Failure("/Query", path, code, err);
            default:
                throw Failure("/Query", path, code, err);
        }
    }

    public void SetEnabled(string path, bool enabled)
    {
        ValidatePath(path);
        var (code, _, err) = ProcessRunner.Run("schtasks.exe", new[] { "/Change", "/TN", path, enabled ? "/ENABLE" : "/DISABLE" }, Timeout);
        if (code != 0) throw Failure("/Change", path, code, err);
    }

    /// <summary>Task paths must be absolute (start with a backslash), name a task (no trailing backslash) and contain no quote.</summary>
    public static void ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '\\' || path.EndsWith('\\') || path.Contains('"'))
            throw new ArgumentException($"Invalid task path '{path}'", nameof(path));
    }

    /// <summary>Reads Settings/Enabled from a task's XML; an absent element means enabled (Task Scheduler schema default).</summary>
    public static bool ParseEnabled(string xml)
    {
        var doc = XDocument.Parse(xml.TrimStart('﻿', ' ', '\r', '\n', '\t'));
        var settings = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Settings");
        var enabled = settings?.Elements().FirstOrDefault(e => e.Name.LocalName == "Enabled");
        return enabled is null || !enabled.Value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> ListedTasks()
    {
        var (code, stdout, err) = ProcessRunner.Run("schtasks.exe", new[] { "/Query", "/FO", "CSV", "/NH" }, Timeout);
        if (code != 0) throw Failure("/Query (list)", "", code, err);
        var names = new List<string>();
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var cols = ParseCsvLine(line.TrimEnd('\r'));
            if (cols.Count > 0 && cols[0].Length > 0) names.Add(cols[0]);
        }
        return names;
    }

    private static InvalidOperationException Failure(string op, string path, int code, string err) =>
        new($"schtasks {op} failed for '{path}' (exit {code}): {err.Trim()}");

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
