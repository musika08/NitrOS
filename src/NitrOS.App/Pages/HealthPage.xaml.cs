using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class HealthPage : Page
{
    public HealthViewModel? ViewModel => DataContext as HealthViewModel;

    public HealthPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is HealthViewModel vm)
        {
            DataContext = vm;
        }
    }
}
