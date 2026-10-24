using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

public static class WinSystems
{
    /// <summary>The journal folder, %ProgramData%\NitrOS\backups, locked down by <see cref="Create"/>.</summary>
    public static string BackupPath => BackupFolder.DefaultPath;

    /// <summary>
    /// The real Windows adapters. Also creates (or tightens) the backup folder so only Administrators and SYSTEM can
    /// write journals. Needs an elevated process.
    /// </summary>
    public static LiveSystems Create()
    {
        BackupFolder.Ensure(BackupPath);
        return new LiveSystems(
            Registry: new WinRegistry(),
            Services: new WinServiceConfig(),
            Tasks: new WinTaskScheduler(),
            Power: new WinPowerConfig(),
            Appx: new WinAppx(),
            Dism: new WinDism(),
            Uninstaller: new WinUninstaller(),
            Files: new WinFileSystem(),
            Scripts: new WinScriptRunner(),
            RestorePoint: new WinRestorePoint());
    }
}
