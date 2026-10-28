using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class BlockedPage : Page
{
    public BlockedViewModel? ViewModel => DataContext as BlockedViewModel;

    public BlockedPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is BlockedViewModel vm)
        {
            DataContext = vm;
        }
    }
}
