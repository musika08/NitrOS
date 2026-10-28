using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;
using NitrOS.Core.Model;
using NitrOS.Live;

namespace NitrOS.App.Logic.Screens;

public sealed class WorkingViewModel : ObservableObject, IScreen
{
    private readonly Catalog _catalog;
    private readonly RunnerSet _runners;
    private readonly RunGate _gate;
    private readonly Navigator _navigator;

    private double _percent;
    private string _status = "";
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public ScreenKey Key => ScreenKeys.Working;

    public double Percent
    {
        get => _percent;
        private set => SetProperty(ref _percent, value);
    }

    public ObservableCollection<string> Log { get; } = [];

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public IRelayCommand Cancel { get; }

    internal Task? CurrentRunTask { get; private set; }

    public WorkingViewModel(
        Catalog catalog,
        RunnerSet runners,
        RunGate gate,
        Navigator navigator)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _runners = runners ?? throw new ArgumentNullException(nameof(runners));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        Cancel = new RelayCommand(ExecuteCancel, () => _isRunning);
    }

    public void OnShown(object? argument)
    {
        if (argument is not RunRequest req) return;

        var lease = _gate.TryEnter();
        if (lease is null)
        {
            var busyResult = new RunResult(
                req.Kind,
                Refused: true,
                RefusedReason: TuneEngine.BusyReason,
                Cancelled: false,
                Switches: new Dictionary<string, SwitchOutcome>(),
                MayBeChanged: [],
                Problems: [])
            {
                Request = req
            };
            _navigator.ResetTo(ScreenKeys.Done, busyResult);
            return;
        }

        _isRunning = true;
        Cancel.NotifyCanExecuteChanged();
        Percent = 0;
        Log.Clear();
        Status = "Applying changes...";
        _cts = new CancellationTokenSource();

        CurrentRunTask = RunInternalAsync(req, lease, _cts.Token);
    }

    private void ExecuteCancel()
    {
        if (!_isRunning || _cts is null) return;
        _cts.Cancel();
        Status = "Stopping after the current change. That switch is put back.";
    }

    private async Task RunInternalAsync(RunRequest req, IDisposable lease, CancellationToken ct)
    {
        RunResult result;
        try
        {
            var runner = _runners.For(req.Kind);
            var prog = new DirectProgress(p =>
            {
                if (p.Total > 0)
                {
                    Percent = Math.Clamp((double)p.Done / p.Total * 100.0, 0, 100);
                }
                if (p.LogLine is not null)
                {
                    Log.Add(p.LogLine);
                }
            });

            result = await runner.RunAsync(req, prog, ct);
        }
        catch (Exception ex)
        {
            result = new RunResult(
                req.Kind,
                Refused: true,
                RefusedReason: ex.Message,
                Cancelled: false,
                Switches: new Dictionary<string, SwitchOutcome>(),
                MayBeChanged: [],
                Problems: []);
        }
        finally
        {
            _isRunning = false;
            Cancel.NotifyCanExecuteChanged();
            lease.Dispose();
        }

        result = result with { Request = req };

        foreach (var (switchId, outcome) in result.Switches)
        {
            var name = _catalog.Switches.FirstOrDefault(s => s.Id == switchId)?.Name ?? switchId;
            var prefix = outcome switch
            {
                SwitchOutcome.Applied => "Done: ",
                SwitchOutcome.PartlyApplied => "Partly done: ",
                SwitchOutcome.Failed => "Failed: ",
                SwitchOutcome.Skipped => "Skipped: ",
                _ => "Done: "
            };
            Log.Add($"{prefix}{name}");
        }

        _navigator.ResetTo(ScreenKeys.Done, result);
    }

    private sealed class DirectProgress(Action<RunProgress> onReport) : IProgress<RunProgress>
    {
        public void Report(RunProgress value) => onReport(value);
    }
}
