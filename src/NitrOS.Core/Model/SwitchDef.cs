using System.Text.Json.Serialization;

namespace NitrOS.Core.Model;

public record Measurement(int MemoryMb, int Processes, string WindowsBuild, DateOnly Date);

public record CardDefaults
{
    public required CardDefault Recommended { get; init; }
    public required CardDefault Gamer { get; init; }
    public required CardDefault Streamer { get; init; }
    public required CardDefault MaxLean { get; init; }
}

public record SwitchDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Plain noun for what the switch affects, e.g. "Microsoft Store".</summary>
    public required string Feature { get; init; }
    public required string Description { get; init; }
    public required Risk Risk { get; init; }
    public required IReadOnlyList<Mode> Modes { get; init; }
    public required bool Reversible { get; init; }
    public required CardDefaults Cards { get; init; }

    public IReadOnlyList<string> Breaks { get; init; } = [];
    public IReadOnlyList<string> Requires { get; init; } = [];
    public IReadOnlyList<SwitchAction> Live { get; init; } = [];
    public IReadOnlyList<SwitchAction> Image { get; init; } = [];
    public IReadOnlyList<DeviceTrait> SkipOn { get; init; } = [];
    public Measurement? Measured { get; init; }

    /// <summary>Set by the loader from the owning file.</summary>
    [JsonIgnore]
    public string Group { get; init; } = "";
}

public record GroupFile(string Group, string Title, IReadOnlyList<SwitchDef> Switches);
