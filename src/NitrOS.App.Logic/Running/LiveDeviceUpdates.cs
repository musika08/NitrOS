using System.Diagnostics;
using System.IO;
using Microsoft.Win32.SafeHandles;
using NitrOS.App.Logic.Ports;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;
using NitrOS.Updates.Checking;
using NitrOS.Updates.Installing;
using NitrOS.Updates.Model;

namespace NitrOS.App.Logic.Running;

public sealed class LiveDeviceUpdates(
    UpdateChecker checker,
    IUpdateInstaller installer,
    IPackageRunner runner,
    IUnelevatedLauncher launcher,
    IPowerSource power) : IDeviceUpdates
{
    private readonly UpdateChecker _checker = checker ?? throw new ArgumentNullException(nameof(checker));
    private readonly IUpdateInstaller _installer = installer ?? throw new ArgumentNullException(nameof(installer));
    private readonly IPackageRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly IUnelevatedLauncher _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    private readonly IPowerSource _power = power ?? throw new ArgumentNullException(nameof(power));

    private readonly Dictionary<string, (SafeFileHandle Handle, string Path)> _handedOffFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public IPowerSource Power => _power;

    public DeviceInfo ReadDevice() => _checker.ReadDevice();

    public async Task<CheckReport> CheckAsync(IProgress<CheckProgress>? progress, CancellationToken ct)
    {
        ClearHandedOff();
        return await _checker.CheckAsync(progress, ct);
    }

    public async Task<InstallOutcome> InstallAsync(InstallRequest request, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var outcome = await _installer.InstallAsync(request, progress, ct);
        if (outcome.Status == InstallStatus.HandedOff && !string.IsNullOrEmpty(outcome.HandOffPath))
        {
            try
            {
                var handle = Authenticode.OpenLocked(outcome.HandOffPath);
                lock (_lock)
                {
                    _handedOffFiles[outcome.HandOffPath] = (handle, outcome.HandOffPath);
                }
            }
            catch { }
        }
        return outcome;
    }

    public void StartHandedOff(string path)
    {
        SafeFileHandle? handle = null;
        lock (_lock)
        {
            if (_handedOffFiles.TryGetValue(path, out var tuple))
            {
                handle = tuple.Handle;
                _handedOffFiles.Remove(path);
            }
        }

        try
        {
            if (handle != null)
            {
                _runner.StartInteractive(path, handle);
            }
            else if (File.Exists(path))
            {
                var psi = new ProcessStartInfo { FileName = path, UseShellExecute = true };
                Process.Start(psi);
            }
        }
        finally
        {
            handle?.Dispose();
        }
    }

    public bool OpenPage(Uri page) => _launcher.OpenPage(page);

    public bool ShowInFolder(string path) => _launcher.ShowInFolder(path);

    private void ClearHandedOff()
    {
        List<(SafeFileHandle Handle, string Path)> toDispose;
        lock (_lock)
        {
            toDispose = _handedOffFiles.Values.ToList();
            _handedOffFiles.Clear();
        }

        foreach (var (h, p) in toDispose)
        {
            h.Dispose();
            try { File.Delete(p); } catch { }
        }
    }
}
