namespace NitrOS.App.Logic.Running;

public sealed class UnavailableIsoRunner : IRunner
{
    public RunKind Kind => RunKind.Iso;

    public RunReadiness CheckReady(RunRequest request) => new(false, Texts.IsoLater);

    public Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct) =>
        throw new InvalidOperationException("IsoRunner is not available.");
}
