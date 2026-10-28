using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class WorkingPage : Page
{
    public WorkingViewModel? ViewModel => DataContext as WorkingViewModel;

    public WorkingPage()
    {
        InitializeComponent();
        Loaded += (s, e) => CancelButton.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is WorkingViewModel vm)
        {
            DataContext = vm;
        }
    }
}
