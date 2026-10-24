using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.Json;

public class CatalogJsonTests
{
    private static readonly SwitchAction[] AllActions =
    [
        new RegistrySet("HKLM", @"SOFTWARE\Test", "Value", RegistryKind.ExpandString, "x"),
        new ServiceStart("DiagTrack", ServiceStartType.AutomaticDelayed),
        new TaskDisable(@"\Microsoft\Windows\Test"),
        new AppxRemove("Microsoft.Test"),
        new CapabilityRemove("Test.Capability~~~~0.0.1.0"),
        new FeatureDisable("TestFeature"),
        new PackageRemove("Package_for_Test~*"),
        new FileRemove(@"C:\Test\file.txt"),
        new PowerSet("SUB_SLEEP", "STANDBYIDLE", 0, null),
        new Script("Write-Host hi", "Write-Host undo"),
    ];

    [Fact]
    public void RoundTrip_PreservesEveryActionType()
    {
        var sw = Switches.Make("everything", b =>
        {
            b.Image = AllActions;
            b.Modes = [Mode.Live, Mode.Image];
            b.Requires = ["other"];
            b.Breaks = ["thing"];
            b.SkipOn = [DeviceTrait.Battery, DeviceTrait.Handheld];
            b.Measured = new Measurement(120, 3, "26100", new DateOnly(2026, 10, 24));
        });
        var group = new GroupFile("g", "Group", [sw]);

        var back = CatalogJson.ParseGroup(CatalogJson.Write(group));

        Assert.Equal(group.Group, back.Group);
        Assert.Equal(group.Title, back.Title);
        var a = group.Switches.Single();
        var b2 = back.Switches.Single();
        Assert.Equal(a.Id, b2.Id);
        Assert.Equal(a.Name, b2.Name);
        Assert.Equal(a.Feature, b2.Feature);
        Assert.Equal(a.Description, b2.Description);
        Assert.Equal(a.Risk, b2.Risk);
        Assert.Equal(a.Reversible, b2.Reversible);
        Assert.Equal(a.Cards, b2.Cards);
        Assert.Equal(a.Measured, b2.Measured);
        Assert.Equal(a.Modes, b2.Modes);
        Assert.Equal(a.Requires, b2.Requires);
        Assert.Equal(a.Breaks, b2.Breaks);
        Assert.Equal(a.SkipOn, b2.SkipOn);
        Assert.Equal(a.Live.Count, b2.Live.Count);
        for (int i = 0; i < a.Live.Count; i++) Assert.Equal(a.Live[i], b2.Live[i]);
        Assert.Equal(10, b2.Image.Count);
        for (int i = 0; i < a.Image.Count; i++) Assert.Equal(a.Image[i], b2.Image[i]);
    }

    [Fact]
    public void Enums_UseKebabCase()
    {
        var json = CatalogJson.Write<SwitchAction>(new ServiceStart("DiagTrack", ServiceStartType.AutomaticDelayed));
        Assert.Contains("\"automatic-delayed\"", json);

        var sw = Switches.Make("s", b => b.Cards = new CardDefaults
        {
            Recommended = CardDefault.Off,
            Gamer = CardDefault.Off,
            Streamer = CardDefault.Off,
            MaxLean = CardDefault.On,
        });
        Assert.Contains("\"maxLean\": \"on\"", CatalogJson.Write(sw));
    }

    [Fact]
    public void Parse_AcceptsTypeAfterOtherProperties()
    {
        var action = JsonSerializer.Deserialize<SwitchAction>(
            """{"service":"DiagTrack","start":"disabled","type":"service.start"}""", CatalogJson.Options);
        Assert.Equal(new ServiceStart("DiagTrack", ServiceStartType.Disabled), action);
    }

    private static string GroupJson(string extraSwitchProp = "", string cards =
        """{"recommended":"off","gamer":"off","streamer":"off","maxLean":"off"}""") =>
        $$"""
        {"group":"g","title":"G","switches":[{
          "id":"s","name":"S","feature":"F","description":"D","risk":"safe",
          "modes":["live"],"reversible":true,"cards":{{cards}}{{extraSwitchProp}}
        }]}
        """;

    [Fact]
    public void Parse_RejectsUnknownProperty() =>
        Assert.Throws<JsonException>(() => CatalogJson.ParseGroup(GroupJson(",\"colour\":1")));

    [Fact]
    public void Parse_RejectsMissingCard() =>
        Assert.Throws<JsonException>(() => CatalogJson.ParseGroup(GroupJson(
            cards: """{"recommended":"off","gamer":"off","streamer":"off"}""")));

    [Fact]
    public void Parse_AcceptsMinimalValidGroup()
    {
        var g = CatalogJson.ParseGroup(GroupJson());
        Assert.Empty(g.Switches.Single().Live);
    }

    [Fact]
    public void Parse_RejectsActionMissingField() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwitchAction>(
            """{"type":"registry.set","hive":"HKLM"}""", CatalogJson.Options));

    [Fact]
    public void Parse_RejectsGroupMissingSwitches() =>
        Assert.Throws<JsonException>(() => CatalogJson.ParseGroup("""{"group":"g","title":"t"}"""));

    [Fact]
    public void Parse_AllowsOmittedOptionalActionFields()
    {
        var p = JsonSerializer.Deserialize<SwitchAction>(
            """{"type":"power.set","subgroup":"a","setting":"b"}""", CatalogJson.Options);
        Assert.Equal(new PowerSet("a", "b", null, null), p);
        var s = JsonSerializer.Deserialize<SwitchAction>("""{"type":"script","body":"x"}""", CatalogJson.Options);
        Assert.Equal(new Script("x", null), s);
    }

    [Theory]
    [InlineData("\"risk\":\"safe\"", "\"risk\":2")]
    [InlineData("\"risk\":\"safe\"", "\"risk\":\"2\"")]
    [InlineData("\"modes\":[\"live\"]", "\"modes\":[1]")]
    [InlineData("\"gamer\":\"off\"", "\"gamer\":1")]
    public void Parse_RejectsNumbersForEnums(string from, string to) =>
        Assert.Throws<JsonException>(() => CatalogJson.ParseGroup(GroupJson().Replace(from, to)));

    [Theory]
    [InlineData("""{"type":"registry.set","hive":"HKLM","key":"k","name":"n","kind":99,"data":"1"}""")]
    [InlineData("""{"type":"service.start","service":"s","start":42}""")]
    public void Parse_RejectsNumbersForActionEnums(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwitchAction>(json, CatalogJson.Options));
}
