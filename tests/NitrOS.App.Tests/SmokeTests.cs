using NitrOS.App.Logic;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;

namespace NitrOS.App.Tests;

public sealed class SmokeTests
{
    private sealed class DummyDialogs : IDialogs
    {
        public Task<bool> ConfirmAsync(ConfirmRequest request) => Task.FromResult(true);
        public Task ShowAsync(string title, string message) => Task.CompletedTask;
    }

    [Fact]
    public async Task Smoke_CompositionCannotApplyAnything()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros_smoke_" + Guid.NewGuid().ToString("N"));
        try
        {
            var comp = AppComposition.CreateSmoke(AppDomain.CurrentDomain.BaseDirectory, new DummyDialogs(), tempDir);
            comp.RegisterScreens();

            // 1. Both runners refuse CheckReady and throw on RunAsync
            var dummyCatalog = comp.Catalog ?? new Catalog([], new CardMeasurements());
            var dummySelection = Selection.FromCard(dummyCatalog, CardId.Recommended, TargetMode.TunePc, DeviceProfile.Desktop);
            var dummyReq = new RunRequest(
                RunKind.Tune,
                dummySelection,
                CardId.Recommended,
                false,
                null);

            var tuneRunner = comp.Runners.For(RunKind.Tune);
            var tuneReady = tuneRunner.CheckReady(dummyReq);
            Assert.False(tuneReady.Ready);
            Assert.Equal("Smoke test: nothing is applied.", tuneReady.Reason);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                tuneRunner.RunAsync(dummyReq, new Progress<RunProgress>(), CancellationToken.None));

            var isoRunner = comp.Runners.For(RunKind.Iso);
            var isoReady = isoRunner.CheckReady(dummyReq with { Kind = RunKind.Iso });
            Assert.False(isoReady.Ready);
            Assert.Equal("Smoke test: nothing is applied.", isoReady.Reason);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                isoRunner.RunAsync(dummyReq with { Kind = RunKind.Iso }, new Progress<RunProgress>(), CancellationToken.None));

            // 2. Undo service throws
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                comp.UndoService.UndoAsync("any-path", null, CancellationToken.None));

            // 3. Restarter throws
            Assert.Throws<InvalidOperationException>(() => comp.Restarter.Restart());

            // 4. AfterUpdateTask throws
            Assert.Throws<InvalidOperationException>(() => comp.AfterUpdateTask.Register("any.exe", "S-1-5-21"));
            Assert.Throws<InvalidOperationException>(() => comp.AfterUpdateTask.Remove());

            // 5. AppState writes throw
            Assert.Throws<InvalidOperationException>(() => comp.AppState.SetCheckAfterUpdates(true));

            // 6. AfterRestartLaunch doesn't write to real registry
            Assert.False(comp.AfterRestartLaunch.TryRegister("any.exe"));

            // 7. Review.Go.CanExecute is false in both Tune and ISO modes
            // Tune mode
            comp.Draft.Start(TargetMode.TunePc);
            comp.Draft.PickCard(CardId.Recommended);
            var reviewTune = new ReviewViewModel(
                dummyCatalog,
                comp.Draft,
                comp.Runners,
                comp.Session,
                comp.TuneLockProbe,
                comp.RunGate,
                comp.Dialogs,
                comp.Navigator);
            reviewTune.OnShown(comp.Draft);
            Assert.False(reviewTune.Go.CanExecute(null));
            Assert.True(reviewTune.GoUnavailableReason is "Smoke test: nothing is applied." or "NitrOS needs administrator rights.");

            // ISO mode
            comp.Draft.Start(TargetMode.BuildIso);
            comp.Draft.PickCard(CardId.Recommended);
            var reviewIso = new ReviewViewModel(
                dummyCatalog,
                comp.Draft,
                comp.Runners,
                comp.Session,
                comp.TuneLockProbe,
                comp.RunGate,
                comp.Dialogs,
                comp.Navigator);
            reviewIso.OnShown(comp.Draft);
            Assert.False(reviewIso.Go.CanExecute(null));
            Assert.True(reviewIso.GoUnavailableReason is "Smoke test: nothing is applied." or "NitrOS needs administrator rights.");

            // 8. No folders created
            Assert.False(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
