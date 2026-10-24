# Winhance settings catalog (facts only)

Source: github.com/memstechtips/Winhance, branch main, commit SHA `db80ecfcf6762a7b856860930d9f4ee17bf1e67c`.
License: PolyForm Shield. Nothing here is copied code. Only identifiers (registry paths, value names, service names, appx names, task paths, power GUID aliases) and short plain-language descriptions are recorded.

Files read (all under `src/Winhance.Core/Features/`): `Optimize/Models/{GamingAndPerformance,Power,Privacy,Update,Notification,Sound}Optimizations.cs`, `Optimize/Models/PowerTemplates.cs`, `Customize/Models/{Explorer,StartMenu,Taskbar,WindowsTheme}Customizations.cs`, `SoftwareApps/Models/{WindowsAppDefinitions,CapabilityDefinitions,OptionalFeatureDefinitions,EdgeRemovalScript,OneDriveRemovalScript}.cs`, `Common/Models/PowerPlanDefinitions.cs`, plus `src/Winhance.UI/Features/Common/Resources/Configs/Winhance_Recommended_Config.winhance` (the "recommended" preset) and `Infrastructure/.../PowerService.cs` (skimmed).

Counts: 418 SettingDefinition entries (Gaming/Performance 116, Power 48, Privacy+Security 90, Explorer 87, Taskbar 28, Notifications 15, Start menu 13, Updates 12, Sound 7, Theme 2), plus 57 Windows app/AI entries (Appx removals), 10 Windows Capabilities, and 6+ Windows Optional Features (WSL, Hyper-V platform/full/tools, .NET 3.5, Sandbox, Recall). About 490 items total.

Abbreviations: HKLM/HKCU. `CV` = `Software\Microsoft\Windows\CurrentVersion`. `POL` = `HKLM\SOFTWARE\Policies\Microsoft\Windows`. Service start types: 2 Automatic, 3 Manual, 4 Disabled. Winhance changes services by writing `HKLM\SYSTEM\CurrentControlSet\Services\<name>\Start` (not via sc.exe), so a reboot is needed to fully apply. "Rec" is Winhance's own recommended value. Effect scale: real / small / none / unknown, for idle RAM or process count.

How Winhance models toggles: a setting has an on-value and off-value per registry key; combo settings map each option to values. Many items carry a "subjective preference" flag (their wording: taste, not universally recommended) or "group policy" flag (writes under `Policies` keys, which also greys out the Settings page).

---

## 1. Services (Gaming/Performance page, group "System Services")

All written as `HKLM\SYSTEM\CurrentControlSet\Services\<svc>\Start`. Windows default is Automatic or Manual per service. Winhance's recommended option is shown. Manual still lets Windows start a service on demand by trigger, so Manual lowers idle count only for services that are not triggered at boot; Disabled blocks it entirely.

| Plain name | What it changes | Category | Idle RAM/process effect | Risk / what breaks |
|---|---|---|---|---|
| SysMain (Superfetch) | `Services\SysMain\Start`; Rec = Disabled (4) | memory/process | real (one svchost-hosted service, plus stops preloading RAM) | Slower app launches on HDD. Winhance warns SSD-only. |
| Prefetch | `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters\EnablePrefetcher` = 0 (default 3). Child setting of SysMain | memory/process | small | Slower first launches; Rec config leaves it unchanged |
| Windows Search indexing | `Services\WSearch\Start`; Rec = Manual (3) | memory/process | real (SearchIndexer.exe) | Slower file search, Start menu search for files degraded |
| Print Spooler | `Services\Spooler\Start`; Rec = Manual | memory/process | small | Printing fails until started |
| Connected User Experiences and Telemetry (DiagTrack) | `Services\DiagTrack\Start`; Rec = Manual | privacy | small | Diagnostics/feedback features reduced |
| Connected Devices Platform | `Services\CDPSvc\Start` and `Services\CDPUserSvc\Start`; Rec = Manual | memory/process | small to real (per-user service adds a process) | Phone Link, Nearby Sharing, cross-device clipboard degrade |
| Program Compatibility Assistant | `Services\PcaSvc\Start`; Rec = Manual | other | small | No compatibility prompts |
| Windows Error Reporting | `Services\WerSvc\Start`; Rec = Manual | privacy | small | No crash reports sent |
| Geolocation | `Services\lfsvc\Start`; Rec = Manual | privacy | small | Location-dependent apps fail |
| Retail Demo | `Services\RetailDemo\Start`; Rec = Manual | other | none | None for normal PCs |
| Windows Insider Service | `Services\wisvc\Start`; Rec = Manual | other | small | Insider builds cannot be managed |
| Phone Service | `Services\PhoneSvc\Start`; Rec = Manual | other | small | Telephony state features |
| Wallet Service | `Services\WalletService\Start`; Rec = Manual | other | small | Wallet/NFC pay |
| Smart Card services | `Services\SCardSvr\Start`, `ScDeviceEnum\Start`, `SCPolicySvc\Start`; Rec = Manual | other | small | Smart card login/VPN certs |
| Downloaded Maps Manager | `Services\MapsBroker\Start`; Rec = Manual | other | small | Offline maps |
| Fax | `Services\Fax\Start`; Rec = Disabled | other | none | Fax unavailable |
| WMP Network Sharing | `Services\WMPNetworkSvc\Start`; Rec = Disabled | other | small | Media library sharing off |
| Windows Mixed Reality OpenXR | `Services\MixedRealityOpenXRSvc\Start`; Rec = Manual | other | small | WMR/VR headsets |
| Mobile Hotspot | `Services\icssvc\Start`; Rec = Manual | other | small | Hotspot sharing may not start. Relevant to a handheld |
| SMS Router | `Services\SmsRouter\Start`; Rec = Manual | other | none | SMS routing |
| Parental Controls | `Services\WpcMonSvc\Start`; Rec = Manual | other | none | Family Safety enforcement |
| Payments and NFC/SE Manager | `Services\SEMgrSvc\Start`; Rec = Manual | other | none | NFC payments |
| Spot Verifier | `Services\svsvc\Start`; Rec = Manual | other | none | On-demand file corruption verify |
| Remote Access Connection Manager | `Services\RasMan\Start`; Rec = Manual | other | small | VPN/dial-up break if it cannot start |
| Remote Access Auto Connection Manager | `Services\RasAuto\Start`; Rec = Manual | other | small | Auto-connect to remote networks |
| Remote Desktop Services | `Services\TermService\Start`; Rec = Manual | other | small | Inbound RDP |
| Remote Desktop Configuration | `Services\SessionEnv\Start`; Rec = Manual | other | small | RDP config |
| RDP UserMode Port Redirector | `Services\UmRdpService\Start`; Rec = Manual | other | small | RDP drive/device redirect |
| Xbox Live Auth Manager | `Services\XblAuthManager\Start`; Rec = Manual | gaming | small | Game Pass / Xbox sign-in; critical on an Xbox Ally |
| Xbox Live Game Save | `Services\XblGameSave\Start`; Rec = Manual | gaming | small | Xbox cloud saves |
| Xbox Live Networking | `Services\XboxNetApiSvc\Start`; Rec = Manual | gaming | small | Xbox multiplayer networking |
| Windows Biometric | `Services\WbioSrvc\Start`; Rec = Manual | other | small | Windows Hello fingerprint/face |
| Touch Keyboard and Handwriting | `Services\TabletInputService\Start`; `HKCU\Software\Microsoft\input\IsInputAppPreloadEnabled`; plus a script that renames `C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe` to a `.old.exe` name (undone on enable). Rec config uses the Disabled option | memory/process | real (TextInputHost.exe) | Emoji panel, touch keyboard and handwriting may break. Dangerous on a touchscreen handheld |
| Telephony (TAPI) | `Services\TapiSrv\Start`; Rec = Manual | other | small | Phone Link audio relay |
| Sensor Monitoring | `Services\SensrSvc\Start`; Rec = Manual | other | small | Auto-brightness / rotation on handheld |
| Sensor Data | `Services\SensorDataService\Start`; Rec = Manual | other | small | Same, sensor data for apps |
| Windows AI Fabric (WSAIFabricSvc) | `Services\WSAIFabricSvc\Start`; Rec = Disabled | memory/process | small to real | Windows AI features |
| Services pipe timeout | `HKLM\SYSTEM\CurrentControlSet\Control\ServicesPipeTimeout` = 30000 (ms; default 60000) | other | none | Slow services may time out at boot |
| Svchost split threshold | `HKLM\SYSTEM\CurrentControlSet\Control\SvcHostSplitThresholdInKB`; combo of default (3670016) and values up to 32 GB-equivalent. Rec in config = index 0 (default) | memory/process | real if raised above installed RAM (services share fewer svchost.exe) | One crashing service can take down its host group; less isolation |

