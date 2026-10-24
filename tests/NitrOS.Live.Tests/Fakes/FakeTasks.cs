using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeTasks : ITaskScheduler
{
    public Dictionary<string, bool> Enabled { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? FailOn { get; set; }

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
        Enabled[path] = enabled;
    }
}
