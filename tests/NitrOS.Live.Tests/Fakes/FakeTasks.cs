using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeTasks : ITaskScheduler
{
    public Dictionary<string, bool> Enabled { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? FailOn { get; set; }
    /// <summary>When true every SetEnabled throws UnauthorizedAccessException (reads still work).</summary>
    public bool RefuseWrites { get; set; }
    /// <summary>Paths written by SetEnabled.</summary>
    public List<string> Writes { get; } = [];

    private void Check(string path)
    {
        if (FailOn is not null && string.Equals(FailOn, path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"FakeTasks failure on {path}");
    }

    public TaskInfo? Get(string path)
    {
        Check(path);
        return Enabled.TryGetValue(path, out var e) ? new TaskInfo(e) : null;
    }

    public void SetEnabled(string path, bool enabled)
    {
        Check(path);
        if (!Enabled.ContainsKey(path)) throw new InvalidOperationException($"No task {path}");
        if (RefuseWrites) throw new UnauthorizedAccessException($"Access to the task '{path}' is denied.");
        Writes.Add(path);
        Enabled[path] = enabled;
    }
}
