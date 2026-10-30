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

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 750));
        AppWindow.Closing += OnAppWindowClosing;
        SizeChanged += OnWindowSizeChanged;
    }

    private const double RailCollapsed = 64;
    private const double RailExpanded = 200;
    private bool _railExpanded;

    private void OnRailToggleClicked(object sender, RoutedEventArgs e) => SetRailExpanded(!_railExpanded);

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        // Narrow windows keep the rail collapsed so content never clips.
        if (e.Size.Width < 900 && _railExpanded) SetRailExpanded(false);
    }

    private void SetRailExpanded(bool expanded)
    {
        _railExpanded = expanded;
        SidebarColumn.Width = new GridLength(expanded ? RailExpanded : RailCollapsed);
        var vis = expanded ? Visibility.Visible : Visibility.Collapsed;
        BrandLabel.Visibility = vis;
        LabelOverview.Visibility = vis;
        LabelTune.Visibility = vis;
        LabelUpdates.Visibility = vis;
        LabelHealth.Visibility = vis;
        LabelUndo.Visibility = vis;
        LabelMyths.Visibility = vis;
        LabelSettings.Visibility = vis;
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
            UpdateActiveNav(screen.Key);
        });
    }

    private void UpdateActiveNav(ScreenKey key)
    {
        var activeBrush = Application.Current.Resources.TryGetValue("SubtleFillColorSecondaryBrush", out var b)
            ? (Brush)b
            : new SolidColorBrush(Windows.UI.Color.FromArgb(30, 128, 128, 128));

        var transparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        NavOverview.Background = (key == ScreenKeys.Start) ? activeBrush : transparentBrush;
        NavTune.Background = (key == ScreenKeys.Cards || key == ScreenKeys.Switches || key == ScreenKeys.Review || key == ScreenKeys.Working || key == ScreenKeys.Done) ? activeBrush : transparentBrush;
        NavUpdates.Background = (key == ScreenKeys.DeviceUpdates) ? activeBrush : transparentBrush;
        NavHealth.Background = (key == ScreenKeys.Health) ? activeBrush : transparentBrush;
        NavUndo.Background = (key == ScreenKeys.Undo) ? activeBrush : transparentBrush;
        NavMyths.Background = (key == ScreenKeys.Myths) ? activeBrush : transparentBrush;
        NavSettings.Background = (key == ScreenKeys.Settings) ? activeBrush : transparentBrush;

        HeaderTitleText.Text = key.Name switch
        {
            "start" => "Overview",
            "cards" => "Tuning Profiles",
            "switches" => "Tuning Switches",
            "review" => "Review Changes",
            "working" => "Applying Changes",
            "done" => "Completed",
            "device-updates" => "Device Updates",
            "health" => "System Health",
            "undo" => "Undo Changes",
            "myths" => "Myths We Tested",
            "settings" => "Settings",
            "iso-source" => "Build Slim ISO",
            _ => "NitrOS"
        };
    }

    private void OnNavOverviewClicked(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null && _navigator.IsRegistered(ScreenKeys.Start))
            _navigator.ResetTo(ScreenKeys.Start);
    }

    private void OnNavTuneClicked(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null && _navigator.IsRegistered(ScreenKeys.Cards))
            _navigator.GoTo(ScreenKeys.Cards);
    }

    private void OnNavUpdatesClicked(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null && _navigator.IsRegistered(ScreenKeys.DeviceUpdates))
            _navigator.GoTo(ScreenKeys.DeviceUpdates);
    }

    private void OnNavHealthClicked(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null && _navigator.IsRegistered(ScreenKeys.Health))
            _navigator.GoTo(ScreenKeys.Health);
    }

    private void OnNavUndoClicked(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null && _navigator.IsRegistered(ScreenKeys.Undo))
            _navigator.GoTo(ScreenKeys.Undo);
    }

    private void OnNavMythsClicked(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null && _navigator.IsRegistered(ScreenKeys.Myths))
            _navigator.GoTo(ScreenKeys.Myths);
    }

    private void OnNavSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null && _navigator.IsRegistered(ScreenKeys.Settings))
            _navigator.GoTo(ScreenKeys.Settings);
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
                var msg = _gate.BusyMessage ?? "NitrOS is changing this PC. Press Cancel on the Working screen first.";
                await _dialogs.ShowAsync("NitrOS", msg);
            }
        }
    }
}
