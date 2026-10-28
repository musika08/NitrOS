using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class UndoPage : Page
{
    public UndoViewModel? ViewModel => DataContext as UndoViewModel;

    public UndoPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is UndoViewModel vm)
        {
            DataContext = vm;
        }
    }

    private void OnUndoRunClicked(object sender, RoutedEventArgs e)
    {
        ViewModel?.UndoEverything.Execute(null);
    }

    private void OnUndoSwitchClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SwitchSummary summary } && ViewModel is not null)
        {
            ViewModel.UndoSwitch.Execute(summary);
        }
    }
}
