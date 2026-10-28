using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Storage;
using NitrOS.Live;
using NitrOS.Live.Systems;

namespace NitrOS.App.Logic.Running;

public sealed class LiveDriftService(
    JournalAccess journals,
    Func<LiveSystems> adapters,
    ISessionFacts session) : IDriftService
{
    private readonly JournalAccess _journals = journals ?? throw new ArgumentNullException(nameof(journals));
    private readonly Func<LiveSystems> _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
    private readonly ISessionFacts _session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task<DriftFindings> CheckAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var listing = _journals.ListMine();
            if (listing.FolderProblem is not null)
            {
                return new DriftFindings([], [], listing.FolderProblem);
            }

            var drifted = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<(string SwitchId, string Error)>();

            foreach (var (_, journal) in listing.Mine)
            {
                try
                {
                    var checker = new DriftChecker(_adapters());
                    var report = checker.Check(journal, _session.ProcessUserSid);

                    foreach (var id in report.Drifted)
                    {
                        if (seen.Add(id))
                            drifted.Add(id);
                    }

                    errors.AddRange(report.Errors);
                }
                catch (JournalOwnerException)
                {
                    errors.Add((journal.RunId, TuneEngine.OtherAccountReason));
                }
                catch (Exception ex)
                {
                    errors.Add((journal.RunId, ex.Message));
                }
            }

            return new DriftFindings(drifted, errors, null);
        }, ct);
    }
}
