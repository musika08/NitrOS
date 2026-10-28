using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Help;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Startup;
using NitrOS.App.Logic.Storage;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Live;
using NitrOS.Live.Launch;
using NitrOS.Live.Measuring;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.App.Logic;

public sealed class AppComposition
{
    public Navigator Navigator { get; }
    public RunGate RunGate { get; }
    public AppContextInfo AppContext { get; }
    public AppStateStore AppState { get; }
    public IMachineMeter MachineMeter { get; }
    public IWindowsVersion WindowsVersion { get; }
    public IDriftService DriftService { get; }
    public IHealthService HealthService { get; }
    public IUndoService UndoService { get; }
    public RunnerSet Runners { get; }
    public Draft Draft { get; }
    public Catalog? Catalog { get; }
    public IDialogs Dialogs { get; }
    public ISessionFacts Session { get; }
    public ITuneLockProbe TuneLockProbe { get; }
    public IRestarter Restarter { get; }
    public IXboxRestorer XboxRestorer { get; }
    public IAfterUpdateTask AfterUpdateTask { get; }
    public AfterRestartLaunch AfterRestartLaunch { get; }
    public JournalAccess JournalAccess { get; }
    public string ExePath { get; }

    public AppComposition(
        Navigator navigator,
        RunGate runGate,
        AppContextInfo appContext,
        AppStateStore appState,
        IMachineMeter machineMeter,
        IWindowsVersion windowsVersion,
        IDriftService driftService,
        IHealthService healthService,
        IUndoService undoService,
        RunnerSet runners,
        Draft draft,
        Catalog? catalog,
        IDialogs dialogs,
        ISessionFacts session,
        ITuneLockProbe tuneLockProbe,
        IRestarter restarter,
        IXboxRestorer xboxRestorer,
        IAfterUpdateTask afterUpdateTask,
        AfterRestartLaunch afterRestartLaunch,
        JournalAccess journalAccess,
        string exePath,
        IDeviceUpdates? deviceUpdates = null)
    {
        Navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        RunGate = runGate ?? throw new ArgumentNullException(nameof(runGate));
        AppContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
        AppState = appState ?? throw new ArgumentNullException(nameof(appState));
        MachineMeter = machineMeter ?? throw new ArgumentNullException(nameof(machineMeter));
        WindowsVersion = windowsVersion ?? throw new ArgumentNullException(nameof(windowsVersion));
        DriftService = driftService ?? throw new ArgumentNullException(nameof(driftService));
        HealthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        UndoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        Runners = runners ?? throw new ArgumentNullException(nameof(runners));
        Draft = draft ?? throw new ArgumentNullException(nameof(draft));
        Catalog = catalog;
        Dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        TuneLockProbe = tuneLockProbe ?? throw new ArgumentNullException(nameof(tuneLockProbe));
        Restarter = restarter ?? throw new ArgumentNullException(nameof(restarter));
        XboxRestorer = xboxRestorer ?? throw new ArgumentNullException(nameof(xboxRestorer));
        AfterUpdateTask = afterUpdateTask ?? throw new ArgumentNullException(nameof(afterUpdateTask));
        AfterRestartLaunch = afterRestartLaunch ?? throw new ArgumentNullException(nameof(afterRestartLaunch));
        JournalAccess = journalAccess ?? throw new ArgumentNullException(nameof(journalAccess));
        ExePath = exePath ?? throw new ArgumentNullException(nameof(exePath));
        DeviceUpdates = deviceUpdates;
    }

    public IDeviceUpdates? DeviceUpdates { get; }

