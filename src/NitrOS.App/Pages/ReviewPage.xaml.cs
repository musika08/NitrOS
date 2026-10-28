using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class ReviewPage : Page
{
    public ReviewViewModel? ViewModel => DataContext as ReviewViewModel;

    public ReviewPage()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            if (GoButton.IsEnabled)
            {
                GoButton.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ReviewViewModel vm)
        {
            DataContext = vm;
        }
    }
}
