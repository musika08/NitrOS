using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.Live;
using NitrOS.Live.Systems;
using NitrOS.Updates.Checking;
using NitrOS.Updates.Installing;
using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;

namespace NitrOS.App.Logic.Screens;

public sealed partial class DeviceUpdatesViewModel : ObservableObject, IScreen
{
    private readonly IDeviceUpdates _updates;
    private readonly IDialogs _dialogs;
    private readonly RunGate _gate;
    private readonly ITuneLockProbe _probe;
    private readonly ISessionFacts _session;
    private readonly IRestarter _restarter;

    private bool _isChecking;
    private string? _checkProgress;
    private IReadOnlyList<UpdateRow> _rows = [];
    private IReadOnlyList<string> _notices = [];
    private IReadOnlyList<(string Text, IRelayCommand Open)> _links = [];
    private string? _policyNote;
    private string? _lastChecked;
    private DeviceInfo? _device;

    public ScreenKey Key => ScreenKeys.DeviceUpdates;

    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            if (SetProperty(ref _isChecking, value))
            {
                CheckCommand.NotifyCanExecuteChanged();
                UpdateRowCommandsCanExecute();
            }
        }
    }

    public string? CheckProgress
    {
        get => _checkProgress;
        private set => SetProperty(ref _checkProgress, value);
    }

    public IReadOnlyList<UpdateRow> Rows
    {
        get => _rows;
        private set
        {
            if (SetProperty(ref _rows, value))
            {
                OnPropertyChanged(nameof(CanUpdateAll));
                UpdateAllCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<string> Notices
    {
        get => _notices;
        private set => SetProperty(ref _notices, value);
    }

    public IReadOnlyList<(string Text, IRelayCommand Open)> Links
    {
        get => _links;
        private set => SetProperty(ref _links, value);
    }

    public string? PolicyNote
    {
        get => _policyNote;
        private set => SetProperty(ref _policyNote, value);
    }

    public string? LastChecked
    {
        get => _lastChecked;
        private set => SetProperty(ref _lastChecked, value);
    }

    public bool CanUpdateAll => !IsChecking && Rows.Any(r => r.Install != null && r.Merged.State == ItemState.NewVersion);
    public IAsyncRelayCommand CheckCommand { get; }
    public IAsyncRelayCommand UpdateAllCommand { get; }

    public DeviceUpdatesViewModel(
        IDeviceUpdates updates,
        IDialogs dialogs,
        RunGate gate,
        ITuneLockProbe probe,
        ISessionFacts session,
        IRestarter restarter)
    {
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _restarter = restarter ?? throw new ArgumentNullException(nameof(restarter));

        CheckCommand = new AsyncRelayCommand(RunCheckAsync, () => !IsChecking);
        UpdateAllCommand = new AsyncRelayCommand(RunUpdateAllAsync, () => CanUpdateAll);
    }

    public void OnShown(object? argument)
    {
        LoadInitialDevices();
        _ = RunCheckAsync();
    }

    private void LoadInitialDevices()
    {
        try
        {
            var device = _updates.ReadDevice();
            _device = device;
            var initialCombined = Merger.CombineWithDevices(Array.Empty<MergedItem>(), device);
            var rows = new List<UpdateRow>();
            foreach (var m in initialCombined)
            {
                rows.Add(CreateRow(m));
            }
            Rows = rows;
        }
        catch { }
    }

    public void OnHidden() { }

    private async Task RunCheckAsync()
    {
        if (IsChecking) return;
        IsChecking = true;
        CheckProgress = "Checking for updates...";

        var progress = new Progress<NitrOS.Updates.Checking.CheckProgress>(p =>
        {
            CheckProgress = $"Checking {p.SourceName}... {p.Done} of {p.Total}";
        });

        try
        {
            var report = await _updates.CheckAsync(progress, CancellationToken.None);
            _device = report.Device;

            PolicyNote = report.WindowsUpdateDriversExcluded ? Texts.WuDriversExcludedNote : null;
            LastChecked = $"Checked at {report.CheckedAt.ToLocalTime():HH:mm}.";

            var noticeList = new List<string>();
            var linkList = new List<(string Text, IRelayCommand Open)>();

            foreach (var n in report.Notices)
            {
                noticeList.Add($"{n.SourceName}: {n.Problem}");
                if (n.Page != null)
                {
                    linkList.Add(($"Open {n.SourceName}'s support page", new RelayCommand(() => HandleOpenPage(n.Page))));
                }
            }

            foreach (var l in report.Links)
            {
                linkList.Add((l.Text, new RelayCommand(() => HandleOpenPage(l.Page))));
            }

            Notices = noticeList;
            Links = linkList;

            var combined = Merger.CombineWithDevices(report.Items, report.Device);
            var rows = new List<UpdateRow>();
            foreach (var m in combined)
            {
                var row = CreateRow(m);
                rows.Add(row);
            }
            Rows = rows;
        }
        catch (Exception ex)
        {
            Notices = [$"Update check failed: {ex.Message}"];
        }
        finally
        {
            IsChecking = false;
            CheckProgress = null;
        }
    }

    private UpdateRow CreateRow(MergedItem merged)
    {
        var row = new UpdateRow(merged);

        if (merged.State == ItemState.NewVersion)
        {
            row.Install = new AsyncRelayCommand(() => RunInstallFlowAsync(row, confirmPrompt: null), () => CanInstall(row));
        }
        else if (merged.State == ItemState.CantTell && (merged.Item.DownloadUrl != null || merged.Item.WindowsUpdateId != null))
        {
            row.GetAnyway = new AsyncRelayCommand(
                () => RunInstallFlowAsync(row, "NitrOS can't read the installed version. Get this one anyway?"),
                () => CanInstall(row));
        }
        else if (merged.State == ItemState.PageOnly && merged.Item.Page != null)
        {
            row.OpenPage = new RelayCommand(() => HandleOpenPage(merged.Item.Page));
        }

        return row;
    }

    private bool CanInstall(UpdateRow row)
    {
        if (!_session.IsElevated)
        {
            row.Unavailable = "Administrator rights required.";
            return false;
        }

        if (!_gate.CanChange(_probe))
        {
            row.Unavailable = "Another operation is running.";
            return false;
        }

        if (IsChecking)
        {
            row.Unavailable = "Check in progress.";
            return false;
        }

        row.Unavailable = null;
        return true;
    }

    private void UpdateRowCommandsCanExecute()
    {
        foreach (var r in Rows)
        {
            r.Install?.NotifyCanExecuteChanged();
            r.GetAnyway?.NotifyCanExecuteChanged();
        }
        OnPropertyChanged(nameof(CanUpdateAll));
        UpdateAllCommand?.NotifyCanExecuteChanged();
    }

    private async Task RunUpdateAllAsync()
    {
        if (IsChecking) return;
        var targets = Rows.Where(r => r.Install != null && r.Merged.State == ItemState.NewVersion).ToList();
        if (targets.Count == 0) return;

        foreach (var row in targets)
        {
            if (row.Install != null && row.Install.CanExecute(null))
            {
                await row.Install.ExecuteAsync(null);
            }
        }
        UpdateRowCommandsCanExecute();
    }

    private async Task RunInstallFlowAsync(UpdateRow row, string? confirmPrompt)
    {
        var item = row.Item;

        if (!string.IsNullOrEmpty(confirmPrompt))
        {
            var ok = await _dialogs.ConfirmAsync(new ConfirmRequest(
                "Notice",
                confirmPrompt,
                [],
                "Get update",
                "Cancel"));
            if (!ok) return;
        }

        bool confirmedWarning = false;
        if (row.IsFirmware)
        {
            var kindName = item.Kind == UpdateKind.Bios ? "BIOS" : "firmware";
            var ok = await _dialogs.ConfirmAsync(new ConfirmRequest(
                $"Before you update the {kindName}",
                "",
                ["Keep the charger connected.", "Do not turn off or restart the PC until it says it is done.", "It can take several minutes."],
                "I understand",
                "Cancel"));
            if (!ok) return;
            confirmedWarning = true;
        }
        else
        {
            var ok = await _dialogs.ConfirmAsync(new ConfirmRequest(
                $"Install {item.Name}?",
                $"{item.LatestVersion} from {item.SourceName}. A restore point is made first.",
                [],
                "Install",
                "Cancel"));
            if (!ok) return;
        }

        using var gateToken = _gate.TryEnter(Texts.InstallingCloseMessage);
        if (gateToken == null)
        {
            await _dialogs.ShowAsync("Busy", "Another operation is in progress.");
            return;
        }

        UpdateRowCommandsCanExecute();
        row.Progress = "Preparing installation...";

        var progress = new Progress<InstallProgress>(p =>
        {
            row.Progress = p.Line ?? p.Stage.ToString();
        });

        try
        {
            var request = new InstallRequest(row.Merged, _device!, confirmedWarning);
            var outcome = await _updates.InstallAsync(request, progress, CancellationToken.None);

            row.Progress = null;
            row.Result = outcome.Message;

            if (outcome.Status == InstallStatus.HandedOff && !string.IsNullOrEmpty(outcome.HandOffPath))
            {
                row.StartUpdater = new RelayCommand(async () => await StartHandedOffFlowAsync(row, outcome.HandOffPath));
                row.OpenFolder = new RelayCommand(() =>
                {
                    if (!_updates.ShowInFolder(outcome.HandOffPath))
                    {
                        row.Result = $"File saved to: {outcome.HandOffPath}";
                    }
                });
            }
            else if (outcome.Status == InstallStatus.InstalledRestartNeeded)
            {
                var restartOk = await _dialogs.ConfirmAsync(new ConfirmRequest(
                    "Restart required",
                    "Restart your PC now to finish the update?",
                    [],
                    "Restart now",
                    "Later"));
                if (restartOk)
                {
                    _restarter.Restart();
                }
            }
        }
        catch (Exception ex)
        {
            row.Progress = null;
            row.Result = $"Error: {ex.Message}";
        }
        finally
        {
            UpdateRowCommandsCanExecute();
        }
    }

    private async Task StartHandedOffFlowAsync(UpdateRow row, string path)
    {
        if (row.IsFirmware)
        {
            var power = _updates.Power.Read();
            if (power.HasBattery && power.OnBattery)
            {
                await _dialogs.ShowAsync("Charger required", "Connect the charger first. Firmware and BIOS updates must not lose power.");
                return;
            }

            var kindName = row.Item.Kind == UpdateKind.Bios ? "BIOS" : "firmware";
            var ok = await _dialogs.ConfirmAsync(new ConfirmRequest(
                $"Before you update the {kindName}",
                "",
                ["Keep the charger connected.", "Do not turn off or restart the PC until it says it is done.", "It can take several minutes."],
                "I understand",
                "Cancel"));
            if (!ok) return;
        }

        _updates.StartHandedOff(path);
    }

    private void HandleOpenPage(Uri page)
    {
        if (!_updates.OpenPage(page))
        {
            _dialogs.ShowAsync("Open link", page.ToString());
        }
    }
}
