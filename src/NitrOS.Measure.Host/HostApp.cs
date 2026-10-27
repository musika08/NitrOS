using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Journal;
using NitrOS.Measure.Campaign;
using NitrOS.Measure.Guest;
using NitrOS.Measure.Results;
using NitrOS.Measure.Unattend;

namespace NitrOS.Measure.Host;

public static class HostApp
{
    public static int Run(
        string[] args,
        TextWriter output,
        IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(environment);

        if (args.Length == 0)
        {
            return 2;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "plan" => RunPlan(rest, output),
                "next" => RunNext(rest, output),
                "complete" => RunComplete(rest, output),
                "fail" => RunFail(rest, output),
                "reset" => RunReset(rest, output),
                "status" => RunStatus(rest, output),
                "summarize-run" => RunSummarizeRun(rest, output),
                "results" => RunResults(rest, output),
                "compare-fingerprints" => RunCompareFingerprints(rest, output),
                "answer-iso" => RunAnswerIso(rest, output, environment),
                _ => 2,
            };
        }
        catch (UsageException)
        {
            return 2;
        }
        catch (Exception ex)
        {
            output.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private sealed class UsageException : Exception;

    private static int RunPlan(string[] args, TextWriter output)
    {
        string? campaignDir = null, catalogDir = null, build = null, commit = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--campaign" when i + 1 < args.Length: campaignDir = args[++i]; break;
                case "--catalog" when i + 1 < args.Length: catalogDir = args[++i]; break;
                case "--build" when i + 1 < args.Length: build = args[++i]; break;
                case "--cli-commit" when i + 1 < args.Length: commit = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(campaignDir) || string.IsNullOrEmpty(catalogDir) ||
            string.IsNullOrEmpty(build) || string.IsNullOrEmpty(commit))
        {
            throw new UsageException();
        }

        var catResult = CatalogLoader.Load(catalogDir);
        if (catResult.Catalog is null)
        {
            output.WriteLine($"Failed to load catalog: {string.Join("; ", catResult.Errors)}");
            return 1;
        }

        var runs = CampaignPlanner.Build(catResult.Catalog, DeviceProfile.Desktop);
        var store = new CampaignStore(campaignDir);
        store.Create(Path.GetFileName(campaignDir), build, commit, runs);
        output.WriteLine($"Campaign plan created with {runs.Count} runs.");
        return 0;
    }

    private static int RunNext(string[] args, TextWriter output)
    {
        string? campaignDir = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--campaign" when i + 1 < args.Length: campaignDir = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(campaignDir)) throw new UsageException();

        var store = new CampaignStore(campaignDir);
        var next = store.Next();
        if (next is null)
        {
            return 4;
        }

        output.WriteLine(CatalogJson.Write(next));
        return 0;
    }

    private static int RunComplete(string[] args, TextWriter output)
    {
        string? campaignDir = null, runId = null, resultFile = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--campaign" when i + 1 < args.Length: campaignDir = args[++i]; break;
                case "--run" when i + 1 < args.Length: runId = args[++i]; break;
                case "--result" when i + 1 < args.Length: resultFile = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(campaignDir) || string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(resultFile))
        {
            throw new UsageException();
        }

        var resultJson = File.ReadAllText(resultFile);
        var result = JsonSerializer.Deserialize<RunResult>(resultJson, CatalogJson.Options)
            ?? throw new InvalidOperationException("Result file is null.");

