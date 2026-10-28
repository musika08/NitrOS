namespace NitrOS.Image.Building;

using System;
using System.IO;
using System.Text.Json;
using NitrOS.Core.Json;

public sealed class BuildStore
{
    public const string FileName = "build.json";

    private readonly string _workFolder;
    private readonly string _stateFilePath;

    public BuildStore(string workFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workFolder);
        _workFolder = workFolder;
        _stateFilePath = Path.Combine(workFolder, FileName);
    }

    public void Save(BuildState s)
    {
        ArgumentNullException.ThrowIfNull(s);

        Directory.CreateDirectory(_workFolder);

        var tmpPath = Path.Combine(_workFolder, $"{FileName}.tmp");
        var json = JsonSerializer.Serialize(s, CatalogJson.Options);

        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _stateFilePath, overwrite: true);
    }

    public BuildState? TryLoad()
    {
        if (!File.Exists(_stateFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            return JsonSerializer.Deserialize<BuildState>(json, CatalogJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public BuildState Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            throw new FileNotFoundException($"Build state file not found: {_stateFilePath}", _stateFilePath);
        }

        var json = File.ReadAllText(_stateFilePath);
        return JsonSerializer.Deserialize<BuildState>(json, CatalogJson.Options)
            ?? throw new InvalidOperationException($"Build state file parsed as null: {_stateFilePath}");
    }
}
