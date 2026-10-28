namespace NitrOS.App.Logic.Running;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NitrOS.App.Logic.Storage;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Live;
using NitrOS.Live.Systems;

public sealed class IsoRunner : IRunner
{
    private readonly IIsoBackend _backend;
    private readonly ISessionFacts _session;
    private readonly IsoBuildIndex _index;

    public RunKind Kind => RunKind.Iso;

    public IsoRunner(IIsoBackend backend, ISessionFacts session, IsoBuildIndex index)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _index = index ?? throw new ArgumentNullException(nameof(index));
    }

    public RunReadiness CheckReady(RunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Iso is null)
        {
            return new RunReadiness(false, Texts.IsoChoosePaths);
        }

        var buildReq = new BuildRequest(request.Iso.SourceIso, request.Iso.OutputIso, request.Iso.EditionIndex, request.Selection.On.ToList(), request.Card);
        var pathReason = _backend.PathsReason(buildReq);
        if (pathReason != null) return new RunReadiness(false, pathReason);

        var dismReason = _backend.HostDismReason();
        if (dismReason != null) return new RunReadiness(false, dismReason);

        var powerReason = BuildChecks.Power(_backend.Power());
        if (powerReason != null) return new RunReadiness(false, powerReason);

        if (request.Selection.On.Count == 0)
        {
            return new RunReadiness(false, "Turn on at least one switch.");
        }

        return new RunReadiness(true, null);
    }

    public async Task<RunResult> RunAsync(RunRequest request, IProgress<RunProgress> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        var guard = SessionGuard.Check(_session);
        if (!guard.Ok)
        {
            return new RunResult(RunKind.Iso, true, guard.Reason, false, new Dictionary<string, SwitchOutcome>(), [], [])
            {
                Request = request
            };
        }

        if (request.Iso is null)
        {
            return new RunResult(RunKind.Iso, true, Texts.IsoChoosePaths, false, new Dictionary<string, SwitchOutcome>(), [], [])
            {
                Request = request
            };
        }

        var buildReq = new BuildRequest(request.Iso.SourceIso, request.Iso.OutputIso, request.Iso.EditionIndex, request.Selection.On.ToList(), request.Card);
        var existing = _backend.FindResumable(request.Iso.OutputIso);
        BuildState state;
        if (existing != null)
        {
            state = existing;
        }
        else
        {
            try
            {
                state = _backend.Start(buildReq);
            }
            catch (BuildRefusedException bre)
            {
                return new RunResult(RunKind.Iso, true, bre.Message, false, new Dictionary<string, SwitchOutcome>(), [], [])
                {
                    Request = request
                };
            }
        }

        _index.Set(new IsoBuildRecord(state.Request.OutputIso, state.WorkFolder, state.BuildId, state.Request.Card, state.Request.SwitchIds));

        var buildProgress = new Progress<BuildProgress>(bp =>
        {
            progress.Report(new RunProgress(bp.SwitchId, bp.Done, bp.Total, bp.LogLine));
        });

        var res = await Task.Run(() => _backend.Run(state, buildProgress, ct), ct);

        var outcomeMap = new Dictionary<string, SwitchOutcome>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, sr) in res.Switches)
        {
            outcomeMap[id] = sr switch
            {
                SwitchImageResult.Applied => SwitchOutcome.Applied,
                SwitchImageResult.PartlyApplied => SwitchOutcome.PartlyApplied,
                SwitchImageResult.Failed => SwitchOutcome.Failed,
                _ => SwitchOutcome.Skipped
            };
        }

        if (res.Status == BuildStatus.Done)
        {
            _index.Clear();
        }

        var isCancelled = res.Status == BuildStatus.Cancelled || res.Status == BuildStatus.StoppedLowBattery;
        var isRefused = res.Status == BuildStatus.Refused;

        return new RunResult(
            RunKind.Iso,
            isRefused,
            res.Reason,
            isCancelled,
            outcomeMap,
            [],
            res.Problems)
        {
            OutputPath = res.OutputPath,
            NeedsRestart = false,
            Request = request
        };
    }
}
