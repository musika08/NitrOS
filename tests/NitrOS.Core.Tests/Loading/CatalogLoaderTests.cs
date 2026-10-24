using NitrOS.Core.Json;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.Loading;

public sealed class CatalogLoaderTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "nitros-loader-" + Guid.NewGuid().ToString("N"));

    public CatalogLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    private void WriteCards() =>
        Write("cards.json", CatalogJson.Write(new CardMeasurements(null, null, null, null)));

    private void WriteGroup(string file, string group, params SwitchDef[] switches) =>
        Write(file, CatalogJson.Write(new GroupFile(group, group.ToUpperInvariant(), switches)));

    [Fact]
    public void Load_ValidDirectory_ReturnsCatalog()
    {
        WriteCards();
        WriteGroup("a.json", "a", Switches.Make("a.one"));

        var result = CatalogLoader.Load(_dir);

        Assert.True(result.Ok);
        Assert.NotNull(result.Catalog);
        Assert.Single(result.Catalog!.Switches);
        Assert.Equal("a", result.Catalog.Switches[0].Group);
    }

    [Fact]
    public void Load_ReportsBrokenJsonWithFileAndLine()
    {
        WriteCards();
        WriteGroup("a.json", "a", Switches.Make("a.one"));
        Write("b.json", "{\"group\":\"b\",\"title\":\"B\",\"switches\":[]}\n\n}");

        var result = CatalogLoader.Load(_dir);

        Assert.False(result.Ok);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Errors, e => e.File == "b.json" && e.Line == 3);
        Assert.DoesNotContain(result.Errors, e => e.File == "a.json");
    }

    [Fact]
    public void Load_ReportsUnknownActionType()
    {
        WriteCards();
        WriteGroup("a.json", "a", Switches.Make("a.one"));
        var json = File.ReadAllText(Path.Combine(_dir, "a.json")).Replace("registry.set", "registry.nuke");
        Write("a.json", json);

        var result = CatalogLoader.Load(_dir);

        Assert.Contains("registry.nuke", json);
        var error = Assert.Single(result.Errors);
        Assert.Equal("a.json", error.File);
    }

    [Fact]
    public void Load_MissingCardsFile_IsError()
    {
        WriteGroup("a.json", "a", Switches.Make("a.one"));

        var result = CatalogLoader.Load(_dir);

        Assert.Contains(result.Errors, e => e.File == "cards.json");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void Load_MissingDirectory_IsError()
    {
        var missing = Path.Combine(_dir, "nope");

        var result = CatalogLoader.Load(missing);

        var error = Assert.Single(result.Errors);
        Assert.Equal(missing, error.File);
        Assert.Null(result.Catalog);
    }
}
