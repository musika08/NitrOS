using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Startup;
using NitrOS.Core.Selecting;

namespace NitrOS.App.Logic.Screens;

public sealed record StartEntry(
    string Title,
    string Description,
    IRelayCommand Open,
    bool Enabled,
    string? DisabledReason);

public sealed class StartViewModel : ObservableObject, IScreen
{
    private readonly Navigator _navigator;
    private readonly Draft _draft;
    private readonly AppContextInfo _context;
    private string? _banner;
    private IReadOnlyList<StartEntry> _entries = [];

    public ScreenKey Key => ScreenKeys.Start;

    public string? Banner
    {
        get => _banner;
        set => SetProperty(ref _banner, value);
    }

    public IReadOnlyList<StartEntry> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    public StartViewModel(Navigator navigator, Draft draft, AppContextInfo context)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _context = context ?? throw new ArgumentNullException(nameof(context));

        if (!_context.Session.Ok)
            _banner = _context.Session.Reason;

        RefreshEntries();
    }

    public void OnShown(object? argument)
    {
        RefreshEntries();
    }

    private void RefreshEntries()
    {
        bool sessionOk = _context.Session.Ok;
        string? sessionReason = _context.Session.Reason;

        bool deviceUpdatesRegistered = _navigator.IsRegistered(ScreenKeys.DeviceUpdates);
        bool deviceUpdatesEnabled = sessionOk && deviceUpdatesRegistered;
        string? deviceUpdatesDisabled = !deviceUpdatesRegistered
            ? Texts.DeviceUpdatesLater
            : (!sessionOk ? sessionReason : null);

        var tuneCmd = new RelayCommand(() =>
        {
            _draft.Start(TargetMode.TunePc);
            _navigator.GoTo(ScreenKeys.Cards);
        });

        var isoCmd = new RelayCommand(() =>
        {
            _draft.Start(TargetMode.BuildIso);
            _navigator.GoTo(ScreenKeys.Cards);
        });

        var undoCmd = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Undo));
        var updatesCmd = new RelayCommand(() => _navigator.GoTo(ScreenKeys.DeviceUpdates));
        var mythsCmd = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Myths));
        var settingsCmd = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Settings));

        Entries =
        [
            new StartEntry("Tune this PC", "Tune Windows for gaming and battery life on this PC.", tuneCmd, sessionOk, sessionOk ? null : sessionReason),
            new StartEntry("Build a slim ISO", "Build an unattended installation image for another PC.", isoCmd, sessionOk, sessionOk ? null : sessionReason),
            new StartEntry("Undo changes", "Put settings back from previous NitrOS runs.", undoCmd, sessionOk, sessionOk ? null : sessionReason),
            new StartEntry("Device updates", "Check for ASUS driver and firmware updates.", updatesCmd, deviceUpdatesEnabled, deviceUpdatesDisabled),
            new StartEntry("Myths we tested", "Read what we measured and debunked.", mythsCmd, true, null),
            new StartEntry("Settings", "Configure NitrOS options.", settingsCmd, sessionOk, sessionOk ? null : sessionReason)
        ];
    }
}
