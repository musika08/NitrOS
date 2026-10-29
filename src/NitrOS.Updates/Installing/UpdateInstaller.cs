using Microsoft.Win32.SafeHandles;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;
using NitrOS.Updates.KnownBad;
using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;
using NitrOS.Updates.Windows;

namespace NitrOS.Updates.Installing;

public sealed class UpdateInstaller(
    IWindowsUpdateAgent agent,
    Downloader downloader,
    IFileVerifier verifier,
    IPackageRunner runner,
    IPowerSource power,
    IRestorePoint restorePoint,
    KnownBadList knownBad,
    IReadOnlyList<SourcePolicy> policies,
    string downloadFolder,
    Action<string> ensureFolder) : IUpdateInstaller
{
    private readonly IWindowsUpdateAgent _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    private readonly Downloader _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
    private readonly IFileVerifier _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
    private readonly IPackageRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly IPowerSource _power = power ?? throw new ArgumentNullException(nameof(power));
    private readonly IRestorePoint _restorePoint = restorePoint ?? throw new ArgumentNullException(nameof(restorePoint));
    private readonly KnownBadList _knownBad = knownBad ?? throw new ArgumentNullException(nameof(knownBad));
    private readonly IReadOnlyList<SourcePolicy> _policies = policies ?? Array.Empty<SourcePolicy>();
    private readonly string _downloadFolder = downloadFolder ?? throw new ArgumentNullException(nameof(downloadFolder));
    private readonly Action<string> _ensureFolder = ensureFolder ?? throw new ArgumentNullException(nameof(ensureFolder));

    public async Task<InstallOutcome> InstallAsync(
        InstallRequest request,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        var mergedItem = request.Item;
        var item = mergedItem.Item;
        var device = request.Device;

        // 1. Item.State must be NewVersion or CantTell
        if (mergedItem.State != ItemState.NewVersion && mergedItem.State != ItemState.CantTell)
        {
            return new InstallOutcome(InstallStatus.Refused, $"Cannot install item in state '{mergedItem.State}'.", null, null);
        }

        // 2. Known-bad check again
        if (!string.IsNullOrEmpty(item.LatestVersion))
        {
            var bad = _knownBad.Find(device.Maker, device.ModelCode, item.Kind, item.LatestVersion);
            if (bad != null)
            {
                return new InstallOutcome(InstallStatus.Refused, bad.Reason, null, null);
            }
        }

        // 3. Bios or Firmware checks
        var isBiosOrFw = item.Kind == UpdateKind.Bios || item.Kind == UpdateKind.Firmware;
        if (isBiosOrFw)
        {
            if (!request.UserConfirmedFirmwareWarning)
            {
                return new InstallOutcome(InstallStatus.Refused, "Firmware warning must be confirmed before proceeding.", null, null);
            }

            var powerState = _power.Read();
            if (powerState.HasBattery && powerState.OnBattery)
            {
                return new InstallOutcome(InstallStatus.Refused, "Connect the charger first. Firmware and BIOS updates must not lose power.", null, null);
            }
        }

        // 4. Restore point
        progress?.Report(new InstallProgress(InstallStage.Checking, null, null, "Creating system restore point..."));
        var rpOutcome = _restorePoint.Create($"NitrOS device update: {item.Name}");

        // 5. Method WindowsUpdate
        if (item.Method == InstallMethod.WindowsUpdate)
        {
            if (string.IsNullOrEmpty(item.WindowsUpdateId))
            {
                return new InstallOutcome(InstallStatus.Failed, "Missing Windows Update ID.", null, rpOutcome);
            }

            var wuProgress = new Progress<string>(s => progress?.Report(new InstallProgress(InstallStage.Installing, null, null, s)));
            var wuRes = await _agent.InstallAsync(item.WindowsUpdateId, wuProgress, ct);

            return wuRes.ResultCode switch
            {
                2 => new InstallOutcome(
                    wuRes.RebootRequired ? InstallStatus.InstalledRestartNeeded : InstallStatus.Installed,
                    "Successfully installed.",
                    null,
                    rpOutcome),
                3 => new InstallOutcome(
                    InstallStatus.Installed,
                    "Windows reported some errors.",
                    null,
                    rpOutcome),
                4 => new InstallOutcome(
                    InstallStatus.Failed,
                    $"Installation failed with HRESULT 0x{wuRes.HResult:X8}.",
                    null,
                    rpOutcome),
                5 => new InstallOutcome(
                    InstallStatus.Cancelled,
                    "Operation was cancelled.",
                    null,
                    rpOutcome),
                _ => new InstallOutcome(
                    InstallStatus.Failed,
                    $"Windows Update returned unexpected result code {wuRes.ResultCode}.",
                    null,
                    rpOutcome)
            };
        }

        // 6. Other methods: download with policy
        if (item.DownloadUrl == null)
        {
            return new InstallOutcome(InstallStatus.Refused, "No download URL provided.", null, rpOutcome);
        }

        var policy = _policies.FirstOrDefault(p => string.Equals(p.SourceId, item.SourceId, StringComparison.OrdinalIgnoreCase));
        if (policy == null)
        {
            return new InstallOutcome(InstallStatus.Refused, $"NitrOS has no download rules for {item.SourceId}.", null, rpOutcome);
        }

        _ensureFolder(_downloadFolder);
        string downloadedPath;
        try
        {
            downloadedPath = await _downloader.DownloadAsync(item.DownloadUrl, policy, item.SizeBytes, _downloadFolder, progress, ct);
        }
        catch (Exception ex)
        {
            return new InstallOutcome(InstallStatus.Refused, ex.Message, null, rpOutcome);
        }

        // 7. Verify file lock, checksum, signature
        progress?.Report(new InstallProgress(InstallStage.Verifying, null, null, "Verifying file signature and checksum..."));
        SafeFileHandle handle;
        try
        {
            handle = Authenticode.OpenLocked(downloadedPath);
        }
        catch (Exception lockEx)
        {
            try { File.Delete(downloadedPath); } catch { }
            return new InstallOutcome(InstallStatus.Failed, $"Could not open locked file: {lockEx.Message}", null, rpOutcome);
        }

        try
        {
            if (!string.IsNullOrEmpty(item.Sha256))
            {
                if (!_verifier.Sha256Matches(handle, item.Sha256))
                {
                    handle.Dispose();
                    try { File.Delete(downloadedPath); } catch { }
                    return new InstallOutcome(InstallStatus.Refused, "The download does not match the maker's checksum.", null, rpOutcome);
                }
            }

            var signers = _verifier.Signers(handle, downloadedPath);
            if (signers == null || !signers.Any(s => policy.ExpectedSigners.Any(expected => Authenticode.NamesMatch(s, expected))))
            {
                handle.Dispose();
                try { File.Delete(downloadedPath); } catch { }
                return new InstallOutcome(InstallStatus.Refused, $"The file is not signed by {item.SourceName}, so NitrOS will not run it.", null, rpOutcome);
            }

            // 8. HandOff
            if (item.Method == InstallMethod.HandOff)
            {
                handle.Dispose();
                return new InstallOutcome(InstallStatus.HandedOff, "Ready for manual installation.", downloadedPath, rpOutcome);
            }

            // 9. MakerPackage
            if (item.Method == InstallMethod.MakerPackage)
            {
                progress?.Report(new InstallProgress(InstallStage.Installing, null, null, "Running installer..."));
                int exitCode;
                try
                {
                    exitCode = _runner.Run(downloadedPath, handle, item.SilentArgs, TimeSpan.FromMinutes(60));
                }
                finally
                {
                    handle.Dispose();
                    try { File.Delete(downloadedPath); } catch { }
                }

                if (exitCode == 0)
                {
                    var status = item.RestartLikely ? InstallStatus.InstalledRestartNeeded : InstallStatus.Installed;
                    return new InstallOutcome(status, "Installation completed successfully.", null, rpOutcome);
                }
                else if (exitCode == 3010) // ERROR_SUCCESS_REBOOT_REQUIRED
                {
                    return new InstallOutcome(InstallStatus.InstalledRestartNeeded, "Installation completed. A restart is required.", null, rpOutcome);
                }
                else
                {
                    return new InstallOutcome(InstallStatus.Failed, $"The installer stopped with code {exitCode}.", null, rpOutcome);
                }
            }

            return new InstallOutcome(InstallStatus.Refused, $"Unsupported install method '{item.Method}'.", null, rpOutcome);
        }
        catch (Exception ex)
        {
            handle.Dispose();
            try { File.Delete(downloadedPath); } catch { }
            return new InstallOutcome(InstallStatus.Failed, ex.Message, null, rpOutcome);
        }
    }
}
