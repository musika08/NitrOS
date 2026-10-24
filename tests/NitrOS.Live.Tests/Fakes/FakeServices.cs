using NitrOS.Core.Model;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeServices : IServiceConfig
{
    public Dictionary<string, ServiceStartType> Starts { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Template name to its per-user instance names.</summary>
    public Dictionary<string, List<string>> Instances { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? FailOn { get; set; }
    /// <summary>When true every Set throws UnauthorizedAccessException (reads still work).</summary>
    public bool RefuseWrites { get; set; }
    /// <summary>Names written by Set.</summary>
    public List<string> Writes { get; } = [];

    private void Check(string name)
    {
        if (FailOn is not null && string.Equals(FailOn, name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"FakeServices failure on {name}");
    }

    public ServiceStartInfo? Get(string name)
    {
        Check(name);
        return Starts.TryGetValue(name, out var s) ? new ServiceStartInfo(s) : null;
    }

    public void Set(string name, ServiceStartType start)
    {
        Check(name);
        if (RefuseWrites) throw new UnauthorizedAccessException($"Access to the service '{name}' is denied.");
        Writes.Add(name);
        Starts[name] = start;
    }

    public IReadOnlyList<string> PerUserInstances(string template)
    {
        Check(template);
        return Instances.TryGetValue(template, out var l) ? l : [];
    }
}
