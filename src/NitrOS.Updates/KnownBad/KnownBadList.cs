using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NitrOS.Updates.Model;
using NitrOS.Updates.Versions;

namespace NitrOS.Updates.KnownBad;

public sealed record KnownBadEntry(
    MakerId Maker,
    string Model,
    UpdateKind Kind,
    string Version,
    string Reason,
    string Source);

public sealed class KnownBadList(IReadOnlyList<KnownBadEntry> entries)
{
    private readonly IReadOnlyList<KnownBadEntry> _entries = entries ?? Array.Empty<KnownBadEntry>();

    public IReadOnlyList<KnownBadEntry> Entries => _entries;

    public static KnownBadList Shipped()
    {
        var asm = typeof(KnownBadList).Assembly;
        using var stream = asm.GetManifestResourceStream("NitrOS.Updates.KnownBad.known-bad.json")
            ?? throw new InvalidOperationException("Embedded resource 'known-bad.json' not found.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return Parse(json);
    }

    public static KnownBadList Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<KnownBadDoc>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new FormatException("Invalid known-bad JSON.");

        var list = new List<KnownBadEntry>();
        if (doc.Entries != null)
        {
            foreach (var item in doc.Entries)
            {
                var maker = Enum.TryParse<MakerId>(item.Maker, ignoreCase: true, out var m) ? m : MakerId.Other;
                var kind = Enum.TryParse<UpdateKind>(item.Kind, ignoreCase: true, out var k) ? k : UpdateKind.Driver;
                list.Add(new KnownBadEntry(
                    maker,
                    item.Model ?? "*",
                    kind,
                    item.Version ?? string.Empty,
                    item.Reason ?? string.Empty,
                    item.Source ?? string.Empty));
            }
        }

        return new KnownBadList(list);
    }

    public KnownBadEntry? Find(MakerId maker, string? modelCode, UpdateKind kind, string version)
    {
        foreach (var entry in _entries)
        {
            if (entry.Maker != maker)
                continue;

            if (entry.Kind != kind)
                continue;

            if (!string.Equals(entry.Model, "*", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.Model, modelCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cmp = VersionText.Compare(entry.Version, version);
            if (cmp.HasValue)
            {
                if (cmp.Value == 0)
                    return entry;
            }
            else
            {
                if (string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
        }

        return null;
    }

    private sealed class KnownBadDoc
    {
        [JsonPropertyName("entries")]
        public List<RawEntry>? Entries { get; set; }
    }

    private sealed class RawEntry
    {
        public string? Maker { get; set; }
        public string? Model { get; set; }
        public string? Kind { get; set; }
        public string? Version { get; set; }
        public string? Reason { get; set; }
        public string? Source { get; set; }
    }
}
