using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            DataContext = vm;
        }
    }

    private void OnToggleToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts && ViewModel is not null)
        {
            if (ts.IsOn != ViewModel.CheckAfterUpdates && ViewModel.ToggleCheckAfterUpdates.CanExecute(null))
            {
                ViewModel.ToggleCheckAfterUpdates.Execute(null);
            }
        }
    }
}
