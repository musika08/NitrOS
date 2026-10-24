using System.Text.Json.Serialization;

namespace NitrOS.Core.Model;

public record CardMeasurements(
    Measurement? Recommended = null,
    Measurement? Gamer = null,
    Measurement? Streamer = null,
    Measurement? MaxLean = null)
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
    private IReadOnlyList<GroupFile> _groups = Groups;
    private IReadOnlyList<SwitchDef>? _switches;

    /// <summary>Replacing the groups (for example with a <c>with</c> expression) drops the cached switch list.</summary>
    public IReadOnlyList<GroupFile> Groups
    {
        get => _groups;
        init
        {
            _groups = value;
            _switches = null;
        }
    }

    /// <summary>All switches in file order, flattened once and cached.</summary>
    [JsonIgnore]
    public IReadOnlyList<SwitchDef> Switches => _switches ??= _groups.SelectMany(g => g.Switches).ToList();

    public SwitchDef Get(string id) =>
        Switches.FirstOrDefault(s => s.Id == id)
        ?? throw new KeyNotFoundException($"No switch with id '{id}'.");

    // The cache field must not take part in equality, so compare the data members explicitly.
    public virtual bool Equals(Catalog? other) =>
        other is not null && EqualityComparer<IReadOnlyList<GroupFile>>.Default.Equals(_groups, other._groups)
        && EqualityComparer<CardMeasurements>.Default.Equals(Cards, other.Cards);

    public override int GetHashCode() => HashCode.Combine(_groups, Cards);
}
