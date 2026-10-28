namespace NitrOS.App.Logic.Running;

public interface IRunner
{
    RunKind Kind { get; }

    /// <summary>Cheap and side-effect free; Review calls it to enable Go.</summary>
    RunReadiness CheckReady(RunRequest request);

    /// <summary>Never throws for an expected refusal; returns Refused with a plain reason.</summary>
    Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct);
}
