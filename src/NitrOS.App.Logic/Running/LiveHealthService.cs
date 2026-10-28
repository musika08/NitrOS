using NitrOS.App.Logic.Ports;
using NitrOS.Live;

namespace NitrOS.App.Logic.Running;

public sealed class LiveHealthService(IHealthProbe probe) : IHealthService
{
    private readonly IHealthProbe _probe = probe ?? throw new ArgumentNullException(nameof(probe));

    public Task<HealthReport> RunAsync(IReadOnlySet<string> appliedSwitchIds, CancellationToken ct) =>
        Task.Run(() => HealthCheck.Run(_probe, appliedSwitchIds), ct);
}
