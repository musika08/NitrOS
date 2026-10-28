using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Formatting;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;

namespace NitrOS.App.Logic.Screens;

public sealed class DoneViewModel : ObservableObject, IScreen
{
    private readonly Catalog _catalog;
    private readonly Navigator _navigator;
    private readonly IDialogs _dialogs;
    private readonly IRestarter _restarter;
    private readonly IXboxRestorer _xboxRestorer;
    private readonly IMachineMeter _meter;
    private readonly RunGate _gate;

    private RunResult? _result;
    private string _headline = "";
    private IReadOnlyList<string> _counts = [];
    private IReadOnlyList<string> _problems = [];
    private IReadOnlyList<string> _mayBeChanged = [];
    private string? _mayBeChangedHeading;
    private string? _beforeAfter;
    private string? _restorePointLine;
    private string? _isoLine;
    private bool _canRetryWithoutRestorePoint;
    private bool _needsRestart;
    private bool _showAddXboxBack;

    public ScreenKey Key => ScreenKeys.Done;

    public string Headline
    {
        get => _headline;
        private set => SetProperty(ref _headline, value);
    }

    public IReadOnlyList<string> Counts
    {
        get => _counts;
        private set => SetProperty(ref _counts, value);
    }

    public IReadOnlyList<string> Problems
    {
        get => _problems;
        private set => SetProperty(ref _problems, value);
    }

    public IReadOnlyList<string> MayBeChanged
    {
        get => _mayBeChanged;
        private set => SetProperty(ref _mayBeChanged, value);
    }

    public string? MayBeChangedHeading
    {
        get => _mayBeChangedHeading;
        private set => SetProperty(ref _mayBeChangedHeading, value);
    }

    public string? BeforeAfter
    {
        get => _beforeAfter;
        private set => SetProperty(ref _beforeAfter, value);
    }

    public string? RestorePointLine
    {
        get => _restorePointLine;
        private set => SetProperty(ref _restorePointLine, value);
    }

    public string? IsoLine
    {
        get => _isoLine;
        private set => SetProperty(ref _isoLine, value);
    }

    public bool CanRetryWithoutRestorePoint
    {
        get => _canRetryWithoutRestorePoint;
        private set => SetProperty(ref _canRetryWithoutRestorePoint, value);
    }

    public bool NeedsRestart
    {
        get => _needsRestart;
        private set => SetProperty(ref _needsRestart, value);
    }

    public bool ShowAddXboxBack
    {
        get => _showAddXboxBack;
        private set => SetProperty(ref _showAddXboxBack, value);
    }

    public IAsyncRelayCommand RetryWithoutRestorePoint { get; }
    public IAsyncRelayCommand Restart { get; }
    public IAsyncRelayCommand AddXboxBack { get; }
    public IRelayCommand Close { get; }

