using System.Text.Json;
using NitrOS.Core.Json;

namespace NitrOS.Live.Journal;

public sealed class JournalStore(string directory)
{
    private const string Ext = ".json";

    /// <summary>Writes {RunId}.json via a .tmp file and an overwriting move, so a crash never leaves a half-written journal.</summary>
    public string Save(RunJournal journal)
    {
        if (string.IsNullOrWhiteSpace(journal.RunId) || journal.RunId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("RunId is not a valid file name.", nameof(journal));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, journal.RunId + Ext);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(journal, CatalogJson.Options));
        File.Move(tmp, path, overwrite: true);
        return path;
    }

    public RunJournal Load(string path) =>
        JsonSerializer.Deserialize<RunJournal>(File.ReadAllText(path), CatalogJson.Options)
        ?? throw new JsonException("Journal is null.");

    /// <summary>Journal paths, newest first (RunIds are yyyyMMdd-HHmmss, so name order is time order; last-write time only breaks ties). Leftover .tmp files are ignored.</summary>
    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*" + Ext)
            .OrderByDescending(p => Path.GetFileName(p), StringComparer.Ordinal)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }
}
