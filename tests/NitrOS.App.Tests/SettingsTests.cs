using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Live.Launch;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class SettingsTests
{
    private sealed class FakeAfterUpdateTask : IAfterUpdateTask
    {
        private bool _registered;
        public string? RegisteredExePath { get; private set; }
        public string? RegisteredUserSid { get; private set; }

        public bool IsRegistered() => _registered;

        public void Register(string exePath, string userSid)
        {
            _registered = true;
            RegisteredExePath = exePath;
            RegisteredUserSid = userSid;
        }

        public void Remove()
        {
            _registered = false;
        }
    }

    [Fact]
    public async Task Settings_OffByDefaultAndRegistersOnlyOnToggle()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var session = new FakeSession();
        var appStore = new AppStateStore(temp, _ => FolderState.Secure, _ => Directory.CreateDirectory(temp), session);
        var task = new FakeAfterUpdateTask();
        var nav = new Navigator(new RunGate());

        try
        {
            var vm = new SettingsViewModel(task, appStore, session, @"C:\app\nitros.exe", nav);

            // Off by default
            Assert.False(vm.CheckAfterUpdates);
            Assert.False(task.IsRegistered());
            Assert.False(appStore.Read().CheckAfterUpdates);

            // Toggle ON
            await vm.ToggleCheckAfterUpdates.ExecuteAsync(null);
            Assert.True(vm.CheckAfterUpdates);
            Assert.True(task.IsRegistered());
            Assert.True(appStore.Read().CheckAfterUpdates);
            Assert.Equal(@"C:\app\nitros.exe", task.RegisteredExePath);

            // Toggle OFF
            await vm.ToggleCheckAfterUpdates.ExecuteAsync(null);
            Assert.False(vm.CheckAfterUpdates);
            Assert.False(task.IsRegistered());
            Assert.False(appStore.Read().CheckAfterUpdates);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
