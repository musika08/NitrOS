using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;

namespace NitrOS.App;

public sealed partial class ShellWindow : Window
{
    private Navigator? _navigator;
    private RunGate? _gate;
    private PageMap? _pageMap;
    private IDialogs? _dialogs;
    private GamepadFocusBridge? _gamepadBridge;

    public ShellWindow()
    {
        InitializeComponent();
        Title = "NitrOS";
        SystemBackdrop = new MicaBackdrop();

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 720));
        AppWindow.Closing += OnAppWindowClosing;
    }

    public void Initialize(Navigator navigator, RunGate gate, PageMap pageMap, IDialogs dialogs)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _pageMap = pageMap ?? throw new ArgumentNullException(nameof(pageMap));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _navigator.Navigated += OnNavigated;
        _gamepadBridge = new GamepadFocusBridge(this, _navigator);
    }

    private void OnNavigated(object? sender, IScreen screen)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_pageMap is null || _navigator is null) return;
            var pageType = _pageMap.For(screen.Key);
            ContentFrame.Navigate(pageType, screen);
            BackButton.Visibility = _navigator.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        _navigator?.GoBack();
    }

    private async void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_gate is { IsBusy: true })
        {
            args.Cancel = true;
            if (_dialogs is not null)
            {
                await _dialogs.ShowAsync("NitrOS", "NitrOS is changing this PC. Press Cancel on the Working screen first.");
            }
        }
    }
}
