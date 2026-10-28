using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;

namespace NitrOS.App.Logic.Screens;

public sealed class DriftViewModel : ObservableObject, IScreen
{
    private readonly Catalog _catalog;
    private readonly Draft _draft;
    private readonly Navigator _navigator;

    private DriftFindings? _findings;
    private IReadOnlyList<string> _driftedNames = [];
    private IReadOnlyList<string> _couldNotCheck = [];

    public ScreenKey Key => ScreenKeys.Drift;

    public string PromptText => "Windows turned these back on since NitrOS changed them. Turn them off again?";

    public IReadOnlyList<string> DriftedNames
    {
        get => _driftedNames;
        private set => SetProperty(ref _driftedNames, value);
    }

    public IReadOnlyList<string> CouldNotCheck
    {
        get => _couldNotCheck;
        private set => SetProperty(ref _couldNotCheck, value);
    }

    public IRelayCommand TurnOffAgain { get; }
    public IRelayCommand NotNow { get; }

    public DriftViewModel(
        Catalog catalog,
        Draft draft,
        Navigator navigator)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        TurnOffAgain = new RelayCommand(ExecuteTurnOffAgain);
        NotNow = new RelayCommand(() => _navigator.ResetTo(ScreenKeys.Start));
    }

    public void OnShown(object? argument)
    {
        if (argument is not DriftFindings findings) return;
        _findings = findings;

        DriftedNames = findings.Drifted
            .Select(id => _catalog.Switches.FirstOrDefault(s => s.Id == id)?.Name ?? id)
            .ToList();

        CouldNotCheck = findings.Errors
            .Select(err =>
            {
                var name = _catalog.Switches.FirstOrDefault(s => s.Id == err.SwitchId)?.Name ?? err.SwitchId;
                return $"{name}: {err.Error}";
            })
            .ToList();
    }

    private void ExecuteTurnOffAgain()
    {
        if (_findings is null || _findings.Drifted.Count == 0) return;

        _draft.Start(TargetMode.TunePc);
        _draft.UseIds(_findings.Drifted);
        _navigator.GoTo(ScreenKeys.Review);
    }
}
