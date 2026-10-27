using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Measure.Results;

namespace NitrOS.Measure.Tests.Results;

public class CatalogMeasurementWriterTests : IDisposable
{
    private readonly string _dir;
    private readonly string _fixtureDir;

    public CatalogMeasurementWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "nitros-writer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);

        var baseDir = AppContext.BaseDirectory;
        _fixtureDir = Path.Combine(baseDir, "Results", "Fixtures");
        if (!Directory.Exists(_fixtureDir))
        {
            // Try repo source path
            var repoRoot = new DirectoryInfo(baseDir);
            while (repoRoot is not null && !File.Exists(Path.Combine(repoRoot.FullName, "global.json")))
            {
                repoRoot = repoRoot.Parent;
            }
            _fixtureDir = Path.Combine(repoRoot!.FullName, "tests", "NitrOS.Measure.Tests", "Results", "Fixtures");
        }

        foreach (var file in Directory.GetFiles(_fixtureDir, "*.json"))
        {
            File.Copy(file, Path.Combine(_dir, Path.GetFileName(file)), overwrite: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Writer_InsertsMeasuredIntoSwitchWithout()
    {
        var measurements = new Dictionary<string, Measurement>
        {
            ["switch-a"] = new Measurement(150, 4, "26100.1742", new DateOnly(2026, 10, 28))
        };
        var cards = new CardMeasurements();

        CatalogMeasurementWriter.Write(_dir, measurements, cards);

        var loaded = CatalogLoader.Load(_dir).Catalog!;
        var switchA = loaded.Get("switch-a");
        Assert.NotNull(switchA.Measured);
        Assert.Equal(150, switchA.Measured.MemoryMb);
        Assert.Equal(4, switchA.Measured.Processes);
        Assert.Equal("26100.1742", switchA.Measured.WindowsBuild);
    }

    [Fact]
    public void Writer_ReplacesExistingMeasured()
    {
        var measurements = new Dictionary<string, Measurement>
        {
            ["switch-b"] = new Measurement(250, 10, "26100.1742", new DateOnly(2026, 10, 28))
        };
        var cards = new CardMeasurements();

        CatalogMeasurementWriter.Write(_dir, measurements, cards);

        var loaded = CatalogLoader.Load(_dir).Catalog!;
        var switchB = loaded.Get("switch-b");
        Assert.NotNull(switchB.Measured);
        Assert.Equal(250, switchB.Measured.MemoryMb);
        Assert.Equal(10, switchB.Measured.Processes);
    }

    [Fact]
    public void Writer_LeavesEveryOtherByteUnchanged()
    {
        var origBytes = File.ReadAllBytes(Path.Combine(_dir, "test-group.json"));
        var origText = System.Text.Encoding.UTF8.GetString(origBytes);

        var measurements = new Dictionary<string, Measurement>
        {
            ["switch-b"] = new Measurement(999, 99, "26100.1742", new DateOnly(2026, 10, 28))
        };
        var cards = new CardMeasurements();

        CatalogMeasurementWriter.Write(_dir, measurements, cards);

        var newBytes = File.ReadAllBytes(Path.Combine(_dir, "test-group.json"));
        var newText = System.Text.Encoding.UTF8.GetString(newBytes);

        // Find the "measured" span in both
        var origPrefix = origText.Substring(0, origText.IndexOf("\"measured\":"));
        var newPrefix = newText.Substring(0, newText.IndexOf("\"measured\":"));
        Assert.Equal(origPrefix, newPrefix);

        var origSuffix = origText.Substring(origText.IndexOf("}\n  ]") - 1);
        var newSuffix = newText.Substring(newText.IndexOf("}\n  ]") - 1);
        Assert.Equal(origSuffix, newSuffix);
    }

    [Fact]
    public void Writer_WritesCardsJsonThatLoads()
    {
        var m = new Measurement(500, 20, "26100.1742", new DateOnly(2026, 10, 28));
        var cards = new CardMeasurements(Recommended: m, Gamer: m, Streamer: m, MaxLean: m);

        CatalogMeasurementWriter.Write(_dir, new Dictionary<string, Measurement>(), cards);

        var loaded = CatalogLoader.Load(_dir).Catalog!;
        Assert.NotNull(loaded.Cards.Recommended);
        Assert.Equal(500, loaded.Cards.Recommended.MemoryMb);
    }

    [Fact]
    public void Writer_RollsBackWhenLoaderFails()
    {
        var origBytes = File.ReadAllBytes(Path.Combine(_dir, "test-group.json"));

        var invalid = new Dictionary<string, Measurement>
        {
            ["switch-a"] = new Measurement(100, 2, "", new DateOnly(2026, 10, 28)) // empty build makes loader fail
        };

        Assert.ThrowsAny<Exception>(() => CatalogMeasurementWriter.Write(_dir, invalid, new CardMeasurements()));

        var afterBytes = File.ReadAllBytes(Path.Combine(_dir, "test-group.json"));
        Assert.Equal(origBytes, afterBytes);
    }

    [Fact]
    public void Writer_UnknownIdThrowsBeforeWriting()
    {
        var origBytes = File.ReadAllBytes(Path.Combine(_dir, "test-group.json"));

        var unknown = new Dictionary<string, Measurement>
        {
            ["non-existent-switch"] = new Measurement(100, 2, "26100.1", new DateOnly(2026, 10, 28))
        };

        Assert.Throws<KeyNotFoundException>(() => CatalogMeasurementWriter.Write(_dir, unknown, new CardMeasurements()));

        var afterBytes = File.ReadAllBytes(Path.Combine(_dir, "test-group.json"));
        Assert.Equal(origBytes, afterBytes);
    }

    [Fact]
    public void Writer_RealCatalogCopyStillLoads()
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = new DirectoryInfo(baseDir);
        while (repoRoot is not null && !File.Exists(Path.Combine(repoRoot.FullName, "global.json")))
        {
            repoRoot = repoRoot.Parent;
        }
        var realCatalogDir = Path.Combine(repoRoot!.FullName, "catalog");

        var tempCatalog = Path.Combine(Path.GetTempPath(), "real-cat-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempCatalog);

        try
        {
            foreach (var file in Directory.GetFiles(realCatalogDir, "*.json"))
            {
                File.Copy(file, Path.Combine(tempCatalog, Path.GetFileName(file)));
            }

            var initial = CatalogLoader.Load(tempCatalog).Catalog!;
            var measurements = new Dictionary<string, Measurement>();
            var m = new Measurement(42, 3, "26100.1742", new DateOnly(2026, 10, 28));

            foreach (var s in initial.Switches)
            {
                measurements[s.Id] = m;
            }

            var cards = new CardMeasurements(Recommended: m, Gamer: m, Streamer: m, MaxLean: m);
            CatalogMeasurementWriter.Write(tempCatalog, measurements, cards);

            var strictlyLoaded = CatalogLoader.Load(tempCatalog);
            Assert.Empty(strictlyLoaded.Errors);
            Assert.NotNull(strictlyLoaded.Catalog);
            Assert.Equal(initial.Switches.Count, strictlyLoaded.Catalog.Switches.Count);
            foreach (var s in strictlyLoaded.Catalog.Switches)
            {
                Assert.NotNull(s.Measured);
                Assert.Equal(42, s.Measured.MemoryMb);
            }
        }
        finally
        {
            if (Directory.Exists(tempCatalog))
            {
                try { Directory.Delete(tempCatalog, recursive: true); } catch { }
            }
        }
    }
}
