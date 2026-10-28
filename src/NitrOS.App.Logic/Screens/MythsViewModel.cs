using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Help;
using NitrOS.App.Logic.Navigation;

namespace NitrOS.App.Logic.Screens;

public sealed class MythsViewModel : ObservableObject, IScreen
{
    private readonly Navigator _navigator;

    public ScreenKey Key => ScreenKeys.Myths;

    public string Intro => "These popular tweaks are not in NitrOS, because testing shows they do nothing on Windows 11 or cause harm.";

    public IReadOnlyList<Myth> All => Myths.All;

    public IRelayCommand Back { get; }

    public MythsViewModel(Navigator navigator)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        Back = new RelayCommand(navigator.GoBack);
    }

    public void OnShown(object? argument)
    {
    }
}
