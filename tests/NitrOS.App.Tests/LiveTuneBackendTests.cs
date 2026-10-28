using NitrOS.App.Logic.Running;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.App.Tests;

[CollectionDefinition("TuneLock")]
public sealed class TuneLockCollectionDefinition { }

[Collection("TuneLock")]
public sealed class LiveTuneBackendTests
{
    [Fact]
    public async Task LiveTuneBackend_AppliesWithFakeSystems()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromIds(repo, ["consumer-features-off"], TargetMode.TunePc, DeviceProfile.Desktop);

        var tempDir = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = new JournalStore(tempDir);
            var f = new FakeSystems();
            var backend = new LiveTuneBackend(() => f.ToLive(), store);

            var options = new TuneOptions(false, "S-1-5-21-1", "26100.1");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            TuneResult result;
            while (true)
            {
                result = backend.Apply(sel, options, null, TestContext.Current.CancellationToken);
                if (result.Refused && result.RefusedReason == TuneEngine.BusyReason && sw.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                    continue;
                }
                break;
            }

            Assert.False(result.Refused, $"Tune was refused: {result.RefusedReason}");
            Assert.True(result.Switches.ContainsKey("consumer-features-off"));
            Assert.Equal(SwitchResult.Applied, result.Switches["consumer-features-off"]);
            Assert.True(File.Exists(result.JournalPath));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
