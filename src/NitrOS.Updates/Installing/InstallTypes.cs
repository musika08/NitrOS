using NitrOS.Core.Model;
using NitrOS.Live.Systems;
using NitrOS.Updates.Model;

namespace NitrOS.Updates.Installing;

public enum InstallStage
{
    Checking,
    Downloading,
    Verifying,
    Installing,
    Done
}

public sealed record InstallProgress(
    InstallStage Stage,
    long? Bytes,
    long? Total,
    string? Line);

public enum InstallStatus
{
    Installed,
    InstalledRestartNeeded,
    HandedOff,
    Refused,
    Failed,
    Cancelled
}

public sealed record InstallOutcome(
    InstallStatus Status,
    string Message,
    string? HandOffPath,
    RestorePointOutcome? RestorePoint);

public sealed record InstallRequest(
    NitrOS.Updates.Merging.MergedItem Item,
    DeviceInfo Device,
    bool UserConfirmedFirmwareWarning);

public interface IUpdateInstaller
{
    Task<InstallOutcome> InstallAsync(
        InstallRequest request,
        IProgress<InstallProgress>? progress,
        CancellationToken ct);
}
