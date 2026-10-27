using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Measure.Guest;
using NitrOS.Measure.Probe;

namespace NitrOS.Measure.Tests.Guest;

public class ProbeTests : IDisposable
{
    private readonly string _tempFile;

    public ProbeTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "probe-test-" + Guid.NewGuid().ToString("N") + ".json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    private sealed class FakeKvpWriter : IKvpWriter
    {
        public List<(string Key, string Value)> Writes { get; } = [];
        public void Set(string key, string value) => Writes.Add((key, value));
    }

    private sealed class FakeProbeGuestFacts : IGuestFacts
    {
        public (long TotalPhys, long AvailPhys) Memory() => (16L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024);
        public IReadOnlyList<(int Pid, string Name)> Processes() => [(1, "svchost"), (2, "explorer")];
        public TimeSpan UnbiasedTime() => TimeSpan.FromMinutes(10);
        public DateTimeOffset Now() => DateTimeOffset.UtcNow;
        public string WindowsBuild() => "26100.1742";
        public int OwnPid => 12345;
    }

    [Fact]
    public void Probe_SampleWritesJsonAndKvpStates()
    {
        var kvp = new FakeKvpWriter();
        var facts = new FakeProbeGuestFacts();

        var exit = ProbeApp.Run(
            ["sample", "--out", _tempFile, "--idle-minutes", "0", "--count", "1", "--interval-seconds", "0", "--kvp"],
            facts,
            kvp,
            _ => { });

        Assert.Equal(0, exit);
        Assert.True(File.Exists(_tempFile));

        var json = File.ReadAllText(_tempFile);
        var run = JsonSerializer.Deserialize<SampleRun>(json, CatalogJson.Options);
        Assert.NotNull(run);
        Assert.Single(run.Samples);
        Assert.Equal("26100.1742", run.WindowsBuild);

        // KVP states should transition through idle, sampling, done
        var states = kvp.Writes.Where(w => w.Key == "NitrOS.Measure.State").Select(w => w.Value).ToList();
        Assert.Contains("sampling", states);
        Assert.Equal("done", states.Last());
    }

    [Fact]
    public void Probe_BadArgumentsExit2()
    {
        var exit = ProbeApp.Run(
            ["unknown-command"],
            new FakeProbeGuestFacts(),
            new FakeKvpWriter(),
            _ => { });

        Assert.Equal(2, exit);
    }
}