## 2. Scheduled tasks (Gaming/Performance, all disabled in the recommended config)

Task paths under the Task Scheduler library. Effect on idle RAM is none or small (tasks run briefly); the gain is less background CPU/disk activity and telemetry.

| Plain name | Task path | Category | Effect | Risk |
|---|---|---|---|---|
| Compatibility Appraiser | `\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser` | privacy | small | Upgrade readiness data missing |
| Program Data Updater | `\Microsoft\Windows\Application Experience\ProgramDataUpdater` | privacy | small | Compat DB stale |
| Startup App Task | `\Microsoft\Windows\Application Experience\StartupAppTask` | privacy | small | Startup-impact tracking off |
| MARE Backup | `\Microsoft\Windows\Application Experience\MareBackup` | other | none | None noted |
| CEIP Consolidator | `\Microsoft\Windows\Customer Experience Improvement Program\Consolidator` | privacy | small | None |
| USB CEIP | `\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip` | privacy | small | None |
| Disk Diagnostic Data Collector | `\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector` | privacy | small | SMART data to MS off |
| Feedback DmClient | `\Microsoft\Windows\Feedback\Siuf\DmClient` | privacy | small | None |
| Feedback DmClient scenario download | `\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload` | privacy | small | None |
| WER queue reporting | `\Microsoft\Windows\Windows Error Reporting\QueueReporting` | privacy | small | None |
| SQM metrics | `\Microsoft\Windows\PI\Sqm-Tasks` | privacy | small | None |
| Maps update | `\Microsoft\Windows\Maps\MapsUpdateTask` | other | none | Offline maps stale |
| AutoChk proxy | `\Microsoft\Windows\Autochk\Proxy` | other | none | Winhance itself says consider keeping |
| Family Safety monitor | `\Microsoft\Windows\Shell\FamilySafetyMonitor` | other | none | Family Safety stops |
| Power efficiency diagnostics | `\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem` | power | none | Energy reports off |
| Windows AI (Recall) tasks | `\Microsoft\Windows\WindowsAI\RecallConfiguration` and `\Microsoft\Windows\WindowsAI\RecallPipeline` | privacy | small | Recall disabled |
| Office Actions Server | `\Microsoft\Office\Office Actions Server` | privacy | small | Office AI actions |

## 3. Gaming and performance (non-service)

