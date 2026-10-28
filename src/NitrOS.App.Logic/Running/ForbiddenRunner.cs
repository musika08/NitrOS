using NitrOS.App.Logic.Ports;
using NitrOS.Core.Model;

namespace NitrOS.App.Logic.Running;

public sealed class ForbiddenRunner(RunKind kind) : IRunner
{
    public RunKind Kind => kind;

    public RunReadiness CheckReady(RunRequest request) =>
        new(false, "Smoke test: nothing is applied.");

    public Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct) =>
        throw new InvalidOperationException("Smoke test cannot apply anything.");
}
