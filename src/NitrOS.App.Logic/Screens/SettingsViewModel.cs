using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Storage;
using NitrOS.Live;
using NitrOS.Live.Launch;

namespace NitrOS.App.Logic.Screens;

public sealed class SettingsViewModel : ObservableObject, IScreen
{
    private readonly IAfterUpdateTask _afterUpdateTask;
    private readonly AppStateStore _state;
    private readonly ISessionFacts _session;
    private readonly string _exePath;
    private readonly Navigator _navigator;

    private bool _checkAfterUpdates;

    public ScreenKey Key => ScreenKeys.Settings;

    public string Description =>
        "Check after Windows updates: when you sign in after Windows updated itself, NitrOS checks whether anything was turned back on, then closes. Off by default.";

    public bool CheckAfterUpdates
    {
        get => _checkAfterUpdates;
        private set => SetProperty(ref _checkAfterUpdates, value);
    }

    public IAsyncRelayCommand ToggleCheckAfterUpdates { get; }
    public IRelayCommand Back { get; }

    public SettingsViewModel(
        IAfterUpdateTask afterUpdateTask,
        AppStateStore state,
        ISessionFacts session,
        string exePath,
        Navigator navigator)
    {
        _afterUpdateTask = afterUpdateTask ?? throw new ArgumentNullException(nameof(afterUpdateTask));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _exePath = exePath ?? throw new ArgumentNullException(nameof(exePath));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        _checkAfterUpdates = _afterUpdateTask.IsRegistered();

        ToggleCheckAfterUpdates = new AsyncRelayCommand(ExecuteToggleCheckAfterUpdatesAsync);
        Back = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Start));
    }

    public void OnShown(object? argument)
    {
        CheckAfterUpdates = _afterUpdateTask.IsRegistered();
    }

    private Task ExecuteToggleCheckAfterUpdatesAsync()
    {
        if (_checkAfterUpdates)
        {
            _afterUpdateTask.Remove();
            _state.SetCheckAfterUpdates(false);
            CheckAfterUpdates = false;
        }
        else
        {
            _afterUpdateTask.Register(_exePath, _session.ProcessUserSid);
            _state.SetCheckAfterUpdates(true);
            CheckAfterUpdates = true;
        }
        return Task.CompletedTask;
    }
}
