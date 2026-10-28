using NitrOS.Updates.Firmware;
using NitrOS.Updates.KnownBad;
using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;
using NitrOS.Updates.Reading;
using NitrOS.Updates.Sources;

namespace NitrOS.Updates.Checking;

public sealed class UpdateChecker(
    IDeviceReader reader,
    IReadOnlyList<IUpdateSource> sources,
    KnownBadList knownBad,
    FirmwareMap map,
    WindowsUpdatePolicy policy,
    TimeProvider clock)
{
    private readonly IDeviceReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    private readonly IReadOnlyList<IUpdateSource> _sources = sources ?? Array.Empty<IUpdateSource>();
    private readonly KnownBadList _knownBad = knownBad ?? throw new ArgumentNullException(nameof(knownBad));
    private readonly FirmwareMap _map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly WindowsUpdatePolicy _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public DeviceInfo ReadDevice() => _reader.Read();

    public async Task<CheckReport> CheckAsync(IProgress<CheckProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var device = _reader.Read();
        var applicableSources = _sources.Where(s => s.AppliesTo(device)).ToList();

        var allItems = new List<UpdateItem>();
        var notices = new List<SourceNotice>();
        var links = new List<SupportLink>();

        if (device.Maker == MakerId.Other)
        {
            var noMakerProblem = string.Format(UpdateTexts.NoMakerListFormat, device.Manufacturer);
            notices.Add(new SourceNotice("Maker", noMakerProblem, null));
        }

        int total = applicableSources.Count;
        int completed = 0;

        progress?.Report(new CheckProgress("sources", 0, total));

        var tasks = applicableSources.Select(async source =>
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            SourceResult result;
            try
            {
                result = await source.CheckAsync(device, linkedCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), $"{source.Name} could not be checked: {ex.Message}", null);
            }

            var done = Interlocked.Increment(ref completed);
            progress?.Report(new CheckProgress(source.Name, done, total));

            return (Source: source, Result: result);
        }).ToList();

        var completedResults = await Task.WhenAll(tasks);

        foreach (var (src, res) in completedResults)
        {
            if (res.Status == SourceStatus.Unavailable)
            {
                notices.Add(new SourceNotice(src.Name, res.Problem ?? "Unknown problem", res.FallbackPage));
            }
            else if (res.Status == SourceStatus.Ok)
            {
                if (res.Items.Count > 0)
                {
                    allItems.AddRange(res.Items);
                }
                else if (res.FallbackPage != null)
                {
                    links.Add(new SupportLink($"Open {src.Name}'s support page", res.FallbackPage));
                }
            }
        }

        var merged = Merger.Merge(allItems, device, _knownBad, _map);
        var excluded = _policy.DriversExcluded();
        var checkedAt = _clock.GetUtcNow();

        return new CheckReport(device, merged, notices, links, excluded, checkedAt);
    }
}
