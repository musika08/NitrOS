using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Storage;
using NitrOS.Core.Model;
using NitrOS.Live;
using NitrOS.Live.Systems;

namespace NitrOS.App.Logic.Screens;

public sealed class UndoViewModel : ObservableObject, IScreen
{
    private readonly JournalAccess _journals;
    private readonly IUndoService _undoService;
    private readonly Catalog _catalog;
    private readonly IDialogs _dialogs;
    private readonly IRestarter _restarter;
    private readonly RunGate _gate;
    private readonly Navigator _navigator;

    private IReadOnlyList<RunSummary> _runs = [];
    private string? _hiddenNote;
    private string? _unreadableNote;
    private string? _folderProblem;
    private string? _lastResult;
    private bool _needsRestart;

    public ScreenKey Key => ScreenKeys.Undo;

    public IReadOnlyList<RunSummary> Runs
    {
        get => _runs;
        private set => SetProperty(ref _runs, value);
    }

    public string? HiddenNote
    {
        get => _hiddenNote;
        private set => SetProperty(ref _hiddenNote, value);
    }

    public string? UnreadableNote
    {
        get => _unreadableNote;
        private set => SetProperty(ref _unreadableNote, value);
    }

    public string? FolderProblem
    {
        get => _folderProblem;
        private set => SetProperty(ref _folderProblem, value);
    }

    public string? LastResult
    {
        get => _lastResult;
        private set => SetProperty(ref _lastResult, value);
    }

    public bool NeedsRestart
    {
        get => _needsRestart;
        private set => SetProperty(ref _needsRestart, value);
    }

    public IAsyncRelayCommand<SwitchSummary> UndoSwitch { get; }
    public IAsyncRelayCommand UndoEverything { get; }
    public IAsyncRelayCommand Restart { get; }
    public IRelayCommand Back { get; }

    public UndoViewModel(
        JournalAccess journals,
        IUndoService undoService,
        Catalog catalog,
        IDialogs dialogs,
        IRestarter restarter,
        RunGate gate,
        Navigator navigator)
    {
        _journals = journals ?? throw new ArgumentNullException(nameof(journals));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _restarter = restarter ?? throw new ArgumentNullException(nameof(restarter));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        UndoSwitch = new AsyncRelayCommand<SwitchSummary>(ExecuteUndoSwitchAsync, s => s is not null && s.State == SwitchUndoState.CanUndo);
        UndoEverything = new AsyncRelayCommand(ExecuteUndoEverythingAsync, () => Runs.Count > 0);
        Restart = new AsyncRelayCommand(ExecuteRestartAsync, () => NeedsRestart && !_gate.IsBusy);
        Back = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Start));
    }

    public void OnShown(object? argument)
    {
        LoadRuns();
    }

    private void LoadRuns()
    {
        var listing = _journals.ListMine();
        FolderProblem = listing.FolderProblem;

        HiddenNote = listing.OtherAccounts > 0
            ? $"{listing.OtherAccounts} {(listing.OtherAccounts == 1 ? "run" : "runs")} from other Windows accounts {(listing.OtherAccounts == 1 ? "is" : "are")} not shown."
            : null;

        UnreadableNote = listing.Unreadable > 0
            ? $"{listing.Unreadable} {(listing.Unreadable == 1 ? "journal" : "journals")} could not be read."
            : null;

        Runs = listing.Mine
            .Select(m => RunSummary.Build(m.Path, m.Journal, _catalog))
            .ToList();

        UndoEverything.NotifyCanExecuteChanged();
    }

    private async Task ExecuteUndoSwitchAsync(SwitchSummary? summary)
    {
        if (summary is null) return;

        using var lease = _gate.TryEnter();
        if (lease is null)
        {
            LastResult = TuneEngine.BusyReason;
            return;
        }

        var run = Runs.FirstOrDefault(r => r.Switches.Any(s => s.Id == summary.Id && s.State == SwitchUndoState.CanUndo));
        if (run is null) return;

        var outcome = await _undoService.UndoAsync(run.Path, [summary.Id], CancellationToken.None);

        if (outcome.Refused is not null)
        {
            LastResult = outcome.Refused;
            return;
        }

        var lines = new List<string>();
        if (outcome.Undone.Count > 0)
        {
            var names = outcome.Undone.Select(id => _catalog.Switches.FirstOrDefault(s => s.Id == id)?.Name ?? id);
            lines.Add($"Put back: {string.Join(", ", names)}");
            NeedsRestart = true;
            Restart.NotifyCanExecuteChanged();
        }
        if (outcome.CannotUndo.Count > 0)
        {
            foreach (var (id, reason) in outcome.CannotUndo)
            {
                var name = _catalog.Switches.FirstOrDefault(s => s.Id == id)?.Name ?? id;
                lines.Add($"Can't undo: {name}: {reason}");
            }
        }
        if (lines.Count == 0 && outcome.NothingToUndo.Count > 0)
        {
            lines.Add("Nothing left to put back.");
        }

        LastResult = string.Join("\n", lines);
        LoadRuns();
    }

    private async Task ExecuteUndoEverythingAsync()
    {
        using var lease = _gate.TryEnter();
        if (lease is null)
        {
            LastResult = TuneEngine.BusyReason;
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(new ConfirmRequest(
            Title: "Put back every change NitrOS made on this account?",
            Message: "This puts back every setting, service and app changed by NitrOS for which a backup exists.",
            Items: [],
            ConfirmText: "Put back",
            CancelText: "Cancel"));

        if (!confirmed) return;

        var allUndone = new List<string>();
        var allCannot = new List<(string SwitchId, string Reason)>();

        foreach (var run in Runs)
        {
            var outcome = await _undoService.UndoAsync(run.Path, null, CancellationToken.None);
            if (outcome.Refused is not null)
            {
                LastResult = outcome.Refused;
                LoadRuns();
                return;
            }
            allUndone.AddRange(outcome.Undone);
            allCannot.AddRange(outcome.CannotUndo);
        }

        var lines = new List<string>();
        if (allUndone.Count > 0)
        {
            var names = allUndone.Select(id => _catalog.Switches.FirstOrDefault(s => s.Id == id)?.Name ?? id);
            lines.Add($"Put back: {string.Join(", ", names)}");
            NeedsRestart = true;
            Restart.NotifyCanExecuteChanged();
        }
        if (allCannot.Count > 0)
        {
            foreach (var (id, reason) in allCannot)
            {
                var name = _catalog.Switches.FirstOrDefault(s => s.Id == id)?.Name ?? id;
                lines.Add($"Can't undo: {name}: {reason}");
            }
        }
        if (lines.Count == 0)
        {
            lines.Add("Nothing left to put back.");
        }

        LastResult = string.Join("\n", lines);
        LoadRuns();
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
}
