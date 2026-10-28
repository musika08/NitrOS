using NitrOS.Live;

namespace NitrOS.App.Logic.Ports;

public interface IHealthService
{
    Task<HealthReport> RunAsync(IReadOnlySet<string> appliedSwitchIds, CancellationToken ct);
}
