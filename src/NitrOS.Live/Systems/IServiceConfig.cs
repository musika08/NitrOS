using NitrOS.Core.Model;

namespace NitrOS.Live.Systems;

public sealed record ServiceStartInfo(ServiceStartType Start);

public interface IServiceConfig
{
    ServiceStartInfo? Get(string name);
    void Set(string name, ServiceStartType start);
    IReadOnlyList<string> PerUserInstances(string template);
}
