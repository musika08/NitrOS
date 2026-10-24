namespace NitrOS.Live.Systems;

public sealed record LiveSystems(
    IRegistry Registry,
    IServiceConfig Services,
    ITaskScheduler Tasks,
    IPowerConfig Power,
    IAppx Appx,
    IDism Dism,
    IUninstaller Uninstaller,
    IFileSystem Files,
    IScriptRunner Scripts,
    IRestorePoint RestorePoint);
