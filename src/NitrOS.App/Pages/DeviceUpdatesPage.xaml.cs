using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Pages;

public sealed partial class DeviceUpdatesPage : Page
{
    public DeviceUpdatesViewModel? ViewModel => DataContext as DeviceUpdatesViewModel;

    public DeviceUpdatesPage()
    {
        InitializeComponent();
        Loaded += (s, e) => CheckButton.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DeviceUpdatesViewModel vm)
        {
            DataContext = vm;
        }
    }
}
