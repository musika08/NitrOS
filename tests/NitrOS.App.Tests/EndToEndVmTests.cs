using NitrOS.App.Logic;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Startup;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Windows;

namespace NitrOS.App.Tests;

public sealed class EndToEndVmTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    [Fact]
    [Trait("Lane", "Vm")]
    public async Task EndToEnd_RecommendedTuneThenUndoEverythingInVm()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: applies and undoes changes on a real Windows VM");

        var dialogs = new FakeDialogs { NextConfirmResult = true };
        var comp = AppComposition.CreateReal(AppDomain.CurrentDomain.BaseDirectory, dialogs);
        comp.RegisterScreens();

        // 1. Start -> Tune
        comp.Draft.Start(TargetMode.TunePc);

        // 2. Cards -> Pick Recommended
        comp.Draft.PickCard(CardId.Recommended);

        // 3. Review -> Go
        comp.Navigator.GoTo(ScreenKeys.Review, comp.Draft);
        var reviewVm = comp.Navigator.Current as ReviewViewModel;
        Assert.NotNull(reviewVm);
        Assert.True(reviewVm.Go.CanExecute(null));

        await reviewVm.Go.ExecuteAsync(null);

        // Working screen runs
        var workingVm = comp.Navigator.Current as WorkingViewModel;
        Assert.NotNull(workingVm);
        if (workingVm.CurrentRunTask is not null)
        {
            await workingVm.CurrentRunTask;
        }

        // Done screen
        var doneVm = comp.Navigator.Current as DoneViewModel;
        Assert.NotNull(doneVm);
        Assert.DoesNotContain(doneVm.Headline, "Refused");
        Assert.DoesNotContain(doneVm.Headline, "Stopped");

        // 4. Undo screen -> Undo everything
        comp.Navigator.GoTo(ScreenKeys.Undo);
        var undoVm = comp.Navigator.Current as UndoViewModel;
        Assert.NotNull(undoVm);
        Assert.NotEmpty(undoVm.Runs);

        await undoVm.UndoEverything.ExecuteAsync(null);

        // Assert all reversible switches are put back (DriftChecker finds them drifted back to prior values)
        var findings = await comp.DriftService.CheckAsync(CancellationToken.None);
        Assert.NotEmpty(findings.Drifted);
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public async Task EndToEnd_DriftOnOpenFindsTurnedBackSwitchInVm()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: tests drift on open");

        var dialogs = new FakeDialogs { NextConfirmResult = true };
        var comp = AppComposition.CreateReal(AppDomain.CurrentDomain.BaseDirectory, dialogs);
        comp.RegisterScreens();

        // Apply Gamer
        comp.Draft.Start(TargetMode.TunePc);
        comp.Draft.PickCard(CardId.Gamer);

        comp.Navigator.GoTo(ScreenKeys.Review, comp.Draft);
        var reviewVm = comp.Navigator.Current as ReviewViewModel;
        Assert.NotNull(reviewVm);
        await reviewVm.Go.ExecuteAsync(null);

        var workingVm = comp.Navigator.Current as WorkingViewModel;
        Assert.NotNull(workingVm);
        if (workingVm.CurrentRunTask is not null)
        {
            await workingVm.CurrentRunTask;
        }

        // Turn one service back to Automatic outside NitrOS
        var serviceConfig = new WinServiceConfig();
        serviceConfig.Set("Fax", ServiceStartType.Automatic);

        // Run StartupRouter in Normal mode
        var decision = await StartupRouter.DecideAsync(
            LaunchMode.Normal,
            comp.AppContext,
            comp.AppState,
            comp.MachineMeter,
            comp.WindowsVersion,
            comp.DriftService);

        Assert.True(decision.ShowWindow);
        Assert.True(decision.RunDriftInBackground);

        var findings = await comp.DriftService.CheckAsync(CancellationToken.None);
        Assert.Contains(findings.Drifted, id => string.Equals(id, "fax-service-disable", StringComparison.OrdinalIgnoreCase));
    }
}