| Plain name | What it changes | Category | Effect | Risk |
|---|---|---|---|---|
| Game Mode | `HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled` = 1 | gaming latency | none | None |
| Enhance pointer precision | `HKCU\Control Panel\Mouse\MouseSpeed` = "0" (off). Left unchanged in rec config | gaming latency | none | Cursor feel changes |
| Mouse hover time | `HKCU\Control Panel\Mouse\MouseHoverTime`, combo 1 to 400 ms (Rec 1) | UI/looks | none | Tooltips fire too eagerly |
| Startup delay for apps | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize\StartupDelayInMSec` = 0 | other | none | Slower, busier boot |
| Background app permission | GPO `LetAppsRunInBackground` under `HKCU` and `HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy`; rec index 2 (force deny) | memory/process | real (stops UWP background processes) | Store apps no longer update live tiles/notify in background; Xbox and messaging apps may not alert |
| Storage Sense | GPO `HKLM\SOFTWARE\Policies\Microsoft\Windows\StorageSense\AllowStorageSenseGlobal` = 0 (and HKCU) | other | none | No auto temp cleanup |
| Search entire file system | `HKCU\...\Explorer\Search\Preferences\WholeFileSystem` = 0 | other | none | Search scope |
| WebView2 in Windows Search | `HKLM\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260` values `EnabledState`, `EnabledStateOptions`, `Variant`, `VariantPayload` (feature-flag override). Turned off in rec config | memory/process | real (Description says it removes the Edge/WebView2 processes spawned by search) | Search flyout may render differently |
| Wallpaper compression | `HKCU\Control Panel\Desktop\JPEGImportQuality` = 100 | UI/looks | none | Larger wallpaper memory |
| Menu show delay | `HKCU\Control Panel\Desktop\MenuShowDelay` = "0" | UI/looks | none | None |
| Alt+Tab filter | `HKCU\...\Explorer\Advanced\MultiTaskingAltTabFilter` = 3 (windows only, no Edge tabs) | UI/looks | none | None |
| Foreground/background CPU split | `HKLM\System\CurrentControlSet\Control\PriorityControl\Win32PrioritySeparation`; options "Programs" (decimal 38, Rec) or "Background Services" (24) | gaming latency | none | Background work gets less CPU |
| System responsiveness | `HKLM\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\SystemResponsiveness` = 10 (default 20) | gaming latency | none | Background audio/video tasks get less CPU |
| Games CPU priority | `...\SystemProfile\Tasks\Games\Priority` = 6 | gaming latency | none | Low |
| Games scheduling category | `...\SystemProfile\Tasks\Games\Scheduling Category` = "High" | gaming latency | none | Low |
| Games GPU priority | `...\SystemProfile\Tasks\Games\GPU Priority` = 8 | gaming latency | none | Low |
| Hardware-accelerated GPU scheduling | `HKLM\System\CurrentControlSet\Control\GraphicsDrivers\HwSchMode` = 2 | gaming latency | none | Driver-dependent glitches |
| Windowed game optimization (flip model), VRR optimization, Auto HDR | All three write bits inside one string value `HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings`. Flip model on in rec config; VRR and Auto HDR off | gaming latency | none | Exact string layout unclear |
| Legacy NVIDIA sharpening | `HKLM\Software\NVIDIA Corporation\Global\FTS\EnableGR535` = 0 | gaming latency | none | NVIDIA-only; irrelevant on AMD Ally |
| Fullscreen optimizations | `HKCU\System\GameConfigStore\GameDVR_FSEBehaviorMode` = 0 | gaming latency | none | Some games change behaviour |
| Desktop composition | `HKCU\Software\Microsoft\Windows\DWM\CompositionPolicy` (set in rec config) | UI/looks | unknown | Unclear exact value; DWM cannot truly be turned off on Win11 |
| Auto color management | `HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore\AutoColorManagementEnabled` = 0 | UI/looks | none | Colors on wide-gamut screens |
| MPO off | `HKLM\SOFTWARE\Microsoft\Windows\Dwm\OverlayTestMode`. On in rec config | gaming latency | none | More GPU composition work, may raise power |
| Hardware overlays off | `HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\DisableOverlays` | gaming latency | none | Same |
| MPO min FPS | `HKLM\SOFTWARE\Microsoft\Windows\Dwm\OverlayMinFPS` | gaming latency | none | Same |
| Network throttling | `HKLM\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\NetworkThrottlingIndex` (default 10; rec config applies its recommended) | gaming latency | none | Low |
| Nagle's algorithm | `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\<adapter>\TcpAckFrequency` and `TCPNoDelay` | gaming latency | none | Slight extra network overhead |
| DNS server | Script: `Set-DnsClientServerAddress` on all adapters (Cloudflare, Google, Quad9, OpenDNS variants), optional DNS-over-HTTPS templates | other | none | Overrides router/ISP DNS |
| VBS (Virtualization Based Security) off | `HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard`: `EnableVirtualizationBasedSecurity`=0, `RequirePlatformSecurityFeatures`=0, `Locked`=0 | gaming latency / memory | small (frees hypervisor memory/CPU) | Weakens security. Left ON in rec config |
| Memory integrity (HVCI) off | `...\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity` values `Enabled`, `Locked`, `WasEnabledBy` = 0 | gaming latency | small | Weakens driver protection. Left ON in rec config |
| Xbox Game DVR / capture | `HKCU\System\GameConfigStore\GameDVR_Enabled`=0, `HKCU\...\CurrentVersion\GameDVR\AppCaptureEnabled`=0, GPO `HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR\AllowGameDVR`=0 | gaming latency | small | No clip recording/screenshots from Game Bar |
| Game Bar controller button | `HKCU\Software\Microsoft\GameBar\UseNexusForGameBarEnabled` = 0 | gaming | none | Xbox button no longer opens Game Bar; important on an Xbox Ally |
| Game Bar tips | `HKCU\Software\Microsoft\GameBar\ShowStartupPanel` = 0 | UI/looks | none | None |
| System Restore protection | Script `Enable-ComputerRestore` / `Disable-ComputerRestore` on C: | other | none | Rec config keeps it on |

### Visual effects (Gaming/Performance page, all "subjective")
Effect on RAM: small (DWM/explorer animations), none on process count. Risk: looks only. Most are left unchanged in rec config.

| Item | Registry |
|---|---|
| Visual effects mode (preset combo) | `HKCU\...\Explorer\VisualEffects\VisualFXSetting` (rec index 3; meaning of index unclear) |
| Animate controls, menu fade/slide, tooltip fade, menu item fade, mouse shadow, window shadows, combo-box slide, smooth-scroll list boxes | bits in `HKCU\Control Panel\Desktop\UserPreferencesMask` (shared) |
| Minimize/maximize animation | `HKCU\Control Panel\Desktop\WindowMetrics\MinAnimate` |
| Taskbar animations | `...\Explorer\Advanced\TaskbarAnimations` |
| Aero Peek | `HKCU\Software\Microsoft\Windows\DWM\EnableAeroPeek` |
| Taskbar thumbnails cache | `HKCU\Software\Microsoft\Windows\DWM\AlwaysHibernateThumbnails` |
| Show thumbnails vs icons | `...\Explorer\Advanced\IconsOnly` |
| Translucent selection | `...\Explorer\Advanced\ListviewAlphaSelect` |
| Drag full windows | `HKCU\Control Panel\Desktop\DragFullWindows` |
| Font smoothing | `HKCU\Control Panel\Desktop\FontSmoothing` |
| Desktop icon label shadows | `...\Explorer\Advanced\ListviewShadow` |

### Accessibility hotkeys (left as-is in rec config)
`HKCU\Control Panel\Accessibility\StickyKeys\Flags`, `Keyboard Response\Flags` (FilterKeys), `ToggleKeys\Flags`, `MouseKeys\Flags`, `HighContrast\Flags`, and `HKCU\Software\Microsoft\Narrator\NoRoam\WinEnterLaunchEnabled`. Effect none; they stop popups when mashing keys (useful for gamepad/keyboard play).

## 4. Power (Optimize > Power)

Winhance does not write a registry for most of these; it writes values into the active power scheme (AC and DC separately) through the native power API and `powercfg`-style setting GUIDs. It also exposes a plan picker with five predefined plans:

| Plan | GUID |
|---|---|
| Power saver | a1841308-3541-4fab-bc81-f71556f20b4a |
| Balanced | 381b4222-f694-41f0-9685-ff5bb260df2e |
| High performance | 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c |
| Ultimate Performance | e9a42b02-d5df-448d-aa00-03f14749eb61 |
| "Winhance Power Plan" (custom, fixed GUID 57696e68-616e-6365-506f-776572000000) | imported/created on demand and then filled with the recommended values below; its creation source (file or duplicate of a built-in) is unclear |

The rec config leaves almost all power settings unselected (it only stores a few AC/DC choices), so these are opt-in.

| Plain name | Alias / identifier | Winhance recommended AC / DC | Category | Effect | Risk |
|---|---|---|---|---|---|
| Display off timeout | SUB_VIDEO / VIDEOIDLE | Never / 5 min | power | none | Battery |
| Hard disk off | DISKIDLE | not stated (config uses 0 AC / 600 s DC) | power | none | Wake delay |
| JavaScript timer frequency | GUID 4c793e7d-a264-42e1-87d3-7a0d2f523ccd | Maximum performance both | power | none | Battery |
| Desktop slideshow | GUID 309dce9b-bef4-4119-9921-a851fb12f0f4 | Available / Paused | power | small | None |
| Wireless adapter power saving | GUID 12bbebe6-58d6-4636-95bb-3217ef867c1a | Max performance / Medium saving | power | none | Battery |
| Sleep timeout | STANBYIDLE | Never / 15 min | power | none | Battery |
| Wake timers | RTCWAKE | Disable / Disable | power | none | Scheduled wake tasks stop |
| Hibernation enable | `HKLM\SYSTEM\CurrentControlSet\Control\Power\HibernateEnabled` | no recommendation set | power | small (frees hiberfil.sys disk, not RAM) | Hibernate gone |
| Hibernate after | HIBERNATEIDLE | not stated | power | none | |
| Hybrid sleep | HYBRIDSLEEP | not stated | power | none | |
| Fast Startup | `HKLM\SYSTEM\ControlSet001\Control\Session Manager\Power\HiberbootEnabled` | no recommendation set | power | none | Cold boots become slower but cleaner (frees kernel session between boots) |
| Start menu Hibernate / Lock / Sleep entries | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings\ShowHibernateOption`, `ShowLockOption`, `ShowSleepOption` | none | UI/looks | none | |
| USB hub selective suspend timeout | GUID 0853a681-27c8-4100-a2fd-82013e970683 | not stated | power | none | |
| USB selective suspend | GUID 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 | Disabled / Enabled | power | none | Controllers may draw more power |
| USB 3 link power management | GUID d4e98f31-5ffe-4ce1-be31-1b38b384c009 | Off / Moderate savings | power | none | |
| Intel graphics power plan | GUID 3619c3f2-afb2-4afc-b0e9-e7fef372de36 | Max performance / Balanced | power | none | Intel only |
| Power button, sleep button, lid close actions | PBUTTONACTION area / LIDACTION | Do nothing / Do nothing / Sleep | power | none | |
| PCIe link state power management | ASPM | Off / Maximum savings | power | none | |
| CPU min / max state | PROCTHROTTLEMIN / PROCTHROTTLEMAX | no recommendation text (defaults list min 100 AC, 5 DC; max 100) | power | none | Heat, battery |
| System cooling policy | GUID 94d3a615-a899-4ac5-ae2b-e4d8f634367f | Active / Active | power | none | Fan noise |
| Processor boost mode | PERFBOOSTMODE | Aggressive / Enabled | power | none | Heat |
| Performance increase / decrease policy | PERFINCPOL / PERFDECPOL | Rocket / Ideal (increase); Single / Rocket (decrease) | power | none | |
| Core parking min / max cores | CPMINCORES / CPMAXCORES | min 0 default, max 100 | power | none | Parked cores wake slowly if min is low |
| Energy performance preference | PERFEPP | AC 0 (max perf) / DC 50 | power | none | |
| Performance increase / decrease thresholds | PERFINCTHRESHOLD / PERFDECTHRESHOLD | AC 10 / DC 30; AC 8 / DC 20 | power | none | |
| Power throttling | `HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling\PowerThrottlingOff` = 1 | no recommendation text | power | none | Background apps no longer throttled: higher power use on handheld |
| Media sharing sleep, video playback quality bias, when playing video | GUIDs 03680956-93bc-4294-bba6-4e0f09bb717f, 10778347-1370-4ee0-8bbd-33bdacaade49, 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 | performance-biased both | power | none | |
| Battery notifications, actions, levels (low, critical, reserve) | BATFLAGSLOW, BATFLAGSCRIT, BATACTIONLOW, BATACTIONCRIT, BATLEVELOW (10), BATLEVELCRIT (5), reserve GUID f3c5027d-cd16-4930-aa6b-90db844a8f00 (7) | Notifications on; low action nothing; critical action Hibernate | power | none | |
| AMD power slider overlay | GUID 7ec1751b-60ed-4588-afb5-9819d3d77d90 | Best Performance / Better Battery | power | none | Directly relevant: Ally uses AMD |
| ATI PowerPlay | GUID 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 | Max performance / Balanced | power | none | Old Radeons |
| Switchable graphics GPU preference | GUID a1662ab2-9d34-4e53-ba8b-2639b9e20857 | Maximize performance / Power saving | power | none | |

