using NitrOS.App.Logic;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Tests.Fakes;

namespace NitrOS.App.Tests;

public sealed class AppCompositionTests
{
    private sealed class DummyDialogs : IDialogs
    {
        public Task<bool> ConfirmAsync(ConfirmRequest request) => Task.FromResult(true);
        public Task ShowAsync(string title, string message) => Task.CompletedTask;
    }

    [Fact]
    public void Composition_RegistersEveryScreenOfThisPlan()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros_comp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var comp = AppComposition.CreateReal(AppDomain.CurrentDomain.BaseDirectory, new DummyDialogs(), tempDir);
            comp.RegisterScreens();

            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Start));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Cards));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Switches));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Review));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Working));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Done));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Undo));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Drift));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Health));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Myths));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Settings));
            Assert.True(comp.Navigator.IsRegistered(ScreenKeys.Blocked));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Composition_DeviceUpdatesNotRegistered()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros_comp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var comp = AppComposition.CreateReal(AppDomain.CurrentDomain.BaseDirectory, new DummyDialogs(), tempDir);
            comp.RegisterScreens();

            Assert.False(comp.Navigator.IsRegistered(ScreenKeys.DeviceUpdates));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Composition_IsoRunnerIsReal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros_comp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var comp = AppComposition.CreateReal(AppDomain.CurrentDomain.BaseDirectory, new DummyDialogs(), tempDir);
            var isoRunner = comp.Runners.For(RunKind.Iso);

            Assert.IsType<IsoRunner>(isoRunner);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Composition_CreateRealCreatesNoFolder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros_comp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var comp = AppComposition.CreateReal(AppDomain.CurrentDomain.BaseDirectory, new DummyDialogs(), tempDir);

            Assert.False(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
