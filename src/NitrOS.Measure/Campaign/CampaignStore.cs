using System.Text.Json;
using NitrOS.Core.Json;

namespace NitrOS.Measure.Campaign;

public sealed class CampaignStore(string directory)
{
    public const int MaxAttempts = 3;

    private readonly string _campaignFile = Path.Combine(directory, "campaign.json");
    private readonly string _runsDir = Path.Combine(directory, "runs");

    public CampaignState Create(string campaignId, string windowsBuild, string cliCommit, IReadOnlyList<RunSpec> runs)
    {
        ArgumentNullException.ThrowIfNull(campaignId);
        ArgumentNullException.ThrowIfNull(windowsBuild);
        ArgumentNullException.ThrowIfNull(cliCommit);
        ArgumentNullException.ThrowIfNull(runs);

        if (File.Exists(_campaignFile))
        {
            throw new InvalidOperationException($"Campaign file already exists at '{_campaignFile}'.");
        }

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(_runsDir);

        var states = runs.Select(r => new RunState(r.RunId, RunStatus.Pending, 0, null, null)).ToList();
        var state = new CampaignState(
            CampaignId: campaignId,
            WindowsBuild: windowsBuild,
            CliCommit: cliCommit,
            Created: DateTimeOffset.UtcNow,
            Runs: runs,
            States: states);

        SaveState(state);
        return state;
    }

    public CampaignState Load()
    {
        if (!File.Exists(_campaignFile))
        {
            throw new FileNotFoundException("Campaign file not found.", _campaignFile);
        }

        var json = File.ReadAllText(_campaignFile);
        return JsonSerializer.Deserialize<CampaignState>(json, CatalogJson.Options)
            ?? throw new JsonException("Campaign state is null.");
    }

    public RunSpec? Next()
    {
        var state = Load();
        var mutableStates = state.States.ToList();
        bool dirty = false;

        // 1. Look for an interrupted Running run
        for (int i = 0; i < mutableStates.Count; i++)
        {
            var s = mutableStates[i];
            if (s.Status == RunStatus.Running)
            {
                if (s.Attempts >= MaxAttempts)
                {
                    mutableStates[i] = s with { Status = RunStatus.Failed, LastError = "Exceeded maximum attempts." };
                    dirty = true;
                    continue;
                }

                mutableStates[i] = s with { Attempts = s.Attempts + 1 };
                var updated = state with { States = mutableStates };
                SaveState(updated);
                return state.Runs.Single(r => r.RunId == s.RunId);
            }
        }

        // 2. Look for the first Pending run
        for (int i = 0; i < mutableStates.Count; i++)
        {
            var s = mutableStates[i];
            if (s.Status == RunStatus.Pending)
            {
                if (s.Attempts >= MaxAttempts)
                {
                    mutableStates[i] = s with { Status = RunStatus.Failed, LastError = "Exceeded maximum attempts." };
                    dirty = true;
                    continue;
                }

                mutableStates[i] = s with { Status = RunStatus.Running, Attempts = s.Attempts + 1 };
                var updated = state with { States = mutableStates };
                SaveState(updated);
                return state.Runs.Single(r => r.RunId == s.RunId);
            }
        }

        if (dirty)
        {
            SaveState(state with { States = mutableStates });
        }

        return null;
    }

    public void Complete(string runId, RunResult result)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(result);

        var resultRelPath = Path.Combine("runs", $"{runId}.result.json");
        var resultFullPath = Path.Combine(directory, resultRelPath);

        // Write result file first
        AtomicWrite(resultFullPath, CatalogJson.Write(result));

        // Update state
        var state = Load();
        var index = state.States.ToList().FindIndex(s => s.RunId == runId);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No run with id '{runId}' in campaign.");
        }

        var current = state.States[index];
        if (current.Status == RunStatus.Done)
        {
            return;
        }

        var mutableStates = state.States.ToList();
        mutableStates[index] = current with
        {
            Status = RunStatus.Done,
            ResultFile = resultRelPath,
            LastError = null,
        };

        SaveState(state with { States = mutableStates });
    }

    public void Fail(string runId, string error)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(error);

        var state = Load();
        var index = state.States.ToList().FindIndex(s => s.RunId == runId);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No run with id '{runId}' in campaign.");
        }

        var current = state.States[index];
        var mutableStates = state.States.ToList();
        var newStatus = current.Attempts < MaxAttempts ? RunStatus.Pending : RunStatus.Failed;
        mutableStates[index] = current with
        {
            Status = newStatus,
            LastError = error,
        };

        SaveState(state with { States = mutableStates });
    }

    public void Reset(string runId)
    {
        ArgumentNullException.ThrowIfNull(runId);

        var state = Load();
        var index = state.States.ToList().FindIndex(s => s.RunId == runId);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No run with id '{runId}' in campaign.");
        }

        var current = state.States[index];
        var mutableStates = state.States.ToList();
        mutableStates[index] = current with
        {
            Status = RunStatus.Pending,
            Attempts = 0,
            LastError = null,
            ResultFile = null,
        };

        SaveState(state with { States = mutableStates });
    }

    private void SaveState(CampaignState state)
    {
        AtomicWrite(_campaignFile, CatalogJson.Write(state));
    }

    private static void AtomicWrite(string targetPath, string content)
    {
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmpPath = targetPath + ".tmp";
        File.WriteAllText(tmpPath, content);
        File.Move(tmpPath, targetPath, overwrite: true);
    }
}
