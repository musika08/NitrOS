namespace NitrOS.Live.Systems;

public sealed record TaskInfo(bool Enabled);

public interface ITaskScheduler
{
    TaskInfo? Get(string path);
    void SetEnabled(string path, bool enabled);
}
