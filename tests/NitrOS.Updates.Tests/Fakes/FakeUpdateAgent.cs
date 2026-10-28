using NitrOS.Updates.Windows;

namespace NitrOS.Updates.Tests.Fakes;

public sealed class FakeUpdateAgent : IWindowsUpdateAgent
{
    public Func<CancellationToken, Task<IReadOnlyList<WuRecord>>>? OnSearch { get; set; }
    public Func<string, IProgress<string>?, CancellationToken, Task<WuInstallResult>>? OnInstall { get; set; }

    public Task<IReadOnlyList<WuRecord>> SearchDriversAsync(CancellationToken ct)
    {
        if (OnSearch != null)
        {
            return OnSearch(ct);
        }
        return Task.FromResult<IReadOnlyList<WuRecord>>(Array.Empty<WuRecord>());
    }

    public Task<WuInstallResult> InstallAsync(string updateId, IProgress<string>? progress, CancellationToken ct)
    {
        if (OnInstall != null)
        {
            return OnInstall(updateId, progress, ct);
        }
        return Task.FromResult(new WuInstallResult(2, 0, false));
    }
}