    public DoneViewModel(
        Catalog catalog,
        Navigator navigator,
        IDialogs dialogs,
        IRestarter restarter,
        IXboxRestorer xboxRestorer,
        IMachineMeter meter,
        RunGate gate)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _restarter = restarter ?? throw new ArgumentNullException(nameof(restarter));
        _xboxRestorer = xboxRestorer ?? throw new ArgumentNullException(nameof(xboxRestorer));
        _meter = meter ?? throw new ArgumentNullException(nameof(meter));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));

        RetryWithoutRestorePoint = new AsyncRelayCommand(ExecuteRetryWithoutRestorePointAsync, () => CanRetryWithoutRestorePoint);
        Restart = new AsyncRelayCommand(ExecuteRestartAsync, () => NeedsRestart && !_gate.IsBusy);
        AddXboxBack = new AsyncRelayCommand(ExecuteAddXboxBackAsync, () => ShowAddXboxBack);
        Close = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Start));
    }

    public void OnShown(object? argument)
    {
        if (argument is not RunResult res) return;
        _result = res;

        // Headline
        if (res.Refused)
        {
            Headline = res.RefusedReason ?? "Refused.";
        }
        else if (res.Cancelled)
        {
            Headline = res.Kind == RunKind.Iso
                ? Texts.IsoStopped
                : "Stopped. The change in progress was put back.";
        }
        else if (res.Kind == RunKind.Iso)
        {
            Headline = "Your slim ISO is ready.";
        }
        else
        {
            int appliedCount = res.Switches.Count(kv => kv.Value == SwitchOutcome.Applied);
            Headline = $"Done. {Plain.Count(appliedCount, "switch applied", "switches applied")}.";
        }

        // Counts
        var counts = new List<string>();
        int applied = res.Switches.Count(kv => kv.Value == SwitchOutcome.Applied);
        int partly = res.Switches.Count(kv => kv.Value == SwitchOutcome.PartlyApplied);
        int failed = res.Switches.Count(kv => kv.Value == SwitchOutcome.Failed);
        int skipped = res.Switches.Count(kv => kv.Value == SwitchOutcome.Skipped);

        if (applied > 0) counts.Add($"{applied} applied");
        if (partly > 0) counts.Add($"{partly} partly applied");
        if (failed > 0) counts.Add($"{failed} failed");
        if (skipped > 0) counts.Add($"{skipped} skipped");
        Counts = counts;

        // Problems
        Problems = res.Problems
            .Select(p =>
            {
                var name = _catalog.Switches.FirstOrDefault(s => s.Id == p.SwitchId)?.Name ?? p.SwitchId;
                return $"{name}: {p.Detail}";
            })
            .ToList();

        // MayBeChanged
        MayBeChanged = res.MayBeChanged
            .Select(id => _catalog.Switches.FirstOrDefault(s => s.Id == id)?.Name ?? id)
            .ToList();
        MayBeChangedHeading = MayBeChanged.Count > 0 ? Texts.MayBeChangedHeading : null;

        // BeforeAfter
        if (res.Before is not null && res.After is not null)
        {
            var mem = _meter.Memory();
            var memLine = $"Memory in use: {Plain.Memory(res.Before.MemoryInUseMb)} before, {Plain.Memory(res.After.MemoryInUseMb)} now, of {(mem.VisibleMb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} GB available to Windows.";
            var procLine = $"Processes: {res.Before.Processes} before, {res.After.Processes} now.";
            var note = "Measured on this PC just now. The numbers settle after a restart; NitrOS measures again then.";
            BeforeAfter = $"{memLine} {procLine} {note}";
        }
        else
        {
            BeforeAfter = null;
        }

        // RestorePointLine
        RestorePointLine = res.RestorePoint switch
        {
            RestorePointOutcome.Created => "A restore point was made first.",
            RestorePointOutcome.SkippedRecent => "Windows already made a restore point in the last 24 hours, so that one is used.",
            RestorePointOutcome.CreatedUnknown => "Windows took the restore point request, but NitrOS could not confirm it.",
            RestorePointOutcome.Unavailable when res.Kind == RunKind.Tune => "No restore point was made. NitrOS's own record still lets you undo.",
            _ => null
        };

        // IsoLine
        IsoLine = res.Kind == RunKind.Iso && !string.IsNullOrEmpty(res.OutputPath)
            ? $"Your ISO is at {res.OutputPath}. Test it in a virtual machine before you install it."
            : null;

        CanRetryWithoutRestorePoint = res.CanRetryWithoutRestorePoint;
        NeedsRestart = res.NeedsRestart;

        var isMaxLean = res.Request?.Card == CardId.MaxLean;
        var hasXboxSwitches = res.Switches.Any(kv =>
            (kv.Key == "xbox-app-remove" || kv.Key == "store-remove" || kv.Key == "gaming-services-remove")
            && (kv.Value == SwitchOutcome.Applied || kv.Value == SwitchOutcome.PartlyApplied));

        ShowAddXboxBack = (isMaxLean || hasXboxSwitches) && _xboxRestorer.IsAvailable;

        RetryWithoutRestorePoint.NotifyCanExecuteChanged();
        Restart.NotifyCanExecuteChanged();
        AddXboxBack.NotifyCanExecuteChanged();
    }

    private async Task ExecuteRetryWithoutRestorePointAsync()
    {
        if (_result?.Request is null) return;
        var confirmed = await _dialogs.ConfirmAsync(new ConfirmRequest(
            Title: "Go ahead without a restore point?",
            Message: "Windows cannot make a restore point right now. NitrOS still saves every old value so you can undo.",
            Items: [],
            ConfirmText: "Go ahead",
            CancelText: "Cancel"));
        if (!confirmed) return;

        var retryReq = _result.Request with { AllowWithoutRestorePoint = true };
        _navigator.GoTo(ScreenKeys.Working, retryReq);
    }

    private async Task ExecuteRestartAsync()
    {
        if (_gate.IsBusy) return;
        var confirmed = await _dialogs.ConfirmAsync(new ConfirmRequest(
            Title: "Restart now?",
            Message: "Save your work in other apps first.",
            Items: [],
            ConfirmText: "Restart",
            CancelText: "Later"));
        if (!confirmed) return;

        _restarter.Restart();
    }

    private async Task ExecuteAddXboxBackAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(new ConfirmRequest(
            Title: "Add Xbox back?",
            Message: "This will restore Microsoft Store, Xbox identity provider, Game Bar, and Gaming Services.",
            Items: [],
            ConfirmText: "Add Xbox back",
            CancelText: "Cancel"));
        if (!confirmed) return;

        var emptySel = Selection.FromIds(_catalog, [], TargetMode.TunePc, DeviceProfile.Desktop);
        var req = new RunRequest(RunKind.XboxRestore, emptySel, null, false, null);
        _navigator.GoTo(ScreenKeys.Working, req);
    }
}
