using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class MythsPage : Page
{
    public MythsViewModel? ViewModel => DataContext as MythsViewModel;

    public MythsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is MythsViewModel vm)
        {
            DataContext = vm;
        }
    }
}
