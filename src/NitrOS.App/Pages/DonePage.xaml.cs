using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class DonePage : Page
{
    public DoneViewModel? ViewModel => DataContext as DoneViewModel;

    public DonePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DoneViewModel vm)
        {
            DataContext = vm;
        }
    }
}