## 5. Privacy and security (Optimize > Privacy), 90 items

Most are `HKCU` or policy flags with no direct RAM effect. Items with a real process effect are marked.

| Plain name | What it changes | Cat | Effect | Risk |
|---|---|---|---|---|
| UAC level | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System` `ConsentPromptBehaviorAdmin`, `PromptOnSecureDesktop` (rec index 4) | security | none | Weakens prompts |
| Workplace Join prompts | policy `WorkplaceJoin\BlockAADWorkplaceJoin` = 1 (HKLM and HKCU) | privacy | none | Cannot join Entra via this path |
| BitLocker auto encryption | `HKLM\SYSTEM\CurrentControlSet\Control\BitLocker\PreventDeviceEncryption` = 1 | security | none | No automatic device encryption |
| WiFi-Sense | `HKLM\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting\Value` and `AllowAutoConnectToWiFiSenseHotspots\Value` = 0 | privacy | none | |
| Automatic maintenance | `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance\MaintenanceDisabled` = 1 | other | small | No scheduled idle maintenance (defrag/trim, diagnostics) |
| Windows Error Reporting | policy `Windows Error Reporting\Disabled` = 1 | privacy | small | |
| Remote Assistance | `HKLM\SYSTEM\CurrentControlSet\Control\Remote Assistance\fAllowToGetHelp` = 0 | security | none | |
| Smart App Control | `HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy\VerifiedAndReputablePolicyState` (rec index 0) | security | small | Lowers reputation-based blocking |
| Developer Mode | `HKLM\Software\Microsoft\Windows\CurrentVersion\AppModelUnlock\AllowDevelopmentWithoutDevLicense` = 0 | other | none | |
| PowerShell execution policy | `...\PowerShell\1\ShellIds\Microsoft.PowerShell\ExecutionPolicy` (HKCU and HKLM; rec index 2) | security | none | Script execution behaviour |
| Ads/promotional master toggle (rec index 1) and children: Content Delivery, Subscribed Content, Feature Management, Soft Landing, OEM preinstalled, preinstalled apps, preinstalled-ever, silent installed apps | `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager`: `ContentDeliveryAllowed`, `SubscribedContentEnabled`, `FeatureManagementEnabled`, `SoftLandingEnabled`, `OemPreInstalledAppsEnabled`, `PreInstalledAppsEnabled`, `PreInstalledAppsEverEnabled`, `SilentInstalledAppsEnabled` = 0 | privacy | small (stops silent app installs) | None |
| Lock screen disable | `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\DisableLockWorkstation` = 1 | other | none | Blocks Win+L locking |
| Spotlight / lock screen tips | ContentDeliveryManager `RotatingLockScreenEnabled`, `RotatingLockScreenOverlayEnabled`, `SubscribedContent-338387Enabled`-style ids | privacy | none | |
| Advertising ID | `HKCU\...\AdvertisingInfo\Enabled` = 0, `CPSS\Store\AdvertisingInfo\Value` | privacy | none | |
| Language list opt-out | `HKCU\Control Panel\International\User Profile\HttpAcceptLanguageOptOut` = 1 | privacy | none | |
| App launch tracking | `HKCU\...\Explorer\Advanced\Start_TrackProgs` = 0 | privacy | none | Start "most used" empties |
| Settings app suggestions/notifications | ContentDeliveryManager `SubscribedContent-338393Enabled` etc.; `HKCU\...\SystemSettings\AccountNotifications\EnableAccountNotifications` = 0 | privacy | none | |
| Online speech recognition | `HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy\HasAccepted` = 0; policy `InputPersonalization\AllowInputPersonalization` | privacy | none | Cloud dictation off |
| Narrator online services / scripting | `HKCU\Software\Microsoft\Narrator\NoRoam\OnlineServicesEnabled`, `ScriptingEnabled` = 0 | privacy | none | |
| Inking and typing personalization | `HKCU\...\CPSS\Store\InkingAndTypingPersonalization\Value`, `HKCU\Software\Microsoft\Personalization\Settings\AcceptedPrivacyPolicy`, `HKCU\Software\Microsoft\Input\TIPC\Enabled` = 0 | privacy | none | |
| Diagnostic data | policy `DataCollection\AllowTelemetry` = 0 (HKCU/HKLM) plus `HKCU\...\Diagnostics\DiagTrack\ShowedToastAtLevel` | privacy | small | Level 0 only honoured on Enterprise/Education; home/pro treat as minimum |
| Tailored experiences | `HKCU\...\Privacy\TailoredExperiencesWithDiagnosticDataEnabled` = 0; policy `CloudContent\DisableTailoredExperiencesWithDiagnosticData` | privacy | none | |
| Feedback frequency | policy `DataCollection\DoNotShowFeedbackNotifications` = 1; `HKCU\SOFTWARE\Microsoft\Siuf\Rules\NumberOfSIUFInPeriod` | privacy | none | |
| Activity history / timeline | policy `System\PublishUserActivities` = 0 (HKCU/HKLM); ContentDeliveryManager `SubscribedContent-353698Enabled` | privacy | none | |
| Search history, highlights, cloud search | `HKCU\...\SearchSettings`: `IsDeviceSearchHistoryEnabled`, `IsDynamicSearchBoxEnabled`, `IsMSACloudSearchEnabled`, `IsAADCloudSearchEnabled` = 0 | privacy | small (search highlights runs web content) | |
| Cortana | policy `Windows Search\AllowCortana` = 0 | privacy | small | |
| Location services | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location\Value` = Deny; policy `LocationAndSensors\DisableLocation` | privacy | none | Maps/weather need location |
| Camera / microphone access | ConsentStore `webcam\Value` / `microphone\Value` = Allow (rec config selects these two on) | privacy | none | |
| Account info, app diagnostics | ConsentStore `userAccountInformation\Value`, `appDiagnostics\Value` = Deny | privacy | none | |
| OneDrive auto backup | policy `HKLM/HKCU\SOFTWARE\Policies\Microsoft\OneDrive\KFMBlockOptIn` | privacy | none | Blocks folder backup prompts |

