using System.Text;
using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Measure.Campaign;

namespace NitrOS.Measure.Results;

public static class CatalogMeasurementWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private record SwitchSpan(
        string Id,
        int ClosingBraceOffset,
        (int ValStart, int ValEnd)? MeasuredSpan);

    public static void Write(
        string catalogDir,
        IReadOnlyDictionary<string, Measurement> switches,
        CardMeasurements cards)
    {
        ArgumentNullException.ThrowIfNull(catalogDir);
        ArgumentNullException.ThrowIfNull(switches);
        ArgumentNullException.ThrowIfNull(cards);

        // 1. Initial load to check unknown ids
        var initial = CatalogLoader.Load(catalogDir);
        if (initial.Catalog is null)
        {
            throw new InvalidOperationException("Initial catalog load failed: " + string.Join("; ", initial.Errors));
        }

        var knownIds = initial.Catalog.Switches.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in switches.Keys)
        {
            if (!knownIds.Contains(id))
            {
                throw new KeyNotFoundException($"Unknown switch id '{id}'.");
            }
        }

        // 2. Stage changes
        var stagingDir = Path.Combine(Path.GetTempPath(), "nitros-catalog-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);

        try
        {
            foreach (var file in Directory.GetFiles(catalogDir, "*.json"))
            {
                File.Copy(file, Path.Combine(stagingDir, Path.GetFileName(file)), overwrite: true);
            }

            // Write cards.json
            File.WriteAllText(Path.Combine(stagingDir, "cards.json"), CatalogJson.Write(cards), Utf8NoBom);

            // Write group files
            foreach (var groupFile in Directory.GetFiles(stagingDir, "*.json"))
            {
                if (Path.GetFileName(groupFile).Equals("cards.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var bytes = File.ReadAllBytes(groupFile);
                var switchSpans = FindSwitchSpans(bytes);

                // Collect edits for this file
                var edits = new List<(int Start, int End, string Replacement)>();

                foreach (var span in switchSpans)
                {
                    if (!switches.TryGetValue(span.Id, out var m))
                    {
                        continue;
                    }

                    var measuredValJson = $"{{ \"memoryMb\": {m.MemoryMb}, \"processes\": {m.Processes}, \"windowsBuild\": \"{m.WindowsBuild}\", \"date\": \"{m.Date:yyyy-MM-dd}\" }}";

                    if (span.MeasuredSpan.HasValue)
                    {
                        edits.Add((span.MeasuredSpan.Value.ValStart, span.MeasuredSpan.Value.ValEnd, measuredValJson));
                    }
                    else
                    {
                        var insertion = $", \"measured\": {measuredValJson}";
                        edits.Add((span.ClosingBraceOffset, span.ClosingBraceOffset, insertion));
                    }
                }

                if (edits.Count > 0)
                {
                    // Apply edits in reverse order so offsets stay stable
                    edits.Sort((a, b) => b.Start.CompareTo(a.Start));

                    var sb = new StringBuilder(Encoding.UTF8.GetString(bytes));
                    foreach (var (start, end, rep) in edits)
                    {
                        sb.Remove(start, end - start);
                        sb.Insert(start, rep);
                    }

                    File.WriteAllText(groupFile, sb.ToString(), Utf8NoBom);
                }
            }

            // 3. Strict verification of staged catalog
            var stagedResult = CatalogLoader.Load(stagingDir);
            if (stagedResult.Errors.Count > 0 || stagedResult.Catalog is null)
            {
                throw new InvalidOperationException("Staged catalog verification failed: " + string.Join("; ", stagedResult.Errors));
            }

            // Verify every switch equals the original except Measured
            foreach (var stagedSwitch in stagedResult.Catalog.Switches)
            {
                var orig = initial.Catalog.Get(stagedSwitch.Id);
                if (DefinitionHash.Of(stagedSwitch) != DefinitionHash.Of(orig))
                {
                    throw new InvalidOperationException($"Switch '{stagedSwitch.Id}' has modifications outside Measured.");
                }
            }

            // 4. Overwrite originals
            foreach (var stagedFile in Directory.GetFiles(stagingDir, "*.json"))
            {
                var destFile = Path.Combine(catalogDir, Path.GetFileName(stagedFile));
                File.Copy(stagedFile, destFile, overwrite: true);
            }
        }
        finally
        {
            if (Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, recursive: true); } catch { }
            }
        }
    }

    private static List<SwitchSpan> FindSwitchSpans(byte[] jsonBytes)
    {
        var spans = new List<SwitchSpan>();
        var reader = new Utf8JsonReader(jsonBytes);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("switches"u8))
            {
                reader.Read(); // StartArray
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        string? switchId = null;
                        (int ValStart, int ValEnd)? measuredSpan = null;

                        int depth = 1;
                        while (depth > 0 && reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                depth++;
                            }
                            else if (reader.TokenType == JsonTokenType.EndObject)
                            {
                                depth--;
                                if (depth == 0)
                                {
                                    int closingOffset = (int)reader.TokenStartIndex;
                                    if (switchId is not null)
                                    {
                                        spans.Add(new SwitchSpan(switchId, closingOffset, measuredSpan));
                                    }
                                    break;
                                }
                            }
                            else if (depth == 1 && reader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (reader.ValueTextEquals("id"u8))
                                {
                                    reader.Read();
                                    switchId = reader.GetString();
                                }
                                else if (reader.ValueTextEquals("measured"u8))
                                {
                                    reader.Read(); // StartObject
                                    int valStart = (int)reader.TokenStartIndex;
                                    reader.Skip(); // Skips measured object, now at EndObject
                                    int valEnd = (int)reader.BytesConsumed;
                                    measuredSpan = (valStart, valEnd);
                                }
                                else
                                {
                                    reader.Skip();
                                }
                            }
                        }
                    }
                }
            }
        }

        return spans;
    }
}
