using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class SwitchesPage : Page
{
    public SwitchesViewModel? ViewModel => DataContext as SwitchesViewModel;

    public SwitchesPage()
    {
        InitializeComponent();
        Loaded += (s, e) => SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SwitchesViewModel vm)
        {
            DataContext = vm;
        }
    }
}