### 5a. AI and Copilot block (all policy or per-user flags)
Effect: real when combined with the Appx removals in section 7; the policy flags alone mostly stop features from launching. Identifiers:

| Group | Identifiers |
|---|---|
| Windows Copilot | `HKCU/HKLM\Software\Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot` = 1; `HKCU\Software\Microsoft\Windows\Shell\Copilot\IsCopilotAvailable` = 0; `...\Copilot\BingChat\IsUserEligible` = 0; `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CopilotKey\SetCopilotHardwareKey` = ""; `HKCU\...\Explorer\Advanced\ShowCopilotNudges` = 0 |
| Windows AI policies | `POL\WindowsAI\`: `DisableAIDataAnalysis`, `AllowRecallEnablement`=0, `TurnOffSavingSnapshots`, `DisableClickToDo`, `DisableSettingsAgent`, `DisableAgentConnectors`, `DisableAgentWorkspaces`, `DisableRemoteAgentConnectors`, `AllowCopilotRuntime`=0; `POL\CloudContent\DisableConsumerAccountStateContent` |
| AI access consent | ConsentStore `generativeAI\Value`, `systemAIModels\Value` = Deny; `POL\AppPrivacy\LetAppsAccessGenerativeAI`, `LetAppsAccessSystemAIModels` = 2; per-package microphone deny for `Microsoft.Copilot_8wekyb3d8bbwe` |
| Paint AI | `HKLM\SOFTWARE\Policies\Microsoft\Paint\`: `DisableImageCreator`, `DisableCocreator`, `DisableGenerativeFill`, `DisableGenerativeErase`, `DisableRemoveBackground` = 1 |
| Input insights | `HKCU\Software\Microsoft\input\Settings\InsightsEnabled` = 0 |
| Edge AI policies | `HKLM\SOFTWARE\Policies\Microsoft\Edge\`: `CopilotCDPPageContext`, `CopilotPageContext`, `HubsSidebarEnabled`, `EdgeEntraCopilotPageContext`, `Microsoft365CopilotChatIconEnabled`, `EdgeHistoryAISearchEnabled`, `ComposeInlineEnabled`, `GenAILocalFoundationalModelSettings`, `BuiltInAIAPIsEnabled`, `AIGenThemesEnabled`, `DevToolsGenAiSettings`, `ShareBrowsingHistoryWithCopilotSearchAllowed` |
| Office AI | `HKCU\Software\Policies\Microsoft\office\16.0\common\ai\training\optionalconnectedexperiencesenabled`, `...\common\privacy\controllerconnectedservicesenabled`, `usercontentdisabled`, `...\common\ai\contentsafetyserviceenabled`; `HKCU\Software\Microsoft\Office\16.0\{Word,Excel}\Options\EnableCopilot`; OneNote `Options\Other\EnableCopilot`, `EnableCopilotNotebooks` |

## 6. Updates, notifications, sound

| Plain name | What it changes | Cat | Effect | Risk |
|---|---|---|---|---|
| Windows Update policy mode (combo) | `...\Policies\Microsoft\Windows\WindowsUpdate\AU\NoAutoUpdate`, `AUOptions` (HKCU and `POL`). Rec index 1 | other | none | Missing security patches if set to "never" |
| Delivery Optimization | `...\DeliveryOptimization\DODownloadMode` (rec index 3) | other | small (stops P2P uploads) | |
| Latest updates early | `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings\IsContinuousInnovationOptedIn` = 0 | other | none | |
| Other MS products | `...\WindowsUpdate\UX\Settings\AllowMUUpdateService` = 0 | other | none | |
| Get me up to date | `...\UX\Settings\IsExpedited` = 0 | other | none | |
| Auto restart after updates | policy `AU\NoAutoRebootWithLoggedOnUsers` | other | none | |
| Update notifications level | policy `SetUpdateNotificationLevel` | other | none | |
| Restart notification | `...\UX\Settings\RestartNotificationsAllowed2` | other | none | |
| Metered download | `...\UX\Settings\AllowAutoWindowsUpdateDownloadOverMeteredNetwork` | other | none | |
| Driver updates via WU | policy `ExcludeWUDriversInQualityUpdate` | other | none | Drivers (e.g. ROG/AMD) not updated by WU |
| Driver co-installers | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer\DisableCoInstallers` | other | none | |
| Store auto-download | policy `WindowsStore\AutoDownload` = 2 | other | none | |
| Notifications: master + sound + lock screen + bell + welcome + tips + suggestions + system pane + capability access + startup app + location request + clock change + Windows Security + security & maintenance | `HKCU\...\PushNotifications\ToastEnabled`, `...\Notifications\Settings\NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND`, `...\ALLOW_TOASTS_ABOVE_LOCK`, `...\ALLOW_CRITICAL_TOASTS_ABOVE_LOCK`, `PushNotifications\LockScreenToastEnabled`, `Explorer\Advanced\ShowNotificationIcon`, ContentDeliveryManager `SubscribedContent-310093Enabled`, `-338389Enabled`, `SystemPaneSuggestionsEnabled`, `UserProfileEngagement\ScoobeSystemSettingEnabled`, `Windows.SystemToast.CapabilityAccess\Enabled`, `Windows.SystemToast.StartupApp\Enabled`, `Windows.SystemToast.SecurityAndMaintenance\Enabled`, `CapabilityAccessManager\ConsentStore\location\ShowGlobalPrompts`, `HKCU\Control Panel\Desktop\DstNotification`, `HKLM\Software\Microsoft\Windows Defender Security Center\Notifications\DisableNotifications` | UI/looks | none | Hidden alerts |
| Startup sound | `HKLM\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation\DisableStartupSound` = 1 and an EditionOverrides value | UI/looks | none | |
| Sound ducking | `HKCU\Software\Microsoft\Multimedia\Audio\UserDuckingPreference` (rec "Do nothing" = 3) | UI/looks | none | Voice chat no longer lowers game audio |
| Narrator ducking | `HKCU\Software\Microsoft\Narrator\NoRoam\DuckAudio` = 0 | UI/looks | none | |
| Voice activation | `HKLM\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings\AgentActivationEnabled` and `AgentActivationLastUsed` = 0 | privacy | small | "Hey Cortana"/apps listening off |
| Accessibility sounds | `HKCU\Control Panel\Accessibility\Sound on Activation`, `Warning Sounds` = 0 | UI/looks | none | |

