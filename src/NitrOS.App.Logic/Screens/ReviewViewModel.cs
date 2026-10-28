using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;

namespace NitrOS.App.Logic.Screens;

public sealed class ReviewViewModel : ObservableObject, IScreen
{
    private readonly Catalog _catalog;
    private readonly Draft _draft;
    private readonly RunnerSet _runners;
    private readonly ISessionFacts _session;
    private readonly ITuneLockProbe _probe;
    private readonly RunGate _gate;
    private readonly IDialogs _dialogs;
    private readonly Navigator _navigator;

    private RunRequest? _request;
    private IReadOnlyList<ReviewGroup> _changes = [];
    private IReadOnlyList<string> _stopsWorking = [];
    private IReadOnlyList<string> _dependencyWarnings = [];
    private string? _irreversibleWarning;
    private IReadOnlyList<string> _irreversibleNames = [];
    private IReadOnlyList<string> _scriptNames = [];
    private string? _safetyNote;
    private string? _goUnavailableReason;

    public ScreenKey Key => ScreenKeys.Review;

    public IReadOnlyList<ReviewGroup> Changes
    {
        get => _changes;
        private set => SetProperty(ref _changes, value);
    }

    public IReadOnlyList<string> StopsWorking
    {
        get => _stopsWorking;
        private set => SetProperty(ref _stopsWorking, value);
    }

    public IReadOnlyList<string> DependencyWarnings
    {
        get => _dependencyWarnings;
        private set => SetProperty(ref _dependencyWarnings, value);
    }

    public string? IrreversibleWarning
    {
        get => _irreversibleWarning;
        private set => SetProperty(ref _irreversibleWarning, value);
    }

    public IReadOnlyList<string> IrreversibleNames
    {
        get => _irreversibleNames;
        private set => SetProperty(ref _irreversibleNames, value);
    }

    public IReadOnlyList<string> ScriptNames
    {
        get => _scriptNames;
        private set => SetProperty(ref _scriptNames, value);
    }

    public string? SafetyNote
    {
        get => _safetyNote;
        private set => SetProperty(ref _safetyNote, value);
    }

    public string? GoUnavailableReason
    {
        get => _goUnavailableReason;
        private set => SetProperty(ref _goUnavailableReason, value);
    }

    public IAsyncRelayCommand Go { get; }
    public IRelayCommand Back { get; }
    public IRelayCommand CheckAgain { get; }