    public void RegisterScreens()
    {
        var dummyCatalog = Catalog ?? new Catalog([], new CardMeasurements());

        Navigator.Register(ScreenKeys.Start, () => new StartViewModel(Navigator, Draft, AppContext));
        var dummyBackend = ImageIsoBackend.CreateReal(dummyCatalog, AppContext.Device);
        var dummyPicker = new DummyFilePicker();
        var dummyRegistry = new NoOpRegistry();
        Navigator.Register(ScreenKeys.IsoSource, () => new IsoSourceViewModel(Navigator, Draft, dummyBackend, dummyPicker, Dialogs, new IsoBuildIndex(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "app"), _ => FolderState.Missing, _ => { }, Session), dummyRegistry));
        Navigator.Register(ScreenKeys.Cards, () => new CardsViewModel(Navigator, Draft, AppContext));
        Navigator.Register(ScreenKeys.Switches, () => new SwitchesViewModel(Navigator, Draft, AppContext));
        Navigator.Register(ScreenKeys.Review, () => new ReviewViewModel(dummyCatalog, Draft, Runners, Session, TuneLockProbe, RunGate, Dialogs, Navigator));
        Navigator.Register(ScreenKeys.Working, () => new WorkingViewModel(dummyCatalog, Runners, RunGate, Navigator));
        Navigator.Register(ScreenKeys.Done, () => new DoneViewModel(dummyCatalog, Navigator, Dialogs, Restarter, XboxRestorer, MachineMeter, RunGate));
        Navigator.Register(ScreenKeys.Undo, () => new UndoViewModel(JournalAccess, UndoService, dummyCatalog, Dialogs, Restarter, RunGate, Navigator));
        Navigator.Register(ScreenKeys.Drift, () => new DriftViewModel(dummyCatalog, Draft, Navigator));
        Navigator.Register(ScreenKeys.Health, () => new HealthViewModel(JournalAccess, HealthService, UndoService, dummyCatalog, AppState, AfterRestartLaunch, MachineMeter, Navigator));
        Navigator.Register(ScreenKeys.Myths, () => new MythsViewModel(Navigator));
        Navigator.Register(ScreenKeys.Settings, () => new SettingsViewModel(AfterUpdateTask, AppState, Session, ExePath, Navigator));
        Navigator.Register(ScreenKeys.Blocked, () => new BlockedViewModel());
        if (DeviceUpdates != null)
        {
            Navigator.Register(ScreenKeys.DeviceUpdates, () => new DeviceUpdatesViewModel(DeviceUpdates, Dialogs, RunGate, TuneLockProbe, Session, Restarter));
        }
    }

    public static AppComposition CreateReal(string exePath, IDialogs dialogs) =>
        CreateReal(exePath, dialogs, null);

    internal static AppComposition CreateReal(string exePath, IDialogs dialogs, string? customProgramData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentNullException.ThrowIfNull(dialogs);

        var exeDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(exeDir)) exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var catalogDir = Path.Combine(exeDir, "catalog");
        if (!Directory.Exists(catalogDir))
        {
            var cur = new DirectoryInfo(exeDir);
            while (cur is not null && !Directory.Exists(Path.Combine(cur.FullName, "catalog")))
            {
                cur = cur.Parent;
            }
            if (cur is not null) catalogDir = Path.Combine(cur.FullName, "catalog");
        }
        var loadResult = CatalogLoader.Load(catalogDir);
        var catalog = loadResult.Catalog;
        var errors = loadResult.Errors;

        var device = DeviceDetector.Detect(new WinDeviceFacts());
        var session = new WinSessionFacts();
        var meter = new WinMachineMeter();
        var mem = meter.Memory();
        var sessionCheck = SessionGuard.Check(session);

        var appContext = new AppContextInfo(catalog, errors, device, mem, sessionCheck)
        {
            ConsoleUserSid = session.ConsoleUserSid
        };

        var gate = new RunGate();
        var navigator = new Navigator(gate);
        var draft = new Draft(catalog ?? new Catalog([], new CardMeasurements()), device);
        var version = new WinWindowsVersion();
        var restarter = new WinRestarter();
        var healthProbe = new WinHealthProbe();
        var afterUpdateTask = new WinAfterUpdateTask();
        var afterRestart = new AfterRestartLaunch(new WinRegistry());

        var programData = customProgramData ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var backupsDir = customProgramData is not null ? Path.Combine(customProgramData, "NitrOS", "backups") : WinSystems.BackupPath;
        var appDir = Path.Combine(programData, "NitrOS", "app");

        var journalAccess = new JournalAccess(backupsDir, session, BackupFolder.Check, p => BackupFolder.Ensure(p));
        var appStateStore = new AppStateStore(appDir, BackupFolder.Check, p => BackupFolder.Ensure(p), session);

        var lockProbe = new LiveTuneLockProbe();
        var tuneRunner = new TuneRunner(
            new LiveTuneBackendFactory(WinSystems.CreateAdapters),
            session,
            version,
            lockProbe,
            journalAccess,
            meter,
            appStateStore,
            afterRestart,
            exePath,
            Thread.Sleep);

        var isoIndex = new IsoBuildIndex(appDir, BackupFolder.Check, p => BackupFolder.Ensure(p), session);
        var isoBackend = ImageIsoBackend.CreateReal(catalog ?? new Catalog([], new CardMeasurements()), device);
        var isoRunner = new IsoRunner(isoBackend, session, isoIndex);
        var runners = new RunnerSet([tuneRunner, isoRunner]);
        var xboxRestorer = new UnprovenXboxRestorer();

        var undoService = new LiveUndoService(session, journalAccess, WinSystems.CreateAdapters, appStateStore);
        var healthService = new LiveHealthService(healthProbe);
        var driftService = new LiveDriftService(journalAccess, WinSystems.CreateAdapters, session);

        var downloadsDir = customProgramData is not null ? Path.Combine(customProgramData, "NitrOS", "downloads") : Path.Combine(programData, "NitrOS", "downloads");
        var deviceReader = new NitrOS.Updates.Reading.DeviceReader(
            new NitrOS.Updates.Windows.WinIdentity(),
            new NitrOS.Updates.Windows.WinInstalledDevices(),
            new NitrOS.Updates.Windows.WinFirmwareTable());
        var updateSources = NitrOS.Updates.Sources.SourceSet.Default(
            new NitrOS.Updates.Windows.WinUpdateAgent(),
            NitrOS.Updates.Net.HttpFactory.Create(),
            NitrOS.Updates.Firmware.FirmwareMap.Shipped());
        var updateChecker = new NitrOS.Updates.Checking.UpdateChecker(
            deviceReader,
            updateSources,
            NitrOS.Updates.KnownBad.KnownBadList.Shipped(),
            NitrOS.Updates.Firmware.FirmwareMap.Shipped(),
            new NitrOS.Updates.Reading.WindowsUpdatePolicy(new WinRegistry()),
            TimeProvider.System);
        var updateInstaller = new NitrOS.Updates.Installing.UpdateInstaller(
            new NitrOS.Updates.Windows.WinUpdateAgent(),
            new NitrOS.Updates.Installing.Downloader(NitrOS.Updates.Net.HttpFactory.Create()),
            new NitrOS.Updates.Windows.WinFileVerifier(),
            new NitrOS.Updates.Windows.WinPackageRunner(),
            new NitrOS.Live.Windows.WinPowerSource(),
            new NitrOS.Live.Windows.WinRestorePoint(),
            NitrOS.Updates.KnownBad.KnownBadList.Shipped(),
            NitrOS.Updates.Installing.SourcePolicy.Shipped(),
            downloadsDir,
            p => BackupFolder.Ensure(p));
        var liveUpdates = new LiveDeviceUpdates(
            updateChecker,
            updateInstaller,
            new NitrOS.Updates.Windows.WinPackageRunner(),
            new NitrOS.Updates.Windows.WinUnelevatedLauncher(),
            new NitrOS.Live.Windows.WinPowerSource());

        return new AppComposition(
            navigator,
            gate,
            appContext,
            appStateStore,
            meter,
            version,
            driftService,
            healthService,
            undoService,
            runners,
            draft,
            catalog,
            dialogs,
            session,
            lockProbe,
            restarter,
            xboxRestorer,
            afterUpdateTask,
            afterRestart,
            journalAccess,
            exePath,
            liveUpdates);
    }

    public static AppComposition CreateSmoke(string exePath, IDialogs dialogs) =>
        CreateSmoke(exePath, dialogs, null);

    internal static AppComposition CreateSmoke(string exePath, IDialogs dialogs, string? customProgramData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentNullException.ThrowIfNull(dialogs);

        var exeDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(exeDir)) exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var catalogDir = Path.Combine(exeDir, "catalog");
        if (!Directory.Exists(catalogDir))
        {
            var cur = new DirectoryInfo(exeDir);
            while (cur is not null && !Directory.Exists(Path.Combine(cur.FullName, "catalog")))
            {
                cur = cur.Parent;
            }
            if (cur is not null) catalogDir = Path.Combine(cur.FullName, "catalog");
        }
        var loadResult = CatalogLoader.Load(catalogDir);
        var catalog = loadResult.Catalog;
        var errors = loadResult.Errors;

        var device = DeviceDetector.Detect(new WinDeviceFacts());
        var session = new WinSessionFacts();
        var meter = new WinMachineMeter();
        var mem = meter.Memory();
        var sessionCheck = SessionGuard.Check(session);

        var appContext = new AppContextInfo(catalog, errors, device, mem, sessionCheck)
        {
            ConsoleUserSid = session.ConsoleUserSid
        };

        var gate = new RunGate();
        var navigator = new Navigator(gate);
        var draft = new Draft(catalog ?? new Catalog([], new CardMeasurements()), device);
        var version = new WinWindowsVersion();

        var programData = customProgramData ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var backupsDir = customProgramData is not null ? Path.Combine(customProgramData, "NitrOS", "backups") : WinSystems.BackupPath;
        var appDir = Path.Combine(programData, "NitrOS", "app");

        var journalAccess = new JournalAccess(backupsDir, session, _ => FolderState.Missing, _ => throw new InvalidOperationException("Smoke test cannot create folders."));
        var appStateStore = new AppStateStore(appDir, _ => FolderState.Missing, _ => throw new InvalidOperationException("Smoke test cannot write app state."), session);

        var lockProbe = new LiveTuneLockProbe();
        var tuneRunner = new ForbiddenRunner(RunKind.Tune);
        var isoRunner = new ForbiddenRunner(RunKind.Iso);
        var runners = new RunnerSet([tuneRunner, isoRunner]);
        var xboxRestorer = new UnprovenXboxRestorer();

        var undoService = new ForbiddenUndoService();
        var restarter = new ForbiddenRestarter();
        var afterUpdateTask = new ForbiddenAfterUpdateTask();
        var afterRestart = new AfterRestartLaunch(new NoOpRegistry());
        var healthService = new EmptyHealthService();
        var driftService = new EmptyDriftService();

        var smokeUpdates = new SmokeDeviceUpdates();

        return new AppComposition(
            navigator,
            gate,
            appContext,
            appStateStore,
            meter,
            version,
            driftService,
            healthService,
            undoService,
            runners,
            draft,
            catalog,
            dialogs,
            session,
            lockProbe,
            restarter,
            xboxRestorer,
            afterUpdateTask,
            afterRestart,
            journalAccess,
            exePath,
            smokeUpdates);
    }

    private sealed class DummyFilePicker : IFilePicker
    {
        public Task<string?> PickIsoToOpenAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickIsoToSaveAsync(string suggestedName) => Task.FromResult<string?>(null);
    }

    private sealed class ForbiddenUndoService : IUndoService
    {
        public Task<UndoOutcome> UndoAsync(string journalPath, IReadOnlyCollection<string>? switchIds, CancellationToken ct) =>
            throw new InvalidOperationException("Smoke test cannot undo.");
    }

    private sealed class ForbiddenRestarter : IRestarter
    {
        public void Restart() =>
            throw new InvalidOperationException("Smoke test cannot restart.");
    }

    private sealed class ForbiddenAfterUpdateTask : IAfterUpdateTask
    {
        public bool IsRegistered() => false;
        public void Register(string exePath, string userSid) =>
            throw new InvalidOperationException("Smoke test cannot register tasks.");
        public void Remove() =>
            throw new InvalidOperationException("Smoke test cannot remove tasks.");
    }

    private sealed class NoOpRegistry : IRegistry
    {
        public RegValue? GetValue(LiveHive h, string key, string name, RegView v) => null;
        public void SetValue(LiveHive h, string key, string name, RegValue value, RegView v) { }
        public void DeleteValue(LiveHive h, string key, string name, RegView v) { }
        public IReadOnlyList<string> GetValueNames(LiveHive h, string key, RegView v) => [];
        public RegTree? ExportKey(LiveHive h, string key, RegView v) => null;
        public void ImportKey(LiveHive h, RegTree tree, RegView v) { }
        public void DeleteKey(LiveHive h, string key, RegView v) { }
        public bool KeyExists(LiveHive h, string key, RegView v) => false;
        public bool CanWrite(LiveHive h, string key, RegView v) => false;
    }

    private sealed class EmptyDriftService : IDriftService
    {
        public Task<DriftFindings> CheckAsync(CancellationToken ct) =>
            Task.FromResult(new DriftFindings([], [], null));
    }

    private sealed class EmptyHealthService : IHealthService
    {
        public Task<HealthReport> RunAsync(IReadOnlySet<string> appliedSwitchIds, CancellationToken ct) =>
            Task.FromResult(new HealthReport([]));
    }

    private sealed class SmokeDeviceUpdates : IDeviceUpdates
    {
        public IPowerSource Power => new WinPowerSource();

        public Task<NitrOS.Updates.Checking.CheckReport> CheckAsync(IProgress<NitrOS.Updates.Checking.CheckProgress>? progress, CancellationToken ct)
        {
            var device = new NitrOS.Updates.Model.DeviceInfo(
                NitrOS.Updates.Model.MakerId.Asus,
                "ASUSTeK COMPUTER INC.",
                "ROG Xbox Ally X RC73XA_RC73XA",
                "RC73XA",
                "RC73XA.319",
                null,
                [],
                []);

            var biosItem = new NitrOS.Updates.Model.UpdateItem(
                "fw:68e0fa36-7c6c-4861-ba1b-d10fbde48a1c",
                "BIOS Update for Windows",
                NitrOS.Updates.Model.UpdateKind.Bios,
                "319",
                "319",
                null,
                null,
                null,
                "asus",
                "ASUS");

            var report = new NitrOS.Updates.Checking.CheckReport(
                device,
                [new NitrOS.Updates.Merging.MergedItem(biosItem, NitrOS.Updates.Merging.ItemState.UpToDate, null, [])],
                [],
                [],
                false,
                DateTimeOffset.UtcNow);

            return Task.FromResult(report);
        }

        public Task<NitrOS.Updates.Installing.InstallOutcome> InstallAsync(NitrOS.Updates.Installing.InstallRequest request, IProgress<NitrOS.Updates.Installing.InstallProgress>? progress, CancellationToken ct) =>
            throw new InvalidOperationException("Smoke test cannot install updates.");

        public void StartHandedOff(string path) =>
            throw new InvalidOperationException("Smoke test cannot start updaters.");

        public bool OpenPage(Uri page) =>
            throw new InvalidOperationException("Smoke test cannot open browser pages.");

        public bool ShowInFolder(string path) =>
            throw new InvalidOperationException("Smoke test cannot show folders.");
    }
}
