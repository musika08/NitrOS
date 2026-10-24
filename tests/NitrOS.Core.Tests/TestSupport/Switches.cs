using NitrOS.Core.Model;

namespace NitrOS.Core.Tests.TestSupport;

public sealed class SwitchDefBuilder
{
    public string Feature { get; set; } = "Test feature";
    public Risk Risk { get; set; } = Risk.Safe;
    public IReadOnlyList<Mode> Modes { get; set; } = [Mode.Live];
    public bool Reversible { get; set; } = true;
    public CardDefaults Cards { get; set; } = new()
    {
        Recommended = CardDefault.Off,
        Gamer = CardDefault.Off,
        Streamer = CardDefault.Off,
        MaxLean = CardDefault.Off,
    };
    public IReadOnlyList<string> Breaks { get; set; } = [];
    public IReadOnlyList<string> Requires { get; set; } = [];
    public IReadOnlyList<SwitchAction> Live { get; set; } =
        [new RegistrySet("HKLM", @"SOFTWARE\Test", "Value", RegistryKind.Dword, "1")];
    public IReadOnlyList<SwitchAction> Image { get; set; } = [];
    public IReadOnlyList<DeviceTrait> SkipOn { get; set; } = [];
    public Measurement? Measured { get; set; }
}

public static class Switches
{
    public static SwitchDef Make(string id, Action<SwitchDefBuilder>? configure = null)
    {
        var b = new SwitchDefBuilder();
        configure?.Invoke(b);
        return new SwitchDef
        {
            Id = id,
            Name = id,
            Feature = b.Feature,
            Description = "Test switch " + id,
            Risk = b.Risk,
            Modes = b.Modes,
            Reversible = b.Reversible,
            Cards = b.Cards,
            Breaks = b.Breaks,
            Requires = b.Requires,
            Live = b.Live,
            Image = b.Image,
            SkipOn = b.SkipOn,
            Measured = b.Measured,
        };
    }

    public static NitrOS.Core.Model.Catalog CatalogOf(params SwitchDef[] switches) =>
        new(
            [new GroupFile("test", "Test", switches.Select(s => s with { Group = "test" }).ToList())],
            new CardMeasurements(null, null, null, null));
}