## 7. Windows app removals (Software & Apps > Windows Apps)

Removal is via Appx removal for all users (`Get-AppxPackage -AllUsers` plus provisioned package removal, according to the script generator; the OneDrive and Edge entries use custom scripts). The recommended config's app section lists 57 entries (apps, 7 capabilities and the Recall feature are combined there) and marks 56 for removal; only the legacy Notepad capability is left. Package names below are the identifiers.

| App | Appx package name(s) | Idle RAM/process effect | Risk |
|---|---|---|---|
| 3D Viewer | Microsoft.Microsoft3DViewer | none | None |
| Mixed Reality Portal | Microsoft.MixedReality.Portal | none | VR headsets |
| Bing Search | Microsoft.BingSearch | small | Web results in search |
| Microsoft News | Microsoft.BingNews | small | None |
| MSN Weather | Microsoft.BingWeather | small | Taskbar/Widgets weather |
| Camera | Microsoft.WindowsCamera | none | Camera app gone |
| Clipchamp | Clipchamp.Clipchamp | none | None |
| Alarms & Clock | Microsoft.WindowsAlarms | none | Timers/alarms |
| Cortana | Microsoft.549981C3F5F10 | none | None |
| Get Help | Microsoft.GetHelp | none | Support entry points break |
| Calculator | Microsoft.WindowsCalculator | none | Calculator gone (it is defined in the app list but not in the recommended-config selection list I parsed) |
| Windows Advanced Settings / Dev Home | Microsoft.Windows.DevHome | none | |
| Microsoft Family Safety | MicrosoftCorporationII.MicrosoftFamily | small | Family features |
| Mail and Calendar | microsoft.windowscommunicationsapps | none | Mail/calendar |
| Skype | Microsoft.SkypeApp | none | |
| Microsoft Teams (consumer) | MSTeams | small | |
| Feedback Hub | Microsoft.WindowsFeedbackHub | none | |
| Maps | Microsoft.WindowsMaps | small | |
| Terminal | Microsoft.WindowsTerminal | none | Loses default terminal on Win11 |
| MS 365 Copilot / Office Hub | Microsoft.MicrosoftOfficeHub | small | |
| Outlook (new) | Microsoft.OutlookForWindows | small | |
| Paint 3D | Microsoft.MSPaint | none | |
| Paint | Microsoft.Paint | none | |
| Photos | Microsoft.Windows.Photos | small | Default image viewer |
| Snipping Tool | Microsoft.ScreenSketch | none | Print Screen shortcut handler |
| People | Microsoft.People | none | |
| Power Automate | Microsoft.PowerAutomateDesktop | none | |
| Quick Assist | MicrosoftCorporationII.QuickAssist | none | |
| Solitaire | Microsoft.MicrosoftSolitaireCollection | none | |
| Xbox app | Microsoft.GamingApp, Microsoft.XboxApp | real (background Xbox processes) | Game Pass, Xbox PC launcher; the Xbox Ally's full-screen experience depends on Xbox components |
| Xbox Identity Provider | Microsoft.XboxIdentityProvider | small | Xbox sign-in in games and Store titles breaks |
| Xbox Game Bar plugin | Microsoft.XboxGameOverlay | small | |
| Xbox Live in-game | Microsoft.Xbox.TCUI | small | Xbox Live dialogs in games break |
| Xbox Game Bar | Microsoft.XboxGamingOverlay | real | Overlay / `ms-gamebar` links; the Ally's Command Center/Game Bar flows break |
| Microsoft Store | Microsoft.WindowsStore | small | Cannot install or update Store/Game Pass games; very risky for Ally |
| Media Player | Microsoft.ZuneMusic | none | |
| Movies & TV | Microsoft.ZuneVideo | none | |
| Sound Recorder | Microsoft.WindowsSoundRecorder | none | |
| Sticky Notes | Microsoft.MicrosoftStickyNotes | none | |
| Tips | Microsoft.Getstarted | none | |
| To Do | Microsoft.Todos | none | |
| Notepad (modern) | Microsoft.WindowsNotepad | none | |
| Phone Link | Microsoft.YourPhone | real (YourPhone and CDP background processes) | Phone linking |
| Copilot | Microsoft.Copilot, Microsoft.Windows.Ai.Copilot.Provider, Microsoft.Copilot_8wekyb3d8bbwe | real | |
| Windows AI Experience | MicrosoftWindows.Client.AIX | real | Click to Do and AI features |
| Windows Copilot Client | MicrosoftWindows.Client.CoPilot | real | |
| Edge Game Assist | Microsoft.Edge.GameAssist | small | Browser overlay in games |
| Office Actions Server | Microsoft.Office.ActionsServer | small | |
| AI Manager | aimgr | small | |
| Writing Assistant | Microsoft.WritingAssistant | small | |
| AI Workload packages (20 names) | WindowsWorkload.OnnxRuntimeGenAI, SemanticTextSimilarity, ImageSearch, ContentExtraction, ScreenRegionDetection, TextRecognition, ImageContentModeration, ScreenSemanticsOCR, ImageSuperResolution, ObjectErase, ImageDescriptor, PortraitBlur, TextToSpeech, SpeechToText, ImageSegmentation, CreativeFilter, StudioEffects, SubtitleTranslation, Summarization, RewriteTextSuggestion | none (on-disk only) | |
| Copilot+ PC AI packages | MicrosoftWindows.Voiess, MicrosoftWindows.Speion, MicrosoftWindows.Livtop, MicrosoftWindows.InpApp, MicrosoftWindows.Filons (names as stored in the source) | none | |
| Microsoft Edge | Microsoft.MicrosoftEdge.Stable; winget id Microsoft.Edge; custom uninstall script v1.4 | real if Edge background processes run | WebView2 is explicitly kept; can break apps that expect Edge |
| App Installer | Microsoft.DesktopAppInstaller; winget id Microsoft.AppInstaller | none | Removes winget |
| OneDrive | Microsoft.OneDriveSync; winget id Microsoft.OneDrive; custom script | real (OneDrive.exe) | Cloud sync |
| OneNote | Microsoft.Office.OneNote | none | |

