using System.Runtime.InteropServices;

namespace NitrOS.Updates.Windows;

public sealed class WinUpdateAgent : IWindowsUpdateAgent
{
    public Task<IReadOnlyList<WuRecord>> SearchDriversAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<WuRecord>>();
        var reg = ct.Register(() => tcs.TrySetResult(Array.Empty<WuRecord>()));

        var thread = new Thread(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("Microsoft.Update.Session COM class not found.");

                dynamic session = Activator.CreateInstance(sessionType)!;
                session.ClientApplicationID = "NitrOS";

                dynamic searcher = session.CreateUpdateSearcher();
                searcher.ServerSelection = 2; // ssWindowsUpdate

                ct.ThrowIfCancellationRequested();
                dynamic searchResult = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'");
                ct.ThrowIfCancellationRequested();
                dynamic updates = searchResult.Updates;
                int count = updates.Count;

                var list = new List<WuRecord>(count);
                for (int i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    dynamic u = updates.Item(i);
                    dynamic identity = u.Identity;
                    string updateId = identity.UpdateID;
                    int revision = identity.RevisionNumber;
                    string title = u.Title ?? string.Empty;
                    bool eulaAccepted = u.EulaAccepted;
                    dynamic behavior = u.InstallationBehavior;
                    bool canRequestUserInput = behavior.CanRequestUserInput;
                    int rebootBehavior = behavior.RebootBehavior;
                    long maxDownloadSize = (long)u.MaxDownloadSize;

                    // Driver properties
                    dynamic? driverObj = null;
                    try { driverObj = u.Driver; } catch { }

                    string? driverClass = null;
                    string? driverHwId = null;
                    string? driverMfg = null;
                    string? driverProvider = null;
                    string? driverModel = null;
                    DateTime? driverDate = null;

                    if (driverObj != null)
                    {
                        try { driverClass = driverObj.DriverClass; } catch { }
                        try { driverHwId = driverObj.HardwareID; } catch { }
                        try { driverMfg = driverObj.Manufacturer; } catch { }
                        try { driverProvider = driverObj.Provider; } catch { }
                        try { driverModel = driverObj.Model; } catch { }
                        try { driverDate = driverObj.DriverVerDate; } catch { }
                    }

                    list.Add(new WuRecord(
                        updateId,
                        revision,
                        title,
                        driverClass,
                        driverHwId,
                        driverMfg,
                        driverProvider,
                        driverModel,
                        driverDate,
                        maxDownloadSize,
                        false,
                        eulaAccepted,
                        canRequestUserInput,
                        rebootBehavior));
                }

                tcs.TrySetResult(list);
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(ct);
                }
                else
                {
                    tcs.TrySetException(ex);
                }
            }
            finally
            {
                reg.Dispose();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    public Task<WuInstallResult> InstallAsync(string updateId, IProgress<string>? progress, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<WuInstallResult>();

        var thread = new Thread(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("Microsoft.Update.Session COM class not found.");

                dynamic session = Activator.CreateInstance(sessionType)!;
                session.ClientApplicationID = "NitrOS";

                dynamic searcher = session.CreateUpdateSearcher();
                searcher.ServerSelection = 2;

                progress?.Report("Searching update in Windows Update...");
                dynamic searchResult = searcher.Search($"UpdateID='{updateId}'");
                dynamic updates = searchResult.Updates;
                if (updates.Count == 0)
                {
                    tcs.TrySetResult(new WuInstallResult(4, unchecked((int)0x80240003), false)); // WU_E_ITEMNOTFOUND
                    return;
                }

                dynamic u = updates.Item(0);
                if (u.InstallationBehavior.CanRequestUserInput || !u.EulaAccepted)
                {
                    // Refuse updates that require interactive prompts or license acceptance
                    tcs.TrySetResult(new WuInstallResult(4, unchecked((int)0x80240020), false)); // Operation aborted
                    return;
                }

                var updateCollType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")
                    ?? throw new InvalidOperationException("Microsoft.Update.UpdateColl COM class not found.");
                dynamic coll = Activator.CreateInstance(updateCollType)!;
                coll.Add(u);

                progress?.Report("Downloading update...");
                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = coll;
                downloader.Download();

                progress?.Report("Installing update...");
                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = coll;
                dynamic installResult = installer.Install();

                int resCode = installResult.ResultCode;
                int hResult = installResult.HResult;
                bool reboot = installResult.RebootRequired;

                tcs.TrySetResult(new WuInstallResult(resCode, hResult, reboot));
            }
            catch (COMException comEx)
            {
                tcs.TrySetResult(new WuInstallResult(4, comEx.ErrorCode, false));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }
}
