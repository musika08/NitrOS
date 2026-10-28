namespace NitrOS.App.Logic.Running;

public sealed class RunnerSet
{
    private readonly Dictionary<RunKind, IRunner> _runners;

    public RunnerSet(IEnumerable<IRunner> runners)
    {
        ArgumentNullException.ThrowIfNull(runners);
        _runners = runners.ToDictionary(r => r.Kind);
    }

    public IRunner For(RunKind kind) =>
        _runners.TryGetValue(kind, out var r) ? r : throw new KeyNotFoundException($"No runner registered for {kind}");
}
