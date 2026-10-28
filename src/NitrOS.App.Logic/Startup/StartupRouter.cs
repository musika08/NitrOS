using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Storage;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;

namespace NitrOS.App.Logic.Startup;

public sealed record StartupDecision(
    bool ShowWindow,
    ScreenKey First,
    object? Argument,
    bool RunDriftInBackground);

public static class StartupRouter
{
    public static async Task<StartupDecision> DecideAsync(
        LaunchMode mode,
        AppContextInfo info,
        AppStateStore state,
        IMachineMeter meter,
        IWindowsVersion version,
        IDriftService drift)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(drift);

        // Catalog failed to load -> Blocked with errors
        if (info.CatalogErrors.Count > 0)
        {
            return new StartupDecision(true, ScreenKeys.Blocked, info.CatalogErrors, false);
        }

        switch (mode)
        {
            case LaunchMode.AfterUpdateMode:
            {
                var appState = state.Read();
                var currentBuild = version.FullBuild;
                if (string.Equals(currentBuild, appState.LastCheckedBuild, StringComparison.OrdinalIgnoreCase))
                {
                    return new StartupDecision(false, ScreenKeys.Start, null, false);
                }

                var findings = await drift.CheckAsync(CancellationToken.None);
                state.SetLastCheckedBuild(currentBuild);

                if (findings.Drifted.Count > 0 || findings.Errors.Count > 0 || findings.FolderProblem is not null)
                {
                    return new StartupDecision(true, ScreenKeys.Drift, findings, false);
                }

                return new StartupDecision(false, ScreenKeys.Start, null, false);
            }

            case LaunchMode.AfterRestartMode:
            {
                var appState = state.Read();
                if (appState.Health is null ||
                    string.IsNullOrEmpty(info.ConsoleUserSid) ||
                    !string.Equals(info.ConsoleUserSid, appState.Health.UserSid, StringComparison.OrdinalIgnoreCase))
                {
                    return new StartupDecision(false, ScreenKeys.Start, null, false);
                }

                return new StartupDecision(true, ScreenKeys.Health, null, false);
            }

            case LaunchMode.SmokeMode:
            {
                return new StartupDecision(true, ScreenKeys.Start, null, false);
            }

            case LaunchMode.NormalMode:
            default:
            {
                var appState = state.Read();
                if (appState.Health is not null &&
                    !string.IsNullOrEmpty(info.ConsoleUserSid) &&
                    string.Equals(info.ConsoleUserSid, appState.Health.UserSid, StringComparison.OrdinalIgnoreCase) &&
                    appState.Health.At < meter.LastBoot())
                {
                    return new StartupDecision(true, ScreenKeys.Health, null, false);
                }

                bool runDrift = info.Session.Ok;
                return new StartupDecision(true, ScreenKeys.Start, null, runDrift);
            }
        }
    }
}
