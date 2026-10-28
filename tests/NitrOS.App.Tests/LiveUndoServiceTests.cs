using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

[Collection("TuneLock")]
public sealed class LiveUndoServiceTests
{
    [Fact]
    public async Task LiveUndoService_RefusedSessionDoesNothing()
    {
        var badSession = new FakeSession(processSid: "S-1-5-21-1", consoleSid: "S-1-5-21-2");
        var ja = new JournalAccess(@"C:\data", badSession, _ => FolderState.Secure, _ => { });
        var appStore = new AppStateStore(@"C:\app", _ => FolderState.Secure, _ => { }, badSession);
        var f = new FakeSystems();

        var service = new LiveUndoService(badSession, ja, () => f.ToLive(), appStore);
        var result = await service.UndoAsync(@"C:\data\20261028-100000.json", null, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Refused);
        Assert.Equal("NitrOS is running as a different user than the one signed in, so it would change the wrong account's settings.", result.Refused);
        Assert.Empty(result.Undone);
    }

    [Fact]
    public async Task LiveUndoService_UndoesWithFakeSystems()
    {
        var repo = TestCatalog.LoadRepo();
        var sel = Selection.FromIds(repo, ["consumer-features-off"], TargetMode.TunePc, DeviceProfile.Desktop);

        var tempDir = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var journalDir = Path.Combine(tempDir, "backups");
        var appDir = Path.Combine(tempDir, "app");
        Directory.CreateDirectory(journalDir);
        Directory.CreateDirectory(appDir);

        try
        {
            var store = new JournalStore(journalDir);
            var f = new FakeSystems();
            var session = new FakeSession();
            var ja = new JournalAccess(journalDir, session, _ => FolderState.Secure, _ => { });
            var appStore = new AppStateStore(appDir, _ => FolderState.Secure, _ => { }, session);

            var engine = new TuneEngine(f.ToLive(), store);
            var options = new TuneOptions(false, session.ProcessUserSid, "26100.1");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            TuneResult applyResult;
            while (true)
            {
                applyResult = engine.Apply(sel, options, null, TestContext.Current.CancellationToken);
                if (applyResult.Refused && applyResult.RefusedReason == TuneEngine.BusyReason && sw.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                    continue;
                }
                break;
            }

            Assert.False(applyResult.Refused);
            Assert.True(File.Exists(applyResult.JournalPath));

            var service = new LiveUndoService(session, ja, () => f.ToLive(), appStore);

            UndoOutcome undoResult;
            sw.Restart();
            while (true)
            {
                undoResult = await service.UndoAsync(applyResult.JournalPath, null, TestContext.Current.CancellationToken);
                if (undoResult.Refused == TuneEngine.BusyReason && sw.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                    continue;
                }
                break;
            }

            Assert.Null(undoResult.Refused);
            Assert.Contains("consumer-features-off", undoResult.Undone);

            var state = appStore.Read();
            Assert.NotNull(state.Health);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