    public ReviewViewModel(
        Catalog catalog,
        Draft draft,
        RunnerSet runners,
        ISessionFacts session,
        ITuneLockProbe probe,
        RunGate gate,
        IDialogs dialogs,
        Navigator navigator)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _runners = runners ?? throw new ArgumentNullException(nameof(runners));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        Go = new AsyncRelayCommand(ExecuteGoAsync, CanExecuteGo);
        Back = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Switches));
        CheckAgain = new RelayCommand(UpdateGoStatus);
    }

    public void OnShown(object? argument)
    {
        if (argument is RunRequest req)
        {
            _request = req;
        }
        else if (argument is Draft d)
        {
            _request = RequestFromDraft(d);
        }
        else
        {
            _request = RequestFromDraft(_draft);
        }

        Refresh();
    }

    private static RunRequest? RequestFromDraft(Draft d)
    {
        if (d.Selection is null) return null;
        var kind = d.Mode == TargetMode.TunePc ? RunKind.Tune : RunKind.Iso;
        return new RunRequest(kind, d.Selection, d.Card, false, d.Iso);
    }

    private void Refresh()
    {
        if (_request is null || _request.Selection.On.Count == 0)
        {
            Changes = [];
            StopsWorking = [];
            DependencyWarnings = [];
            IrreversibleWarning = null;
            IrreversibleNames = [];
            ScriptNames = [];
            SafetyNote = null;
            UpdateGoStatus();
            return;
        }

        var sel = _request.Selection;

        // Changes (by group)
        var changesList = new List<ReviewGroup>();
        foreach (var group in _catalog.Groups)
        {
            var names = group.Switches
                .Where(s => sel.IsOn(s.Id))
                .Select(s => s.Name)
                .ToList();
            if (names.Count > 0)
                changesList.Add(new ReviewGroup(group.Title, names));
        }
        Changes = changesList;

        // StopsWorking
        var totals = TotalsCalculator.For(sel);
        StopsWorking = totals.StopsWorking;

        // Warnings
        var warnings = WarningEngine.For(sel);
        DependencyWarnings = warnings
            .Where(w => w.Kind == WarningKind.Dependency)
            .Select(w => w.Message)
            .Distinct()
            .ToList();

        var irrWarnings = warnings.Where(w => w.Kind == WarningKind.Irreversible).ToList();
        var irrSwitches = irrWarnings
            .Select(w => _catalog.Switches.FirstOrDefault(s => s.Id == w.SwitchId))
            .Where(s => s is not null)
            .Cast<SwitchDef>()
            .ToList();
        IrreversibleNames = irrSwitches.Select(s => s.Name).ToList();
        IrreversibleWarning = IrreversibleNames.Count > 0
            ? $"{IrreversibleNames.Count} of these changes can't be undone without reinstalling."
            : null;

        // Script names
        ScriptNames = _catalog.Switches
            .Where(s => sel.IsOn(s.Id) &&
                        (sel.Mode == TargetMode.TunePc ? s.Live : s.Image).OfType<Script>().Any())
            .Select(s => s.Name)
            .ToList();

        // Safety note
        SafetyNote = sel.Mode == TargetMode.TunePc
            ? "NitrOS makes a Windows restore point first and saves every old value, so you can undo."
            : null;

        UpdateGoStatus();
    }

    private void UpdateGoStatus()
    {
        if (_draft is not null && _request is not null)
        {
            _request = RequestFromDraft(_draft) ?? _request;
        }

        if (_request is null || _request.Selection.On.Count == 0)
        {
            GoUnavailableReason = null;
            Go.NotifyCanExecuteChanged();
            return;
        }

        var sessionCheck = SessionGuard.Check(_session);
        if (!sessionCheck.Ok)
        {
            GoUnavailableReason = sessionCheck.Reason;
            Go.NotifyCanExecuteChanged();
            return;
        }

        if (!_gate.CanChange(_probe))
        {
            GoUnavailableReason = TuneEngine.BusyReason;
            Go.NotifyCanExecuteChanged();
            return;
        }

        var runner = _runners.For(_request.Kind);
        var readiness = runner.CheckReady(_request);
        if (!readiness.Ready)
        {
            GoUnavailableReason = readiness.Reason;
            Go.NotifyCanExecuteChanged();
            return;
        }

        GoUnavailableReason = null;
        Go.NotifyCanExecuteChanged();
    }

    private bool CanExecuteGo()
    {
        if (_request is null || _request.Selection.On.Count == 0) return false;
        var sessionCheck = SessionGuard.Check(_session);
        if (!sessionCheck.Ok) return false;
        if (!_gate.CanChange(_probe)) return false;
        var runner = _runners.For(_request.Kind);
        return runner.CheckReady(_request).Ready;
    }

    private async Task ExecuteGoAsync()
    {
        if (_request is null) return;

        if (IrreversibleNames.Count > 0)
        {
            var confirmed = await _dialogs.ConfirmAsync(new ConfirmRequest(
                Title: "Some changes can't be undone",
                Message: IrreversibleWarning ?? $"{IrreversibleNames.Count} of these changes can't be undone without reinstalling.",
                Items: IrreversibleNames,
                ConfirmText: "Go ahead",
                CancelText: "Cancel"));

            if (!confirmed) return;
        }

        _navigator.GoTo(ScreenKeys.Working, _request);
    }
}
