using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Formatting;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Storage;
using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Launch;
using NitrOS.Live.Measuring;

namespace NitrOS.App.Logic.Screens;

public sealed record HealthRow(
    string Name,
    bool Ok,
    string? Detail,
    string? LikelySwitchName,
    IAsyncRelayCommand? UndoLikely);

public sealed class HealthViewModel : ObservableObject, IScreen
{
    private readonly JournalAccess _journals;
    private readonly IHealthService _healthService;
    private readonly IUndoService _undoService;
    private readonly Catalog _catalog;
    private readonly AppStateStore _state;
    private readonly AfterRestartLaunch? _afterRestart;
    private readonly IMachineMeter _meter;
    private readonly Navigator _navigator;

    private IReadOnlyList<HealthRow> _items = [];
    private string? _afterRestartLine;

    public ScreenKey Key => ScreenKeys.Health;

    public IReadOnlyList<HealthRow> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public string? AfterRestartLine
    {
        get => _afterRestartLine;
        private set => SetProperty(ref _afterRestartLine, value);
    }

    public IAsyncRelayCommand CheckAgain { get; }
    public IRelayCommand Back { get; }

    public HealthViewModel(
        JournalAccess journals,
        IHealthService healthService,
        IUndoService undoService,
        Catalog catalog,
        AppStateStore state,
        AfterRestartLaunch? afterRestart,
        IMachineMeter meter,
        Navigator navigator)
    {
        _journals = journals ?? throw new ArgumentNullException(nameof(journals));
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _afterRestart = afterRestart;
        _meter = meter ?? throw new ArgumentNullException(nameof(meter));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        CheckAgain = new AsyncRelayCommand(RunCheckAsync);
        Back = new RelayCommand(ExecuteBack);
    }

    public void OnShown(object? argument)
    {
        _ = RunCheckAsync();
    }

    public async Task RunCheckAsync()
    {
        var mine = _journals.ListMine().Mine;

        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, j) in mine)
        {
            foreach (var e in j.Entries)
            {
                if (e.Outcome == ActionOutcome.Applied) applied.Add(e.SwitchId);
                else if (e.Outcome == ActionOutcome.RolledBack) applied.Remove(e.SwitchId);
            }
        }

        var report = await _healthService.RunAsync(applied, CancellationToken.None);

        var snap = _meter.ReadOnce();
        var appState = _state.Read();

        if (appState.Health?.Before is not null)
        {
            AfterRestartLine = $"Memory in use after restart: {Plain.Memory(snap.MemoryInUseMb)} ({Plain.Memory(appState.Health.Before.MemoryInUseMb)} before NitrOS). Processes: {snap.Processes} ({appState.Health.Before.Processes} before).";
        }
        else
        {
            AfterRestartLine = $"Memory in use after restart: {Plain.Memory(snap.MemoryInUseMb)}. Processes: {snap.Processes}.";
        }

        var rows = new List<HealthRow>();
        foreach (var item in report.Items)
        {
            string? likelySwitchName = null;
            IAsyncRelayCommand? undoCmd = null;

            if (item.LikelySwitchId is not null)
            {
                var sw = _catalog.Switches.FirstOrDefault(s => s.Id == item.LikelySwitchId);
                likelySwitchName = sw?.Name ?? item.LikelySwitchId;

                if (!item.Ok && sw is not null && sw.Reversible)
                {
                    // Find newest journal on this account with an Applied entry for this switch
                    string? targetJournalPath = null;
                    foreach (var (path, j) in mine)
                    {
                        var entry = j.Entries.LastOrDefault(e => string.Equals(e.SwitchId, item.LikelySwitchId, StringComparison.OrdinalIgnoreCase));
                        if (entry is { Outcome: ActionOutcome.Applied })
                        {
                            targetJournalPath = path;
                            break;
                        }
                    }

                    if (targetJournalPath is not null)
                    {
                        var switchIdToUndo = item.LikelySwitchId;
                        var path = targetJournalPath;
                        undoCmd = new AsyncRelayCommand(async () =>
                        {
                            await _undoService.UndoAsync(path, [switchIdToUndo], CancellationToken.None);
                            await RunCheckAsync();
                        });
                    }
                }
            }

            rows.Add(new HealthRow(item.Name, item.Ok, item.Detail, likelySwitchName, undoCmd));
        }

        Items = rows;

        if (report.AllOk)
        {
            ClearPending();
        }
    }

    private void ExecuteBack()
    {
        ClearPending();
        _navigator.GoTo(ScreenKeys.Start);
    }

    private void ClearPending()
    {
        try
        {
            _state.ClearHealthPending();
            _afterRestart?.Clear();
        }
        catch
        {
            // best-effort
        }
    }
}