        var store = new CampaignStore(campaignDir);
        store.Complete(runId, result);
        output.WriteLine($"Run '{runId}' completed.");
        return 0;
    }

    private static int RunFail(string[] args, TextWriter output)
    {
        string? campaignDir = null, runId = null, error = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--campaign" when i + 1 < args.Length: campaignDir = args[++i]; break;
                case "--run" when i + 1 < args.Length: runId = args[++i]; break;
                case "--error" when i + 1 < args.Length: error = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(campaignDir) || string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(error))
        {
            throw new UsageException();
        }

        var store = new CampaignStore(campaignDir);
        store.Fail(runId, error);
        output.WriteLine($"Run '{runId}' marked failed.");
        return 0;
    }

    private static int RunReset(string[] args, TextWriter output)
    {
        string? campaignDir = null, runId = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--campaign" when i + 1 < args.Length: campaignDir = args[++i]; break;
                case "--run" when i + 1 < args.Length: runId = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(campaignDir) || string.IsNullOrEmpty(runId)) throw new UsageException();

        var store = new CampaignStore(campaignDir);
        store.Reset(runId);
        output.WriteLine($"Run '{runId}' reset to pending.");
        return 0;
    }

    private static int RunStatus(string[] args, TextWriter output)
    {
        string? campaignDir = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--campaign" when i + 1 < args.Length: campaignDir = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(campaignDir)) throw new UsageException();

        var store = new CampaignStore(campaignDir);
        var state = store.Load();

        int pending = state.States.Count(s => s.Status == Campaign.RunStatus.Pending);
        int running = state.States.Count(s => s.Status == Campaign.RunStatus.Running);
        int done = state.States.Count(s => s.Status == Campaign.RunStatus.Done);
        int failed = state.States.Count(s => s.Status == Campaign.RunStatus.Failed);

        int remaining = pending + running;
        int estimatedMinutes = remaining * 20;

        output.WriteLine($"Status for campaign '{state.CampaignId}':");
        output.WriteLine($"  Pending: {pending}");
        output.WriteLine($"  Running: {running}");
        output.WriteLine($"  Done: {done}");
        output.WriteLine($"  Failed: {failed}");
        output.WriteLine($"  Estimated time left: {estimatedMinutes / 60}h {estimatedMinutes % 60}m (~{remaining} runs @ 20m)");
        return 0;
    }

    private static int RunSummarizeRun(string[] args, TextWriter output)
    {
        string? runId = null, samplesFile = null, journalFile = null, outFile = null;
        int? applyExit = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--run" when i + 1 < args.Length: runId = args[++i]; break;
                case "--samples" when i + 1 < args.Length: samplesFile = args[++i]; break;
                case "--journal" when i + 1 < args.Length: journalFile = args[++i]; break;
                case "--apply-exit" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var ae)) throw new UsageException();
                    applyExit = ae;
                    break;
                case "--out" when i + 1 < args.Length: outFile = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(samplesFile) || string.IsNullOrEmpty(outFile))
        {
            throw new UsageException();
        }

        var sampleRun = JsonSerializer.Deserialize<SampleRun>(File.ReadAllText(samplesFile), CatalogJson.Options)
            ?? throw new InvalidOperationException("Failed to read SampleRun.");

        var median = Stats.Summarize(sampleRun.Samples);

        var switchResults = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(journalFile) && File.Exists(journalFile))
        {
            var journal = JsonSerializer.Deserialize<RunJournal>(File.ReadAllText(journalFile), CatalogJson.Options);
            if (journal is not null)
            {
                var grouped = journal.Entries.GroupBy(e => e.SwitchId);
                foreach (var g in grouped)
                {
                    var outcomes = g.Select(e => e.Outcome).ToList();
                    string outcome;
                    if (outcomes.Any(o => o == ActionOutcome.Failed || o == ActionOutcome.RolledBack))
                    {
                        outcome = "Failed";
                    }
                    else if (outcomes.All(o => o == ActionOutcome.Skipped))
                    {
                        outcome = "Skipped";
                    }
                    else
                    {
                        outcome = "Applied";
                    }
                    switchResults[g.Key] = outcome;
                }
            }
        }

        var runResult = new RunResult(
            RunId: runId,
            WindowsBuild: sampleRun.WindowsBuild,
            VisibleMb: sampleRun.VisibleMb,
            Samples: sampleRun.Samples,
            Median: median,
            ApplyExitCode: applyExit,
            SwitchResults: switchResults,
            Slept: sampleRun.Slept,
            Started: sampleRun.Started,
            Finished: sampleRun.Finished);

        var outDir = Path.GetDirectoryName(outFile);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

        File.WriteAllText(outFile, CatalogJson.Write(runResult));
        output.WriteLine($"Run summarized to '{outFile}'.");
        return 0;
    }

    private static int RunResults(string[] args, TextWriter output)
    {
        string? campaignDir = null, catalogDir = null;
        bool write = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--campaign" when i + 1 < args.Length: campaignDir = args[++i]; break;
                case "--catalog" when i + 1 < args.Length: catalogDir = args[++i]; break;
                case "--write": write = true; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(campaignDir) || string.IsNullOrEmpty(catalogDir)) throw new UsageException();

        var store = new CampaignStore(campaignDir);
        var state = store.Load();

        var results = new Dictionary<string, RunResult>();
        var runsDir = Path.Combine(campaignDir, "runs");
        if (Directory.Exists(runsDir))
        {
            foreach (var file in Directory.GetFiles(runsDir, "*.result.json"))
            {
                var r = JsonSerializer.Deserialize<RunResult>(File.ReadAllText(file), CatalogJson.Options);
                if (r is not null)
                {
                    results[r.RunId] = r;
                }
            }
        }

        var catResult = CatalogLoader.Load(catalogDir);
        if (catResult.Catalog is null)
        {
            output.WriteLine($"Failed to load catalog: {string.Join("; ", catResult.Errors)}");
            return 1;
        }

        var svchost = SvchostChoice.FromCatalog(catResult.Catalog);
        var outcome = ResultsCalculator.Compute(state, results, catResult.Catalog, DateOnly.FromDateTime(DateTime.UtcNow), svchost);

        var reportMd = ReportWriter.Markdown(outcome, state, results);
        File.WriteAllText(Path.Combine(campaignDir, "report.md"), reportMd);
        output.WriteLine("Report written to report.md");

        if (write)
        {
            CatalogMeasurementWriter.Write(catalogDir, outcome.Switches, outcome.Cards);
            output.WriteLine("Catalog updated with measurements.");
        }

        return 0;
    }

    private static int RunCompareFingerprints(string[] args, TextWriter output)
    {
        string? beforeFile = null, afterFile = null, catalogDir = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--before" when i + 1 < args.Length: beforeFile = args[++i]; break;
                case "--after" when i + 1 < args.Length: afterFile = args[++i]; break;
                case "--catalog" when i + 1 < args.Length: catalogDir = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(beforeFile) || string.IsNullOrEmpty(afterFile) || string.IsNullOrEmpty(catalogDir))
        {
            throw new UsageException();
        }

        var before = JsonSerializer.Deserialize<Fingerprint>(File.ReadAllText(beforeFile), CatalogJson.Options)
            ?? throw new InvalidOperationException("Failed to read before fingerprint.");
        var after = JsonSerializer.Deserialize<Fingerprint>(File.ReadAllText(afterFile), CatalogJson.Options)
            ?? throw new InvalidOperationException("Failed to read after fingerprint.");

        var catResult = CatalogLoader.Load(catalogDir);
        if (catResult.Catalog is null)
        {
            output.WriteLine($"Failed to load catalog: {string.Join("; ", catResult.Errors)}");
            return 1;
        }

        var diffs = FingerprintTool.Compare(before, after, catResult.Catalog);
        foreach (var d in diffs)
        {
            output.WriteLine($"{d.Key}: expected={d.Expected}, before={d.Before}, after={d.After}");
        }

        return diffs.Any(d => !d.Expected) ? 1 : 0;
    }

    private static int RunAnswerIso(
        string[] args,
        TextWriter output,
        IReadOnlyDictionary<string, string?> environment)
    {
        string? template = null, outIso = null, user = null, computerName = "NITROS-MEASURE";
        int? imageIndex = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--template" when i + 1 < args.Length: template = args[++i]; break;
                case "--out" when i + 1 < args.Length: outIso = args[++i]; break;
                case "--image-index" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var idx)) throw new UsageException();
                    imageIndex = idx;
                    break;
                case "--user" when i + 1 < args.Length: user = args[++i]; break;
                case "--computer-name" when i + 1 < args.Length: computerName = args[++i]; break;
                default: throw new UsageException();
            }
        }

        if (string.IsNullOrEmpty(template) || string.IsNullOrEmpty(outIso) ||
            string.IsNullOrEmpty(user) || !imageIndex.HasValue)
        {
            throw new UsageException();
        }

        if (!environment.TryGetValue("NITROS_MEASURE_PASSWORD", out var password) || string.IsNullOrEmpty(password))
        {
            output.WriteLine("Refused: NITROS_MEASURE_PASSWORD environment variable is missing.");
            return 1;
        }

        var templateText = File.ReadAllText(template);
        var rendered = AnswerFile.Render(templateText, imageIndex.Value, user, password, computerName);
        AnswerIso.Write(outIso, "autounattend.xml", rendered);
        output.WriteLine($"Answer ISO written to '{outIso}'.");
        return 0;
    }
}
