using NitrOS.App.Logic.Ports;
using NitrOS.Live.Systems;
using NitrOS.Updates.Checking;
using NitrOS.Updates.Installing;

namespace NitrOS.App.Tests.Fakes;

public sealed class FakeDeviceUpdates : IDeviceUpdates
{
    public CheckReport? ReportToReturn { get; set; }
    public InstallOutcome? InstallOutcomeToReturn { get; set; }
    public IPowerSource Power { get; set; }

    public int CheckCalls { get; private set; }
    public List<InstallRequest> InstallCalls { get; } = [];
    public List<string> StartHandedOffCalls { get; } = [];
    public List<Uri> OpenPageCalls { get; } = [];
    public List<string> ShowInFolderCalls { get; } = [];

    public bool OpenPageResult { get; set; } = true;
    public bool ShowInFolderResult { get; set; } = true;
    public Updates.Model.DeviceInfo? DeviceToReturn { get; set; }

    public FakeDeviceUpdates(IPowerSource power)
    {
        Power = power;
    }

    public Updates.Model.DeviceInfo ReadDevice() =>
        DeviceToReturn ?? ReportToReturn?.Device ?? new Updates.Model.DeviceInfo(
            Updates.Model.MakerId.Other, "Unknown", "Unknown", null, null, null, [], []);

    public Task<CheckReport> CheckAsync(IProgress<CheckProgress>? progress, CancellationToken ct)
    {
        CheckCalls++;
        var report = ReportToReturn ?? new CheckReport(
            new Updates.Model.DeviceInfo(
                Updates.Model.MakerId.Other, "Unknown", "Unknown", null, null, null, [], []),
            [], [], [], false, DateTimeOffset.UtcNow);

        return Task.FromResult(report);
    }

    public Task<InstallOutcome> InstallAsync(InstallRequest request, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        InstallCalls.Add(request);
        var outcome = InstallOutcomeToReturn ?? new InstallOutcome(InstallStatus.Installed, "Installed.", null, null);
        return Task.FromResult(outcome);
    }

    public void StartHandedOff(string path)
    {
        StartHandedOffCalls.Add(path);
    }

    public bool OpenPage(Uri page)
    {
        OpenPageCalls.Add(page);
        return OpenPageResult;
    }

    public bool ShowInFolder(string path)
    {
        ShowInFolderCalls.Add(path);
        return ShowInFolderResult;
    }
}
