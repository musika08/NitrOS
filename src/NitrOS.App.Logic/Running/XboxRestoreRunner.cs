using NitrOS.App.Logic.Ports;
using NitrOS.Live;

namespace NitrOS.App.Logic.Running;

public sealed class XboxRestoreRunner(
    IXboxRestore restore,
    ISessionFacts session,
    ITuneLockProbe lockProbe) : IRunner, IXboxRestorer
{
    private readonly IXboxRestore _restore = restore ?? throw new ArgumentNullException(nameof(restore));
    private readonly ISessionFacts _session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly ITuneLockProbe _lockProbe = lockProbe ?? throw new ArgumentNullException(nameof(lockProbe));

    public RunKind Kind => RunKind.XboxRestore;
    public bool IsAvailable => true;
    public string? UnavailableReason => null;

    public RunReadiness CheckReady(RunRequest request)
    {
        var check = SessionGuard.Check(_session);
        if (!check.Ok) return new RunReadiness(false, check.Reason);
        if (_lockProbe.IsRunInProgress()) return new RunReadiness(false, TuneEngine.BusyReason);
        return new RunReadiness(true, null);
    }

    public async Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct)
    {
        var check = SessionGuard.Check(_session);
        if (!check.Ok)
        {
            return new RunResult(
                RunKind.XboxRestore,
                Refused: true,
                RefusedReason: check.Reason,
                Cancelled: false,
                Switches: new Dictionary<string, SwitchOutcome>(),
                MayBeChanged: [],
                Problems: []);
        }

        if (_lockProbe.IsRunInProgress())
        {
            return new RunResult(
                RunKind.XboxRestore,
                Refused: true,
                RefusedReason: TuneEngine.BusyReason,
                Cancelled: false,
                Switches: new Dictionary<string, SwitchOutcome>(),
                MayBeChanged: [],
                Problems: []);
        }

        var directProgress = new DirectStringProgress(s => progress?.Report(new RunProgress(null, 0, 0, s)));
        var restoreResult = await Task.Run(() => _restore.Run(directProgress, ct));

        var switches = new Dictionary<string, SwitchOutcome>();
        var problems = new List<(string SwitchId, string Detail)>();
        foreach (var (part, ok, detail) in restoreResult.Parts)
        {
            switches[part] = ok ? SwitchOutcome.Applied : SwitchOutcome.Failed;
            if (!ok && detail is not null)
                problems.Add((part, detail));
        }

        return new RunResult(
            RunKind.XboxRestore,
            Refused: false,
            RefusedReason: null,
            Cancelled: ct.IsCancellationRequested,
            Switches: switches,
            MayBeChanged: [],
            Problems: problems);
    }

    private sealed class DirectStringProgress(Action<string> onReport) : IProgress<string>
    {
        public void Report(string value) => onReport(value);
    }
}