Edge removal script facts (own words): detects Legacy Edge and Chromium Edge, kills Edge processes, runs the uninstall strings found under `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall`, runs EdgeUpdate `/uninstall`, deletes Edge and EdgeUpdate keys under `HKLM/HKCU\SOFTWARE\Microsoft\Edge*`, `HKLM\SOFTWARE\Clients\StartMenuInternet\Microsoft Edge`, event-log service keys, `Classes\MicrosoftEdgeUpdate*` and `MSEdge*`, and sets an Image File Execution Options redirect for `ie_to_edge_stub.exe` (and removes one for `msedge.exe`) to send edge-protocol links to the default browser.
OneDrive removal script facts: removes the OneDriveSync Appx for all users, force-stops `*OneDrive*` processes, runs the uninstaller, deletes user and ProgramData OneDrive folders and Start menu shortcut, cleans related registry; also stops a stray regedit.

## 8. Windows Capabilities and Optional Features (Software & Apps)

Capability name identifiers (removed via Windows capability removal): `Browser.InternetExplorer`, `Microsoft.Windows.PowerShell.ISE`, `App.Support.QuickAssist`, `App.StepsRecorder`, `Media.WindowsMediaPlayer`, `Microsoft.Windows.WordPad`, `Microsoft.Windows.Notepad` (legacy), `Microsoft.Windows.MSPaint` (legacy), `OpenSSH.Client`, `OpenSSH.Server`. Effect: none to small (disk, not RAM). Risk: removing legacy Notepad/Paint or OpenSSH client can break scripts. Rec config selects all but legacy Notepad (7 selected incl. Quick Assist, ISE, Steps Recorder, WordPad, OpenSSH Server).
Optional features (DISM feature names): `Microsoft-Windows-Subsystem-Linux`, `Microsoft-Hyper-V-Hypervisor`, `Microsoft-Hyper-V-All`, `Microsoft-Hyper-V-Tools-All`, `NetFx3`, `Containers-DisposableClientVM` (Sandbox), `Recall`. Disabling Hyper-V/hypervisor platform removes the hypervisor (small to real RAM, and also removes the virtualization layer that VBS uses); Recall in rec config is selected for removal.

## 9. Customize (UI/looks; almost no RAM effect)

