using Microsoft.UI.Xaml;
using NitrOS.App.Logic;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Startup;
using NitrOS.App.Pages;

namespace NitrOS.App;

public partial class App : Application
{
    private ShellWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var mode = LaunchMode.Parse(cmdArgs);

        var exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;

        var window = new ShellWindow();
        var dialogs = new ContentDialogs(window);

        var composition = mode is LaunchMode.SmokeMode
            ? AppComposition.CreateSmoke(exePath, dialogs)
            : AppComposition.CreateReal(exePath, dialogs);
        composition.RegisterScreens();

        var decision = await StartupRouter.DecideAsync(
            mode,
            composition.AppContext,
            composition.AppState,
            composition.MachineMeter,
            composition.WindowsVersion,
            composition.DriftService);

        if (!decision.ShowWindow)
        {
            window.Close();
            Environment.Exit(0);
            return;
        }

        var pageMap = new PageMap();
        pageMap.Add(ScreenKeys.Start, typeof(StartPage));
        pageMap.Add(ScreenKeys.Cards, typeof(CardsPage));
        pageMap.Add(ScreenKeys.Switches, typeof(SwitchesPage));
        pageMap.Add(ScreenKeys.Review, typeof(ReviewPage));
        pageMap.Add(ScreenKeys.Working, typeof(WorkingPage));
        pageMap.Add(ScreenKeys.Done, typeof(DonePage));
        pageMap.Add(ScreenKeys.Undo, typeof(UndoPage));
        pageMap.Add(ScreenKeys.Drift, typeof(DriftPage));
        pageMap.Add(ScreenKeys.Health, typeof(HealthPage));
        pageMap.Add(ScreenKeys.Myths, typeof(MythsPage));
        pageMap.Add(ScreenKeys.Settings, typeof(SettingsPage));
        pageMap.Add(ScreenKeys.Blocked, typeof(BlockedPage));

        window.Initialize(composition.Navigator, composition.RunGate, pageMap, dialogs);
        _window = window;

        composition.Navigator.GoTo(decision.First, decision.Argument);

        window.Activate();

        if (mode is LaunchMode.SmokeMode smoke)
        {
            _ = SmokeRunner.RunAsync(window, composition, smoke.ResultPath);
            return;
        }

        if (decision.RunDriftInBackground)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var findings = await composition.DriftService.CheckAsync(CancellationToken.None);
                    if (findings.Drifted.Count > 0)
                    {
                        window.DispatcherQueue.TryEnqueue(() =>
                        {
                            composition.Navigator.GoTo(ScreenKeys.Drift, findings);
                        });
                    }
                }
                catch
                {
                    // Ignore background drift failure
                }
            });
        }
    }
}
