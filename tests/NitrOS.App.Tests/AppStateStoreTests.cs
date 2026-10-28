using NitrOS.App.Logic;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Live.Measuring;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class AppStateStoreTests
{
    [Fact]
    public void AppStateStore_ReadNeverCreatesFolder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        var session = new FakeSession();

        try
        {
            var store = new AppStateStore(temp, _ => FolderState.Missing, _ => { }, session);
            var state = store.Read();

            Assert.Null(state.Health);
            Assert.False(state.CheckAfterUpdates);
            Assert.Null(state.LastCheckedBuild);
            Assert.False(Directory.Exists(temp));
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void AppStateStore_WriteRefusedWhenNotElevated()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        var nonElevatedSession = new FakeSession(elevated: false);

        try
        {
            var store = new AppStateStore(temp, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp), nonElevatedSession);
            var ex = Assert.Throws<InvalidOperationException>(() => store.SetCheckAfterUpdates(true));
            Assert.Equal(Texts.NotElevated, ex.Message);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void AppStateStore_RoundTrips()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        var session = new FakeSession(elevated: true);

        try
        {
            var store = new AppStateStore(temp, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp), session);

            var hp = new HealthPending("run-123", "S-1-5-21", new MachineSnapshot(4000, 120, DateTimeOffset.Now), DateTimeOffset.Now);
            store.SetHealthPending(hp);

            var s1 = store.Read();
            Assert.NotNull(s1.Health);
            Assert.Equal("run-123", s1.Health.RunId);
            Assert.Equal("S-1-5-21", s1.Health.UserSid);
            Assert.Equal(4000, s1.Health.Before.MemoryInUseMb);

            store.SetCheckAfterUpdates(true);
            var s2 = store.Read();
            Assert.True(s2.CheckAfterUpdates);
            Assert.NotNull(s2.Health);

            store.SetLastCheckedBuild("26100.1000");
            var s3 = store.Read();
            Assert.Equal("26100.1000", s3.LastCheckedBuild);

            store.ClearHealthPending();
            var s4 = store.Read();
            Assert.Null(s4.Health);
            Assert.True(s4.CheckAfterUpdates);
            Assert.Equal("26100.1000", s4.LastCheckedBuild);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
