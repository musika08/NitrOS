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
        return SaveTo(journal, Path.Combine(directory, journal.RunId + Ext));
    }

    /// <summary>Writes the journal to an exact path, atomically like <see cref="Save"/>. Used to update a journal where it was loaded from.</summary>
    public string SaveTo(RunJournal journal, string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(journal, CatalogJson.Options));
        File.Move(tmp, path, overwrite: true);
        return path;
    }

    public RunJournal Load(string path) =>
        JsonSerializer.Deserialize<RunJournal>(File.ReadAllText(path), CatalogJson.Options)
        ?? throw new JsonException("Journal is null.");

    /// <summary>Journal paths, newest first (RunIds are yyyyMMdd-HHmmss with an optional -2, -3 suffix; ordinal order on the name without ".json" is time order, so X-2 is newer than X; last-write time only breaks ties). Leftover .tmp files are ignored.</summary>
    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*" + Ext)
            .OrderByDescending(p => Path.GetFileNameWithoutExtension(p), StringComparer.Ordinal)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }
}
