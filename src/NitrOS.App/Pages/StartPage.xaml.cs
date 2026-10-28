using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class StartPage : Page
{
    public StartViewModel? ViewModel => DataContext as StartViewModel;

    public StartPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is StartViewModel vm)
        {
            DataContext = vm;
        }
    }
}
