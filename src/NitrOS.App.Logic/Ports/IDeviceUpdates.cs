using NitrOS.Live.Systems;
using NitrOS.Updates.Checking;
using NitrOS.Updates.Installing;

namespace NitrOS.App.Logic.Ports;

public interface IDeviceUpdates
{
    Task<CheckReport> CheckAsync(IProgress<CheckProgress>? progress, CancellationToken ct);
    Task<InstallOutcome> InstallAsync(InstallRequest request, IProgress<InstallProgress>? progress, CancellationToken ct);
    void StartHandedOff(string path);
    bool OpenPage(Uri page);
    bool ShowInFolder(string path);
    IPowerSource Power { get; }
}
