using NitrOS.App.Logic;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Tests.Fakes;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Updates.Checking;
using NitrOS.Updates.Installing;
using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;
using Xunit;

namespace NitrOS.App.Tests;

public sealed class DeviceUpdatesViewModelTests
{
    private sealed class LocalFakeRestarter : IRestarter
    {
        public int RestartCalls { get; private set; }
        public void Restart() => RestartCalls++;
    }

    private sealed class LocalFakePower(PowerState initial) : IPowerSource
    {
        public PowerState State { get; set; } = initial;
        public PowerState Read() => State;
    }

    private static (DeviceUpdatesViewModel vm, FakeDeviceUpdates updates, FakeDialogs dialogs, RunGate gate, LocalFakePower power, LocalFakeRestarter restarter)
        CreateVm(CheckReport? report = null, bool elevated = true)
    {
        var power = new LocalFakePower(new PowerState(HasBattery: true, OnBattery: false, BatteryPercent: 95));
        var updates = new FakeDeviceUpdates(power) { ReportToReturn = report };
        var dialogs = new FakeDialogs();
        var gate = new RunGate();
        var probe = new FakeLockProbe();
        var session = new FakeSession(elevated: elevated);
        var restarter = new LocalFakeRestarter();

        var vm = new DeviceUpdatesViewModel(updates, dialogs, gate, probe, session, restarter);
        return (vm, updates, dialogs, gate, power, restarter);
    }

    [Fact]
    public void DeviceUpdates_NothingInstallsUntilInstall()
    {
        var (vm, updates, dialogs, _, _, _) = CreateVm();
        vm.OnShown(null);

        _ = vm.Rows;
        _ = vm.Notices;
        _ = vm.Links;
        _ = vm.PolicyNote;
        _ = vm.LastChecked;

        Assert.Empty(updates.InstallCalls);
        Assert.Empty(updates.StartHandedOffCalls);
        Assert.Empty(updates.OpenPageCalls);
        Assert.Empty(updates.ShowInFolderCalls);
    }

    [Fact]
    public async Task DeviceUpdates_ChecksOnlyOnOpenAndCheck()
    {
        var (vm, updates, _, _, _, _) = CreateVm();
        Assert.Equal(0, updates.CheckCalls);

        vm.OnShown(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, updates.CheckCalls);

        await vm.CheckCommand.ExecuteAsync(null);
        Assert.Equal(2, updates.CheckCalls);
    }

    [Fact]
    public async Task DeviceUpdates_RowsShowStatesAndSources()
    {
        var biosItem = new UpdateItem("fw:1", "BIOS Update", UpdateKind.Bios, "319", "319", null, null, null, "asus", "ASUS");
        var pdItem = new UpdateItem("fw:2", "PD Firmware", UpdateKind.Firmware, "0015", "0020", null, null, null, "asus", "ASUS");
        var wuPdItem = new UpdateItem("fw:2", "PD Firmware", UpdateKind.Firmware, "0015", "10.1.2.20", null, null, null, "windows-update", "Windows Update");

        var report = new CheckReport(
            new DeviceInfo(MakerId.Asus, "ASUS", "Ally", "RC73XA", "319", null, [], []),
            [
                new MergedItem(biosItem, ItemState.UpToDate, null, []),
                new MergedItem(pdItem, ItemState.NewVersion, null, [wuPdItem])
            ],
            [],
            [],
            false,
            DateTimeOffset.UtcNow);

        var (vm, _, _, _, _, _) = CreateVm(report);
        vm.OnShown(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("Up to date", vm.Rows[0].StateText);
        Assert.Equal("New version", vm.Rows[1].StateText);
        Assert.Contains("Windows Update", vm.Rows[1].AlsoOfferedText);
    }

    [Fact]
    public async Task DeviceUpdates_FirmwareInstallAsksWithWarning()
    {
        var pdItem = new UpdateItem("fw:2", "PD Firmware", UpdateKind.Firmware, "0015", "0020", new Uri("https://dlcdnets.asus.com/pd.exe"), null, null, "asus", "ASUS");
        var report = new CheckReport(
            new DeviceInfo(MakerId.Asus, "ASUS", "Ally", "RC73XA", "319", null, [], []),
            [new MergedItem(pdItem, ItemState.NewVersion, null, [])],
            [], [], false, DateTimeOffset.UtcNow);

        var (vm, updates, dialogs, _, _, _) = CreateVm(report);
        dialogs.NextConfirmResult = true;

        vm.OnShown(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        var row = vm.Rows[0];
        Assert.NotNull(row.Install);
        await row.Install.ExecuteAsync(null);

        Assert.Single(updates.InstallCalls);
        Assert.True(updates.InstallCalls[0].UserConfirmedFirmwareWarning);
    }

    [Fact]
    public void RunGate_TryEnterWithMessageSetsBusyMessage()
    {
        var gate = new RunGate();
        Assert.Null(gate.BusyMessage);

        using (var token = gate.TryEnter("Installing update..."))
        {
            Assert.True(gate.IsBusy);
            Assert.Equal("Installing update...", gate.BusyMessage);
        }

        Assert.False(gate.IsBusy);
        Assert.Null(gate.BusyMessage);
    }
}
