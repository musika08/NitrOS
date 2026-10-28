using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Storage;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Launch;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.App.Logic.Running;

public sealed class TuneRunner(
    ITuneBackendFactory backends,
    ISessionFacts session,
    IWindowsVersion version,
    ITuneLockProbe lockProbe,
    JournalAccess journals,
    IMachineMeter meter,
    AppStateStore state,
    AfterRestartLaunch? afterRestart,
    string exePath,
    Action<TimeSpan> sleep) : IRunner
{
    private readonly ITuneBackendFactory _backends = backends ?? throw new ArgumentNullException(nameof(backends));
    private readonly ISessionFacts _session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly IWindowsVersion _version = version ?? throw new ArgumentNullException(nameof(version));
    private readonly ITuneLockProbe _lockProbe = lockProbe ?? throw new ArgumentNullException(nameof(lockProbe));
    private readonly JournalAccess _journals = journals ?? throw new ArgumentNullException(nameof(journals));
    private readonly IMachineMeter _meter = meter ?? throw new ArgumentNullException(nameof(meter));
    private readonly AppStateStore _state = state ?? throw new ArgumentNullException(nameof(state));
    private readonly AfterRestartLaunch? _afterRestart = afterRestart;
    private readonly string _exePath = exePath ?? throw new ArgumentNullException(nameof(exePath));
    private readonly Action<TimeSpan> _sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));

    public RunKind Kind => RunKind.Tune;

    public RunReadiness CheckReady(RunRequest request)
    {
        if (request.Kind != RunKind.Tune) return new RunReadiness(false, "Run kind must be Tune.");
        var sessionCheck = SessionGuard.Check(_session);
        if (!sessionCheck.Ok) return new RunReadiness(false, sessionCheck.Reason);
        if (_lockProbe.IsRunInProgress()) return new RunReadiness(false, TuneEngine.BusyReason);
        return new RunReadiness(true, null);
    }

    public async Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. SessionGuard.Check(session); not Ok -> Refused with its reason. Nothing else runs.
        var sessionCheck = SessionGuard.Check(_session);
        if (!sessionCheck.Ok)
        {
            return new RunResult(
                RunKind.Tune,
                Refused: true,
                RefusedReason: sessionCheck.Reason,
                Cancelled: false,
                Switches: new Dictionary<string, SwitchOutcome>(),
                MayBeChanged: [],
                Problems: []);
        }

        // 2. Selection.Mode must be TunePc and the selection non-empty, else ArgumentException.
        if (request.Selection.Mode != TargetMode.TunePc || request.Selection.On.Count == 0)
        {
            throw new ArgumentException("Selection must be in TunePc mode and non-empty.");
        }

        // 3. TuneEngine.IsRunInProgress() -> Refused with TuneEngine.BusyReason.
        if (_lockProbe.IsRunInProgress())
        {
            return new RunResult(
                RunKind.Tune,
                Refused: true,
                RefusedReason: TuneEngine.BusyReason,
                Cancelled: false,
                Switches: new Dictionary<string, SwitchOutcome>(),
                MayBeChanged: [],
                Problems: []);
        }

        // 4. Before = MachineMeter.Sample(meter, 3, 1 s, sleep).
        var before = MachineMeter.Sample(_meter, 3, TimeSpan.FromSeconds(1), _sleep);

        // 5. journals.OpenForRun(); on an exception -> Refused "The NitrOS data folder could not be prepared: <message>".
        JournalStore store;
        try
        {
            store = _journals.OpenForRun();
        }
        catch (Exception ex)
        {
            return new RunResult(
                RunKind.Tune,
                Refused: true,
                RefusedReason: $"The NitrOS data folder could not be prepared: {ex.Message}",
                Cancelled: false,
                Switches: new Dictionary<string, SwitchOutcome>(),
                MayBeChanged: [],
                Problems: []);
        }

        // 6. backend.Apply
        var backend = _backends.Create(store);
        var tuneProgress = new TuneProgressAdapter(progress, request.Selection);

        var options = new TuneOptions(request.AllowWithoutRestorePoint, _session.ProcessUserSid, _version.FullBuild);
        var tuneResult = await Task.Run(() => backend.Apply(request.Selection, options, tuneProgress, ct));

        // 7. After = MachineMeter.Sample(...)
        var after = MachineMeter.Sample(_meter, 3, TimeSpan.FromSeconds(1), _sleep);

        // 8. If any switch is Applied or PartlyApplied or MayBeChanged is not empty:
        bool needsRestart = false;
        bool hasApplied = tuneResult.Switches.Values.Any(s => s == SwitchResult.Applied || s == SwitchResult.PartlyApplied)
            || tuneResult.MayBeChanged.Count > 0;

        if (hasApplied)
        {
            string runId = !string.IsNullOrEmpty(tuneResult.JournalPath)
                ? Path.GetFileNameWithoutExtension(tuneResult.JournalPath)
                : DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            _state.SetHealthPending(new HealthPending(runId, _session.ProcessUserSid, before, DateTimeOffset.Now));
            _afterRestart?.TryRegister(_exePath);
            needsRestart = true;
        }

        // 9. Map TuneResult to RunResult
        var switches = tuneResult.Switches.ToDictionary(
            kv => kv.Key,
            kv => kv.Value switch
            {
                SwitchResult.Applied => SwitchOutcome.Applied,
                SwitchResult.PartlyApplied => SwitchOutcome.PartlyApplied,
                SwitchResult.Failed => SwitchOutcome.Failed,
                SwitchResult.Skipped => SwitchOutcome.Skipped,
                _ => SwitchOutcome.Failed
            });

        return new RunResult(
            RunKind.Tune,
            Refused: tuneResult.Refused,
            RefusedReason: tuneResult.RefusedReason,
            Cancelled: ct.IsCancellationRequested,
            Switches: switches,
            MayBeChanged: tuneResult.MayBeChanged,
            Problems: tuneResult.Problems)
        {
            CanRetryWithoutRestorePoint = tuneResult.Refused && tuneResult.RefusedReason == TuneEngine.NoRestorePointReason,
            JournalPath = string.IsNullOrEmpty(tuneResult.JournalPath) ? null : tuneResult.JournalPath,
            RestorePoint = tuneResult.RestorePoint,
            Before = before,
            After = after,
            NeedsRestart = needsRestart
        };
    }

    private sealed class TuneProgressAdapter(IProgress<RunProgress>? progress, Selection selection) : IProgress<TuneProgress>
    {
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

        public void Report(TuneProgress p)
        {
            bool first = _seen.Add(p.SwitchId);
            string? name = selection.Catalog.Switches.FirstOrDefault(s => s.Id == p.SwitchId)?.Name ?? p.SwitchId;
            string? log = first ? $"Changing: {name}" : null;
            progress?.Report(new RunProgress(p.SwitchId, p.Done, p.Total, log));
        }
    }
}