| Group | Count | Identifiers / examples | Effect | Risk |
|---|---|---|---|---|
| Taskbar | 28 | `...\Explorer\Advanced\TaskbarAl` (alignment), `ShowTaskViewButton`, `ShowCopilotButton`, `TaskbarCompanion`, `CopilotPWAPin`, `RecallPin`, `TaskbarSmallIcons`, `TaskbarGlomLevel`, `MMTaskbarGlomLevel`, `MMTaskbarEnabled`, `MMTaskbarMode`, `TaskbarBadges`, `TaskbarFlashing`, `TaskbarSn`, `TaskbarSd`, `IconSizePreference`, `ExtendedUIHoverTime`, `TaskbarAcrylicOpacity`, `TaskbarDeveloperSettings\TaskbarEndTask`, `Search\SearchboxTaskbarMode`, `Explorer\StuckRects3\Settings`, `Explorer\EnableAutoTray`; policies `HKCU/HKLM\Software\Policies\Microsoft\Dsh\AllowNewsAndInterests` = 0 (Widgets), `...\Windows Feeds\EnableFeeds` = 0 (News and interests), `Explorer\HideSCAMeetNow` = 1 | Widgets policy is real (stops the Widgets/WebView2 host processes); the rest none | Widgets policy hides the Widgets panel |
| Start menu | 13 | `Explorer\Advanced\Start_Layout`, `Start\AllAppsViewMode`, `Start\ShowRecentList`, `ShowFrequentList`, `Start_TrackProgs`, `Start_TrackDocs`, `Start_IrisRecommendations`, `Start_AccountNotifications`, ContentDeliveryManager `SubscribedContent-338388Enabled`; policy `Explorer\HideRecommendedSection`; policy `Explorer\DisableSearchBoxSuggestions` = 1 (Bing in Start); plus "Clean Start Menu" script: writes a default-profile `LayoutModification.xml` under `C:\Users\Default\AppData\Local\Microsoft\Windows\Shell` and clears per-user pinned-list caches (`start.bin`, `start2.bin`) | Bing-off real-ish (search web process); rest none | Pinned layout reset |
| Explorer | 87 | Desktop icons `HideDesktopIcons\NewStartPanel\{CLSID}`; classic context menu `HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}`; shortcut arrow via `...\Explorer\Shell Icons\29`; Explorer options (`LaunchTo`, `HideFileExt`, `Hidden`, `ShowSuperHidden`, `SeparateProcess`, `PersistBrowsers`, `AutoCheckSelect`, `UseCompactMode`, `TypeAhead`, `NavPane*`); `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled`; This PC folder entries under `...\Explorer\MyComputer\NameSpace\{GUID}`; context menu add-ons (Take Ownership, Show/Hide extensions, SFC, DISM, CHKDSK, Compress To, PS1 Edit/Run); Photo Viewer enable; legacy Notepad association; dynamic lighting `HKCU\Software\Microsoft\Lighting\AmbientLightingEnabled`, `ControlledByForegroundApp`; regional formats `HKCU\Control Panel\International\*`; Autoplay | none. "Launch folder windows in a separate process" (`SeparateProcess`) would add explorer.exe processes (negative). | Mostly taste |
| Theme | 2 | `...\Themes\Personalize\AppsUseLightTheme`, `SystemUsesLightTheme`, `EnableTransparency` = 0 | small (transparency off cuts some GPU/DWM work) | Looks |

## 10. Things Winhance does that winutil and Tiny11 typically do not
(From this catalog only; I did not diff against winutil or Tiny11 source, so treat this as "unusual or not typical" per general knowledge.)

- Full AC/DC power-plan editor using GUID-level settings (processor boost, EPP, core parking, USB suspend, PCIe ASPM, AMD slider overlay) and a bundled custom "Winhance Power Plan" with fixed GUID.
- Per-setting detection of current state with import/export of configs and a "recommended" preset; reverse of every toggle.
- Multimedia scheduler (MMCSS) tweaks: SystemResponsiveness, Games task Priority/GPU Priority/Scheduling Category.
- Graphics: HAGS mode, flip-model/VRR/Auto-HDR flags in the DirectX user GPU preferences string, MPO and overlay toggles (OverlayTestMode, DisableOverlays, OverlayMinFPS), color-management toggle.
- Svchost split threshold control (process-count lever) and ServicesPipeTimeout.
- Granular services (about 34) with Disabled/Manual/Auto choices plus explicit Xbox services guidance.
- A wide scheduled-task disable list and Recall/AI task disable.
- Extensive AI/Copilot/Recall policy set, including Edge, Paint and Office AI policies and Windows AI Appx / `WindowsWorkload.*` package removal.
- Touch-keyboard host rename script (TextInputHost.exe) as a RAM saver.
- Edge and OneDrive custom removal scripts (Edge keeps WebView2), and Windows Capabilities/Optional Features management including Recall.
- WebView2-in-Windows-Search feature-flag override (FeatureManagement overrides ID 1694661260).
- Visual-effect fine toggles, DNS provider picker with DoH templates, Nagle tweaks per interface.
- Reviewed against hardware: items filtered by compatibility (e.g. NVIDIA/Intel/AMD GPU, laptop/battery), and a generated Autounattend/ISO builder (ISO customization, WIM, driver categorizer, in `AdvancedTools`; not catalogued here).

## 11. Risky for an Xbox Ally X (handheld, gamepad, Xbox full-screen experience)
- Removing Microsoft Store, Xbox app, Xbox Identity Provider, Xbox Game Bar, Gaming Services-related entries: breaks Game Pass, launching Store games, Xbox sign-in and likely the console-style experience.
- Disabling Touch Keyboard/TextInputHost (and renaming its exe): on-screen keyboard unusable on a touchscreen.
- Disabling Sensor services (auto brightness, rotation) and Windows Biometric (Hello) services.
- Disabling Game Bar controller access (Xbox button) and Game DVR.
- Disabling Mobile Hotspot, Wireless adapter and USB selective-suspend settings can raise battery drain.
- VBS and HVCI off, UAC lowered, Smart App Control changes, Automatic Maintenance off: security trade-offs. Updates policy mode and driver-update exclusion can block ASUS/AMD drivers.
- Power throttling off and aggressive processor policies: more heat and shorter battery life on a handheld.
- Hard to verify: DirectXUserGlobalSettings string layout, VisualFXSetting index meaning, how the Winhance power plan is first created (unclear).

## 12. Top candidates for lowering idle process count / RAM (from this list)
1. Raise `SvcHostSplitThresholdInKB` above installed RAM (fewer svchost.exe).
2. SysMain disabled.
3. Windows Search (WSearch) to Manual/Disabled.
4. Background app permissions: force deny (`LetAppsRunInBackground`).
5. WebView2 in Windows Search feature-flag override off.
6. Widgets policy (`AllowNewsAndInterests` = 0) plus Widgets/News removal.
7. Copilot, Windows AI Experience (AIX), Copilot client Appx removal plus policies and AI Fabric service disabled.
8. Phone Link removal plus CDPSvc/CDPUserSvc/PhoneSvc/TapiSrv.
9. Xbox apps removal (only if the device does not need Xbox services).
10. Touch keyboard host disabled (not for the Ally).
11. OneDrive removal.
12. Edge removal (WebView2 kept).
13. DiagTrack, WerSvc, PcaSvc, lfsvc, Spooler, RasMan/RasAuto, Remote Desktop trio, MapsBroker, Fax, WMPNetworkSvc to Manual/Disabled.
14. Game DVR / capture off.
15. Telemetry/maintenance scheduled tasks disabled and automatic maintenance off.
