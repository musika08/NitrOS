using NitrOS.Live;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.Cli;

/// <summary>
/// Everything the runner reads from the machine, so tests can swap it. <paramref name="Systems"/> is a factory that is only
/// called by commands that change or inspect the PC's settings (apply, undo, drift); plan, detect, health and journals never call it.
/// </summary>
public sealed record CliEnvironment(
    Func<LiveSystems> Systems,
    IDeviceFacts Device,
    ISessionFacts Session,
    IHealthProbe Health,
    string CatalogDir,
    string JournalDir)
{
    /// <summary>The real PC. WinSystems.Create (which creates the locked-down backup folder) runs only when a command asks for the systems.</summary>
    public static CliEnvironment Real() => new(
        WinSystems.Create,
        new WinDeviceFacts(),
        new WinSessionFacts(),
        new WinHealthProbe(),
        Path.Combine(AppContext.BaseDirectory, "catalog"),
        WinSystems.BackupPath);
}
