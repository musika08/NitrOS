using System.Text.Json.Serialization;

namespace NitrOS.Core.Model;

public record CardMeasurements(
    Measurement? Recommended,
    Measurement? Gamer,
    Measurement? Streamer,
    Measurement? MaxLean)
{
    public Measurement? For(CardId id) => id switch
    {
        CardId.Recommended => Recommended,
        CardId.Gamer => Gamer,
        CardId.Streamer => Streamer,
        CardId.MaxLean => MaxLean,
        _ => null,
    };
}

public record Catalog(IReadOnlyList<GroupFile> Groups, CardMeasurements Cards)
{
    [JsonIgnore]
    public IReadOnlyList<SwitchDef> Switches => Groups.SelectMany(g => g.Switches).ToList();

    public SwitchDef Get(string id) =>
        Switches.FirstOrDefault(s => s.Id == id)
        ?? throw new KeyNotFoundException($"No switch with id '{id}'.");
}
