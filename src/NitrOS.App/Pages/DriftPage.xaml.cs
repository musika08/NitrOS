using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class DriftPage : Page
{
    public DriftViewModel? ViewModel => DataContext as DriftViewModel;

    public DriftPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DriftViewModel vm)
        {
            DataContext = vm;
        }
    }
}
