namespace NitrOS.Updates.Windows;

public sealed record WuRecord(
    string UpdateId,
    int Revision,
    string Title,
    string? DriverClass,
    string? DriverHardwareId,
    string? DriverManufacturer,
    string? DriverProvider,
    string? DriverModel,
    DateTime? DriverVerDate,
    long? MaxDownloadSize,
    bool BrowseOnly,
    bool EulaAccepted,
    bool CanRequestUserInput,
    int RebootBehavior);

public sealed record WuInstallResult(
    int ResultCode,
    int HResult,
    bool RebootRequired);

public interface IWindowsUpdateAgent
{
    Task<IReadOnlyList<WuRecord>> SearchDriversAsync(CancellationToken ct);
    Task<WuInstallResult> InstallAsync(string updateId, IProgress<string>? progress, CancellationToken ct);
}
