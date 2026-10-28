namespace NitrOS.Image.Tests.Handlers;

using System.Collections.Generic;
using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Handlers;
using NitrOS.Image.Offline;
using NitrOS.Image.Systems;
using NitrOS.Image.Tests.Fakes;
using NitrOS.Live.Systems;

public sealed class RegistryHandlersTests
{
    [Fact]
    public void Registry_RefusedKeyIsDeferredNotWritten()
    {
        var fakes = new FakeImageSystems();
        var softwareHive = (FakeOfflineRegistry.FakeHive)fakes.Registry.Load("C:\\SOFTWARE", "NitrOS.Image.SOFTWARE");
        fakes.Registry.RefuseWrite.Add("Microsoft\\Windows\\CurrentVersion\\SearchSettings");

        var hives = new Dictionary<OfflineHive, IOfflineHive>
        {
            [OfflineHive.Software] = softwareHive
        };

        var firstBoot = new FirstBootPlan();
        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, firstBoot, "sw1")
        {
            Hives = hives
        };

        var action = new RegistrySet("HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\SearchSettings", "SafeSearchMode", RegistryKind.Dword, "0");
        var handler = ImageHandlers.For(action);
        var result = handler.Apply(action, ctx);

        Assert.Equal(ImageOutcome.Deferred, result.Outcome);
        Assert.Single(firstBoot.Machine);
        Assert.IsType<MachineRegSet>(firstBoot.Machine[0]);
        Assert.Null(softwareHive.GetValue("Microsoft\\Windows\\CurrentVersion\\SearchSettings", "SafeSearchMode"));
    }

    [Fact]
    public void Registry_ControlSetMapping()
    {
        var fakes = new FakeImageSystems();
        var systemHive = (FakeOfflineRegistry.FakeHive)fakes.Registry.Load("C:\\SYSTEM", "NitrOS.Image.SYSTEM");

        var hives = new Dictionary<OfflineHive, IOfflineHive>
        {
            [OfflineHive.System] = systemHive
        };

        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "svchost-grouping-on")
        {
            Hives = hives
        };

        var action = new RegistrySet("HKLM", "SYSTEM\\CurrentControlSet\\Control", "SvcHostSplitThresholdInKB", RegistryKind.Dword, "380000");
        var handler = ImageHandlers.For(action);
        var result = handler.Apply(action, ctx);

        Assert.Equal(ImageOutcome.Applied, result.Outcome);
        var val = systemHive.GetValue("ControlSet001\\Control", "SvcHostSplitThresholdInKB");
        Assert.NotNull(val);
        Assert.Equal("380000", val.Data);
    }

    [Fact]
    public void Service_SetsStartAndDelayed()
    {
        var fakes = new FakeImageSystems();
        var systemHive = (FakeOfflineRegistry.FakeHive)fakes.Registry.Load("C:\\SYSTEM", "NitrOS.Image.SYSTEM");
        // Ensure service key exists
        systemHive.SetValue("ControlSet001\\Services\\DiagTrack", "DisplayName", new RegValue(RegistryKind.String, "Diagnostics Tracking"));

        var hives = new Dictionary<OfflineHive, IOfflineHive>
        {
            [OfflineHive.System] = systemHive
        };

        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1")
        {
            Hives = hives
        };

        var action = new ServiceStart("DiagTrack", ServiceStartType.AutomaticDelayed);
        var handler = ImageHandlers.For(action);
        var result = handler.Apply(action, ctx);

        Assert.Equal(ImageOutcome.Applied, result.Outcome);
        Assert.Equal("2", systemHive.GetValue("ControlSet001\\Services\\DiagTrack", "Start")?.Data);
        Assert.Equal("1", systemHive.GetValue("ControlSet001\\Services\\DiagTrack", "DelayedAutostart")?.Data);
    }

    [Fact]
    public void Startup_RunWritesDisabledBytesAndLeavesOtherFirstBytes()
    {
        var fakes = new FakeImageSystems();
        var softwareHive = (FakeOfflineRegistry.FakeHive)fakes.Registry.Load("C:\\SOFTWARE", "NitrOS.Image.SOFTWARE");

        softwareHive.SetValue("Microsoft\\Windows\\CurrentVersion\\Run", "OneDrive", new RegValue(RegistryKind.String, "onedrive.exe"));
        softwareHive.SetValue("Microsoft\\Windows\\CurrentVersion\\Run", "UntouchedApp", new RegValue(RegistryKind.String, "app.exe"));

        // Pre-existing 06 entry for UntouchedApp
        softwareHive.SetValue("Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run", "UntouchedApp", new RegValue(RegistryKind.Binary, "060000000000000000000000"));

        var hives = new Dictionary<OfflineHive, IOfflineHive>
        {
            [OfflineHive.Software] = softwareHive
        };

        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1")
        {
            Hives = hives
        };

        var action = new StartupDisable(StartupSource.Run, "OneDrive");
        var handler = ImageHandlers.For(action);
        var result = handler.Apply(action, ctx);

        Assert.Equal(ImageOutcome.Applied, result.Outcome);
        var oneDriveVal = softwareHive.GetValue("Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run", "OneDrive");
        Assert.NotNull(oneDriveVal);
        Assert.StartsWith("03", oneDriveVal.Data);

        // UntouchedApp remains 06
        var untouchedVal = softwareHive.GetValue("Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run", "UntouchedApp");
        Assert.NotNull(untouchedVal);
        Assert.StartsWith("06", untouchedVal.Data);
    }
}
