# tiny11builder: what it removes and changes (facts only)

Source: github.com/ntdevlabs/tiny11builder, default branch `winui-preview` (no branch named `main` exists).
Commit read: `8909da14b783e9625bfabc7ab2cf16e233b00a21` (fetched 2026-10-24). Open issues read: 20 (all of them at fetch time).
The repo has no license, so this file records facts (names, paths, values, command names) in our own words. No code is copied.

Files read: `tiny11maker.ps1`, `tiny11Coremaker.ps1`, `autounattend.xml`, `docs/usage.md`, and in `src/Tiny11Builder.Core/`: `CleanupCatalog.cs`, `PolicyCatalog.cs`, `OfflineRegistry.cs`, `ImageOperations.cs`, `BuildEngine*.cs`, `UnattendBuilder.cs`, `OscdimgAcquirer.cs`, `EdgeRegistration.cs`, `OfflineDataCleaner.cs`, `ImageDefaults.cs`.

Scope key used in the tables:
- **PS-T** = `tiny11maker.ps1` (original "Tiny11", serviceable).
- **PS-C** = `tiny11Coremaker.ps1` (original "Core", not serviceable).
- **W-T** = WinUI app "Tiny11" preset (code name `Original` in `CleanupCatalog.ForMode`).
- **W-C** = WinUI app "Tiny11 Core" preset.
- **W-X** = WinUI Custom only (selectable, in no preset).
- The WinUI presets are NOT command-for-command copies of the scripts (usage.md says so). Where script and app differ, both are listed.
- "Offline" = done on the mounted `install.wim` (or `boot.wim`), never on the running PC.
- RAM / process effect column: the repo contains NO measurements of RAM or process count. Entries are my qualitative reading only, marked "unclear" where I cannot tell.

## 1. Removal and change tables

### 1a. Provisioned apps (AppX/MSIX)

Offline method for all rows: `dism /Image:<mount> /Get-ProvisionedAppxPackages`, filter by name, then `/Remove-ProvisionedAppxPackage /PackageName:<full name>` per match. This only removes the per-new-user provisioning; it does not uninstall from users that already exist (a fresh image has none).
Matching differs: PS-T matches by substring (`-like "*prefix*"`); PS-C matches by `prefix_` start; W-* matches display name exactly AND package name starting with `<name>_`.

| # | Plain name | Identifier (provisioned appx name) | Scope | RAM / process effect | Risk / what it breaks |
|---|---|---|---|---|---|
| 1 | Copilot apps | `Microsoft.Copilot`, `Microsoft.Windows.Copilot`, `Microsoft.549981C3F5F10` | PS-T, PS-C, W-T, W-C | Low (removes an on-demand app/process) | Low. 549981C3F5F10 is Cortana-era id; unclear if present on current builds |
| 2 | New Outlook, Mail and Calendar | `Microsoft.OutlookForWindows`, `microsoft.windowscommunicationsapps` | PS-T, PS-C, W-T, W-C | Low | Loses Mail/Calendar; PS adds block keys (section 1e) because it reappears |
| 3 | Teams (personal) | `MSTeams`, `MicrosoftTeams`, `Microsoft.Windows.Teams` | PS-T, PS-C, W-T, W-C | Low to medium (chat/Teams autostart) | Low |
| 4 | Xbox app and gaming overlays | `Microsoft.GamingApp`, `Microsoft.XboxApp`, `Microsoft.Xbox.TCUI`, `Microsoft.XboxGameOverlay`, `Microsoft.XboxGamingOverlay`, `Microsoft.XboxIdentityProvider`, `Microsoft.XboxSpeechToTextOverlay` | PS-T: all 7. PS-C: GamingApp, TCUI, GameOverlay, GamingOverlay, SpeechToTextOverlay (NOT XboxApp, NOT XboxIdentityProvider). W-*: all 7 | Medium (Game Bar widgets/overlay processes) | **HIGH for NitrOS.** The app's own text: "Gaming sign-in and recording may be unavailable". The ROG Xbox Ally X depends on the Xbox app / full screen experience, so do not take this row |
| 5 | News, Weather, Bing Search | `Microsoft.BingNews`, `Microsoft.BingWeather`, `Microsoft.BingSearch` (PS-C has only News, Weather) | PS-T, PS-C(2), W-T, W-C | Low | Low; local search kept |
| 6 | Microsoft 365 promos | `Microsoft.MicrosoftOfficeHub`, `Microsoft.OfficePushNotificationUtility` (PS-C has only OfficeHub) | PS-T, PS-C(1), W-T, W-C | Low | Low |
| 7 | Clipchamp | `Clipchamp.Clipchamp` | PS-T, PS-C, W-T, W-C | Low | Low |
| 8 | Solitaire | `Microsoft.MicrosoftSolitaireCollection` | PS-T, PS-C, W-T, W-C | Low | Low |
| 9 | Phone Link, cross-device | `Microsoft.YourPhone`, `Microsoft.Windows.CrossDevice` (PS-C has only YourPhone) | PS-T, PS-C(1), W-T, W-C | Low to medium (background sync) | Phone integration stops |
| 10 | Feedback Hub, Get Help, Tips | `Microsoft.WindowsFeedbackHub`, `Microsoft.GetHelp`, `Microsoft.Getstarted` | PS-T, PS-C, W-T, W-C | Low | Low |
| 11 | Dev Home, Power Automate | `Microsoft.Windows.DevHome` (not in PS-C), `Microsoft.PowerAutomateDesktop` | PS-T, PS-C(PAD only), W-T, W-C | Low | Dev Home tends to reappear (section 1e) |
| 12 | People, To Do, Family, Skype | `Microsoft.People`, `Microsoft.Todos`, `Microsoft.SkypeApp` (not PS-C), `MicrosoftCorporationII.MicrosoftFamily` | PS-T, PS-C(3, no Skype), W-T, W-C | Low | Low |
| 13 | Maps, Mixed Reality | `Microsoft.WindowsMaps`, `Microsoft.MixedReality.Portal` (Mixed Reality not in PS-C) | PS-T, PS-C(Maps), W-T, W-C | Low | Low |
| 14 | Media Player and Movies and TV | `Microsoft.ZuneMusic`, `Microsoft.ZuneVideo` | PS-T, PS-C, W-T, W-X (off in Minimal, so not W-C) | Low | Loses Media Player app; codecs stay |
| 15 | Clock, Sound Recorder, Sticky Notes | `Microsoft.WindowsAlarms`, `Microsoft.WindowsSoundRecorder`, `Microsoft.MicrosoftStickyNotes` (Sticky Notes not in PS-C) | PS-T, PS-C(2), W-T, W-C | Low | Low |
| 16 | Quick Assist | `MicrosoftCorporationII.QuickAssist` | PS-T, PS-C, W-T, W-C | Low | Low |
| 17 | Paint, 3D Viewer, OneNote | `Microsoft.Paint`, `Microsoft.MSPaint`, `Microsoft.Microsoft3DViewer`, `Microsoft.Office.OneNote` | PS-T, W-T, W-X (not PS-C, not W-C) | Low | Loses Paint |
| 18 | Camera | `Microsoft.WindowsCamera` | PS-T, W-T, W-X | Low | App only; drivers stay |
| 19 | Windows Terminal | `Microsoft.WindowsTerminal` | PS-T, W-T, W-X | Low | PowerShell and cmd stay |
| 20 | OEM promo apps | `AppUp.IntelManagementandSecurityStatus`, `DolbyLaboratories.DolbyAccess`, `DolbyLaboratories.DolbyDigitalPlusDecoderOEM` | PS-T, W-T, W-X | Low | Drivers not touched (per app text) |
| 21 | Wallet, Start Experiences App | `Microsoft.Wallet`, `Microsoft.StartExperiencesApp` | PS-T only (not in WinUI catalog) | Unclear | Unclear |

### 1b. Files and folders deleted offline

| # | Plain name | Identifier | Scope | How (offline) | RAM / process effect | Risk |
|---|---|---|---|---|---|---|
| 22 | Edge browser files | `Program Files (x86)\Microsoft\Edge`, `...\EdgeUpdate`, `...\EdgeCore` (W-* also does `Program Files\...` and `EdgeWebView`) | PS-T, PS-C, W-T (+W-C when selected as `edge-files`) | takeown/icacls then Remove-Item (PS); native backup/restore-privilege delete (WinUI) | Medium: removes Edge and updater processes | Edge remnants in Settings and "installed programs" (known issue); updates can put Edge back |
| 23 | Edge WebView2 runtime | `Windows\System32\Microsoft-Edge-Webview`; PS-C and WinUI also `Windows\WinSxS\<arch>_microsoft-edge-webview_31bf3856ad364e35*` | PS-T(System32 only), PS-C(both), W-T/W-C (both) | takeown, icacls /grant, delete | Medium (each WebView2 host app spawns several msedgewebview2 processes; removing it removes them) | **High for us**: the app text says apps that require WebView2 may stop working. Which gaming apps need it: unclear |
| 24 | Edge uninstall registry entries | `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge` and `...\Microsoft Edge Update` (PS); WinUI removes only `Microsoft Edge`, after identity and publisher checks, in both Uninstall locations | PS-T, PS-C, W-T | offline SOFTWARE hive key delete | None | Leaves a broken entry if the delete is skipped (issue #659) |
| 25 | OneDrive setup binary | `Windows\System32\OneDriveSetup.exe` (PS); WinUI also `Windows\SysWOW64\OneDriveSetup.exe` | PS-T, PS-C, W-T, W-C (`onedrive-files`) | takeown/icacls/delete | Low to medium (OneDrive autostart process never installs) | No OneDrive client on first sign-in. arm64 media has no System32 copy (known issue) |
| 26 | Windows Recovery Environment | `Windows\System32\Recovery\winre.wim` | PS-C, W-C only | PS-C: delete then create empty placeholder file. WinUI: delete without placeholder | None at idle | **No Reset this PC, no recovery boot.** Disk space saved only |
| 27 | Component store (WinSxS) pruning | `Windows\WinSxS`: PS-C copies a keep-list (Catalogs, FileMaps, Fusion, InstallTemp, Manifests, servicing stack, vc80/vc90 CRT, common-controls, GDI+, isolationautomation, proxystub, resources; ARM64 list also SettingsManifests, Temp) into `WinSxS_edit`, deletes the rest, renames. WinUI deletes every WinSxS folder that does not match its keep pattern (metadata folders + common-controls, gdiplus, servicing stack, vc CRT, isolationautomation) | PS-C, W-C (W-X for single folders) | takeown/icacls /r then delete (PS); extended-length-path native delete (WinUI) | Low to none at idle (it is disk, not RAM) | **Irreversible. No Windows Update, no adding features, no languages.** Hard to say what else breaks; Core is "test VM only" per the repo |
| 28 | Scheduled task definition files | `Windows\System32\Tasks\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser`; `...\Application Experience\ProgramDataUpdater`; folder `...\Customer Experience Improvement Program\` (recursive); `...\Chkdsk\Proxy`; `...\Windows Error Reporting\QueueReporting` | PS-T, PS-C (not in WinUI catalog) | file delete only; the script never touches the TaskCache registry | Low to medium: Appraiser (compattelrunner) and CEIP tasks are periodic processes | Low. Whether Windows rebuilds them from TaskCache: unclear |
| 29 | Redundant image data | `Windows\SoftwareDistribution\Download`, `Windows\Temp`, `Users\Default\AppData\Local\Temp`, `Windows\Logs\CBS` (CbsPersist_*.cab/log), `Windows\Logs\DISM` (*.bak), `Windows\MEMORY.DMP`, `Windows\Minidump\*.dmp`, `ProgramData\Microsoft\Windows\WER\ReportArchive`, `...\ReportQueue` | W-X (opt-in switch "Remove redundant image data", default off) | allowlist delete, skipped if pending servicing | None | Disk only |

### 1c. Capabilities (Features on Demand) and optional features

Offline method: `dism /Image:<mount> /Get-Capabilities`, then `/Remove-Capability`; features via `/Get-Features`, `/Disable-Feature /FeatureName:<n> /Remove /NoRestart`; CBS packages via `/Get-Packages /Format:Table` then `/Remove-Package /PackageName:<identity>` (PS-C only).

| # | Plain name | Identifier | Scope | RAM / process effect | Risk |
|---|---|---|---|---|---|
| 30 | Handwriting, OCR, Speech, TextToSpeech language payloads | Capability prefixes `Language.Handwriting`, `Language.OCR`, `Language.Speech`, `Language.TextToSpeech` (W-X `language-extras`; W-C adds it). PS-C removes CBS packages `Microsoft-Windows-LanguageFeatures-{Handwriting,OCR,Speech,TextToSpeech}-<lang>-Package~31bf3856ad364e35` for the image language only | PS-C, W-C, W-X | Low | Voice typing and narrator voices lose offline data |
| 31 | WordPad | Capability `Microsoft.Windows.WordPad`; PS-C package `Microsoft-Windows-WordPad-FoD-Package~` | PS-C, W-C, W-X (see note below table) | None | Low |
| 32 | Steps Recorder | Capability `App.StepsRecorder`; PS-C package `Microsoft-Windows-StepsRecorder-Package~` | PS-C, W-C | None | Low |
| 33 | Math Input / Tablet PC Math | Capability `MathRecognizer`; PS-C package `Microsoft-Windows-TabletPCMath-Package~` | PS-C, W-C (usage.md lists it under Tiny11, but tiny11maker.ps1 does not remove it) | None | Low |
| 34 | Legacy Media Player | Capability `Media.WindowsMediaPlayer`; PS-C package `Microsoft-Windows-MediaPlayer-Package~31bf3856ad364e35` | PS-C, W-C | None | Low |
| 35 | Extended wallpapers | Capability `Microsoft.Windows.Wallpaper.Extended`; PS-C package `Microsoft-Windows-Wallpaper-Content-Extended-FoD-Package~31bf3856ad364e35` | PS-C, W-C | None | Cosmetic |
| 36 | Internet Explorer optional | Feature `Internet-Explorer-Optional-amd64` / `-arm64`; PS-C package `Microsoft-Windows-InternetExplorer-Optional-Package~31bf3856ad364e35` | PS-C, W-T, W-C | None | Low (IE mode in Edge is separate; unclear) |
| 37 | XPS printing | Feature `Printing-XPSServices-Features` | W-T, W-C (not in the PS scripts) | Low (print spooler helpers) | Breaks XPS print path |
| 38 | Work Folders | Feature `WorkFolders-Client` | W-T, W-C (not in PS) | Low | Enterprise sync only |
| 39 | Recall | Feature `Recall` (only if DISM exposes it) | W-T, W-C (not in PS) | Medium on Copilot+ PCs; unclear on others | Low |
| 40 | Defender client package | CBS package `Windows-Defender-Client-Package~31bf3856ad364e35~` | PS-C only | Medium (MsMpEng and friends never exist) | **No antivirus.** WinUI Core instead only disables (row 50) |
| 41 | Kernel LA57 FoD | CBS package `Microsoft-Windows-Kernel-LA57-FoD-Package~31bf3856ad364e35~amd64` | PS-C only | None | 5-level paging support removed; low for gaming handhelds |
| 42 | .NET 3.5 (added, not removed) | Feature `NetFX3` from `sources\sxs` | PS-C prompt; W-* optional | None | Core cannot add it later |

Note on row 31 and W-T: in `ForPreset(Original)` the two groups `language-extras` and `legacy-capabilities` are explicitly excluded, so W-T does not remove WordPad/Steps Recorder/MathRecognizer/WMP/wallpapers. W-C includes `legacy-capabilities` (Minimal = true) and `language-extras` (added by name).

### 1d. Services and drivers

| # | Plain name | Identifier | Scope | How | RAM / process effect | Risk |
|---|---|---|---|---|---|---|
| 43 | Device-wide push (WAP push) service | `HKLM\SYSTEM\ControlSet001\Services\dmwappushservice\Start = 4` | PS-T, PS-C | offline SYSTEM hive | Low | Low |
| 44 | Windows Update services (disable) | PS-C: `...\Services\wuauserv\Start=4`; deletes keys `Services\WaaSMedicSVC` and `Services\UsoSvc`. W-C: sets Start=4 on `wuauserv`, `UsoSvc`, `WaaSMedicSvc` in every ControlSet | PS-C, W-C | offline SYSTEM hive | Medium (wuauserv, UsoSvc, WaaSMedic cycles) | **No updates, no Store updates, no driver updates.** PS-C deletes service keys outright (not reversible by flipping a value) |
| 45 | Windows Defender services (disable) | `WinDefend`, `WdNisSvc`, `WdNisDrv`, `WdFilter`, `Sense` set to Start=4 (PS-C ControlSet001 only; W-C every ControlSet and also `WaaSMedicSvc` above) | PS-C, W-C | offline SYSTEM hive | Medium to high (MsMpEng is a big resident process) | **No malware protection.** Also hides Settings pages (row 52) |
| 46 | Arbitrary services | any `ControlSetNNN\Services\<name>` | W-X | Disable = Start 4; Remove = delete the service key; shared exe files are never deleted | Variable | No dependency solver; can brick boot |
| 47 | System apps and WinSxS folders, CBS packages | `Windows\SystemApps\<folder>`, `Windows\WinSxS\<folder>`, any package from `/Get-Packages`, any driver from `/Get-Drivers` | W-X ("expert", needs warning + acknowledgement) | native delete or DISM `/Remove-Package`, `/Remove-Driver` | Variable | "Protected payload. Removal can break boot, servicing, security or the shell" (the app's own text) |

### 1e. Registry changes (offline hives) and policies

Hive load names: PS scripts use `HKLM\zCOMPONENTS`, `zDEFAULT`, `zNTUSER`, `zSOFTWARE`, `zSYSTEM`. In the table, `SOFT` = `Windows\System32\config\SOFTWARE`, `SYS` = `...\config\SYSTEM`, `NTU` = `Users\Default\ntuser.dat`, `DEF` = `...\config\default`. WinUI loads hives under a unique key `HKLM\Tiny11Builder_Offline_<guid>` and writes with RegSetValueExW, reading each value back.
Unless noted, scope is PS-T + PS-C, and W-* where the WinUI policy catalog id is given.

| # | Plain name | Hive + key + value | WinUI id | RAM / process effect | Risk |
|---|---|---|---|---|---|
| 48 | Bypass hardware checks (Setup) | SYS `Setup\LabConfig`: `BypassCPUCheck`, `BypassRAMCheck`, `BypassSecureBootCheck`, `BypassStorageCheck`, `BypassTPMCheck` = 1; SYS `Setup\MoSetup\AllowUpgradesWithUnsupportedTPMOrCPU` = 1; DEF and NTU `Control Panel\UnsupportedHardwareNotificationCache` `SV1`,`SV2` = 0. Written to BOTH install.wim and boot.wim (index 2) | `hardware-bypass` (W-T, W-C; off by default in W-X) | None | Unsupported hardware may be unstable. Docs call it "best effort", not guaranteed on new builds |
| 49 | Local account at OOBE | SOFT `Microsoft\Windows\CurrentVersion\OOBE\BypassNRO` = 1; PS-C also `OOBE\DisableOnline` = 1 | via `PreferLocalAccount` recipe flag | None | Docs: may not work on newer builds |
| 50 | Core: Windows Update policy | PS-C: SOFT `Policies\Microsoft\Windows\WindowsUpdate`: `DoNotConnectToWindowsUpdateInternetLocations`=1, `DisableWindowsUpdateAccess`=1, `WUServer`/`WUStatusServer`/`UpdateServiceUrlAlternate` = "localhost"; `...\WindowsUpdate\AU`: `UseWUServer`=1, `NoAutoUpdate`=1; plus RunOnce commands that stop wuauserv and set Start=4. W-C: `AU\NoAutoUpdate`=1 and `WindowsUpdate\DisableWindowsUpdateAccess`=1 only | n/a (Core flavor) | Medium | No updates |
| 51 | Core: Defender policy | W-C: SOFT `Policies\Microsoft\Windows Defender\DisableAntiSpyware`=1 and `...\Real-Time Protection\DisableRealtimeMonitoring`=1. PS-C instead hides Settings pages: SOFT `Microsoft\Windows\CurrentVersion\Policies\Explorer\SettingsPageVisibility` = `hide:virus;windowsupdate` | n/a | Medium | No protection |
| 52 | Sponsored apps and ads | NTU `Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager`: `OemPreInstalledAppsEnabled`, `PreInstalledAppsEnabled`, `PreInstalledAppsEverEnabled`, `SilentInstalledAppsEnabled`, `ContentDeliveryAllowed`, `FeatureManagementEnabled`, `SoftLandingEnabled`, `SubscribedContentEnabled`, `SystemPaneSuggestionsEnabled`, `SubscribedContent-310093/338388/338389/338393/353694/353696Enabled` all = 0; delete subkeys `ContentDeliveryManager\Subscriptions` and `...\SuggestedApps`; SOFT `Policies\Microsoft\Windows\CloudContent`: `DisableWindowsConsumerFeatures`, `DisableConsumerAccountStateContent`, `DisableCloudOptimizedContent` = 1; SOFT `Policies\Microsoft\PushToInstall\DisablePushToInstall`=1; SOFT `Policies\Microsoft\MRT\DontOfferThroughWUAU`=1 (PS only); SOFT `Microsoft\PolicyManager\current\device\Start\ConfigureStartPins` (REG_SZ, an empty pinned list) (PS only). WinUI adds user CloudContent policies (`DisableWindowsSpotlightFeatures`, `DisableThirdPartySuggestions`, `DisableTailoredExperiencesWithDiagnosticData`), `Explorer\Advanced\ShowSyncProviderNotifications`=0 | `ads` | Low to medium (stops silent app installs, fewer background fetches) | Low. Empty Start pins means no pinned apps |
| 53 | Telemetry and privacy | PS: SOFT `Policies\Microsoft\Windows\DataCollection\AllowTelemetry` = **0**; NTU `...\AdvertisingInfo\Enabled`=0, `...\Privacy\TailoredExperiencesWithDiagnosticDataEnabled`=0, `Speech_OneCore\Settings\OnlineSpeechPrivacy\HasAccepted`=0, `Input\TIPC\Enabled`=0, `InputPersonalization`: `RestrictImplicitInkCollection`=1, `RestrictImplicitTextCollection`=1, `TrainedDataStore\HarvestContacts`=0, `Personalization\Settings\AcceptedPrivacyPolicy`=0. WinUI sets `AllowTelemetry` = **1** (keeps required diagnostics) and adds SOFT `Policies\Microsoft\Windows\AdvertisingInfo\DisabledByGroupPolicy`=1 | `privacy` | Low to medium (fewer DiagTrack uploads) | AllowTelemetry 0 is honored only on Enterprise/Education; on Pro it acts as 1 (general Windows knowledge, not stated in the repo) |
| 54 | Reserved storage | SOFT `Microsoft\Windows\CurrentVersion\ReserveManager\ShippedWithReserves` = 0 | `reserved-storage` | None | Less free space for updates |
| 55 | BitLocker auto device encryption | SYS `ControlSet001\Control\BitLocker\PreventDeviceEncryption` = 1 (WinUI uses the active control set number) | `device-encryption` | None | No automatic encryption |
| 56 | Chat / Meet Now icon | SOFT `Policies\Microsoft\Windows\Windows Chat\ChatIcon` = 3; NTU `Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarMn` = 0 | `oobe` | Low | Low |
| 57 | OneDrive sync policy | SOFT `Policies\Microsoft\Windows\OneDrive\DisableFileSyncNGSC` = 1 | `onedrive` | Low to medium (OneDrive.exe stays idle) | No OneDrive sync |
| 58 | Stop Outlook and Dev Home from returning | SOFT `Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator\UScheduler_Oobe\OutlookUpdate\workCompleted`=1 (PS-T only) and `...\UScheduler\OutlookUpdate\workCompleted`=1, `...\UScheduler\DevHomeUpdate\workCompleted`=1; delete `SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\OutlookUpdate` and `...\DevHomeUpdate`. Policies: `Policies\Microsoft\Teams\DisableInstallation`=1, `Policies\Microsoft\Windows\Windows Mail\PreventRun`=1 | `ads` (Teams, Mail policies); orchestrator keys not in WinUI catalog | Low | Repo admits they "might reappear after some time" |
| 59 | Copilot and AI | SOFT `Policies\Microsoft\Windows\WindowsCopilot\TurnOffWindowsCopilot`=1 (also user-hive copy in WinUI); WinUI adds `Policies\Microsoft\Windows\WindowsAI\DisableAIDataAnalysis`=1 and NTU `Explorer\Advanced\ShowCopilotButton`=0 | `copilot` | Low to medium | Low |
| 60 | Edge policy | SOFT `Policies\Microsoft\Edge\HubsSidebarEnabled`=0 (PS). WinUI adds `HideFirstRunExperience`=1, `ShowRecommendationsEnabled`=0, `UserFeedbackAllowed`=0, `SpotlightExperiencesAndRecommendationsEnabled`=0 | `edge` | Low | Low |
| 61 | Search suggestions | NTU/user `Policies\Microsoft\Windows\Explorer\DisableSearchBoxSuggestions`=1 (PS writes it in SOFT; WinUI writes the user hive). WinUI adds SOFT `Policies\Microsoft\Windows\Windows Search`: `AllowCloudSearch`=0, `EnableDynamicContentInWSB`=0 | `search` | Low | Low. Writing `SearchSettings\IsDynamicSearchBoxEnabled` failed with Win32 error 5 in app 0.1.1 (issues #640, #647); 0.1.3 docs say it is no longer written |
| 62 | Widgets | SOFT `Policies\Microsoft\Dsh\AllowNewsAndInterests` = 0 | `widgets` (WinUI only) | Medium (Widgets host processes) | Does not write `TaskbarDa` on purpose (rejected by new builds) |
| 63 | Setup/OOBE privacy nags | SOFT `Policies\Microsoft\Windows\OOBE\DisablePrivacyExperience`=1; NTU `UserProfileEngagement\ScoobeSystemSettingEnabled`=0 | `oobe` (WinUI only) | None | Low |
| 64 | Boot image command line | PS-C only: `boot.wim` SYS `Setup\CmdLine` = `X:\sources\setup.exe` | n/a | None | Needed so legacy Setup starts. WinUI Core instead writes a `Winpeshl.ini` that runs `wpeinit.exe` then `sources\setup.exe` (or `setup_old.exe`) |

### 1f. Counts

- Numbered rows in tables above: 64 (21 app rows, 8 file/folder rows plus 1 cleanup row, 13 capability/feature/package rows, 5 service rows, 17 registry or policy rows; row 42 is an addition, not a removal).
- Provisioned app name patterns: PS-T 53 entries (52 unique; WindowsTerminal is listed twice); PS-C 32 entries; WinUI catalog 20 groups covering 52 package names.
- Registry: PS-T has 64 `Set-RegistryValue` calls (10 of them repeat the bypass in boot.wim) and 7 key deletes; PS-C has 86 `reg add` calls (it repeats some values) plus update/Defender changes and `Setup\CmdLine` in boot.wim.
- Scheduled-task definitions deleted: 5 (one of them a whole folder).

## 2. What Core removes beyond Tiny11

PS-C additions over PS-T (all irreversible, "test VM only" per the repo):
1. CBS package removal: Defender client, IE optional, LA57 FoD, MediaPlayer, WordPad, StepsRecorder, TabletPCMath, extended wallpapers, language features (handwriting/OCR/speech/TTS for the image language).
2. WinSxS rebuilt from a keep-list (rows 27); image cannot be serviced after.
3. WinRE `winre.wim` deleted and replaced by an empty file (row 26).
4. Edge WebView folder removed from WinSxS as well as System32.
5. Windows Update disabled (service, policies, RunOnce stoppers, `UsoSvc`/`WaaSMedicSVC` keys deleted).
6. Defender services disabled and Settings pages hidden.
7. `/ResetBase` cleanup (PS-T also runs it; WinUI Tiny11 does NOT use ResetBase, only WinUI Core does).
8. Uses the legacy Setup path (`Setup\CmdLine`), final image exported as recovery-compressed ESD from index 1.
It removes FEWER bundled apps than PS-T (shorter list in table 1a).

## 3. The offline-image method, in steps

This is the PS-T flow; differences for PS-C and the WinUI app are noted.

1. **Elevate.** Script restarts itself as administrator. The WinUI app has a manifest that always requires admin.
2. **Source.** User mounts the Windows 11 ISO to a drive letter. If `sources\install.wim` or `boot.wim` is missing but `install.esd` exists, convert it: `Export-WindowsImage` (PS-T, max compression, CheckIntegrity) or `dism /Export-Image /Compress:max` (PS-C).
3. **Copy the whole ISO tree** to a work folder (`<scratch>\tiny11`), clear read-only on files, delete the ESD copy. PS-C hard-codes the system drive for this; PS-T accepts a scratch drive letter.
4. **Pick the edition index** (`Get-WindowsImage`; WinUI auto-selects "Windows 11 Pro", else index 1). Take ownership of `install.wim` (`takeown`, `icacls /grant <Administrators>:(F)`), clear read-only.
5. **Mount** the chosen index to `<scratch>\scratchdir` (`Mount-WindowsImage`; PS-C uses `dism /mount-image`). The WinUI app mounts a one-edition working copy, with optional `/Optimize` lazy mount (off by default after a 12 minute failed probe).
6. **Read image facts**: `dism /Get-Intl` for the UI language code, `dism /Get-WimInfo` for architecture (x64 becomes `amd64`).
7. **Remove provisioned apps** (table 1a). PS-C then removes CBS packages by pattern from `/Get-Packages` (table 1c), optionally enables NetFX3 from `sources\sxs`.
8. **Delete files** (Edge, WebView, OneDriveSetup; PS-C also WinRE and WinSxS rebuild). Each needs `takeown` plus `icacls /grant` first because TrustedInstaller owns them.
9. **Load offline hives** with `reg load` into temporary `HKLM\z*` keys: COMPONENTS, DEFAULT (`config\default`), NTUSER (`Users\Default\ntuser.dat`), SOFTWARE, SYSTEM, all under `<mount>\Windows\System32\config`. Apply tweaks (tables 1e, 1d) with `reg add` / `reg delete`. Delete scheduled-task XML files from `Windows\System32\Tasks`.
10. **Copy `autounattend.xml`** into `<mount>\Windows\System32\Sysprep\`.
11. **Unload hives** with `reg unload` (all five). The scripts do not wait or retry; a lingering handle makes unmount fail.
12. **Component store cleanup**: `dism /Image:<mount> /Cleanup-Image /StartComponentCleanup /ResetBase`. In the WinUI app, ResetBase only for Core; it is skipped with a warning if DISM returns 0x800F0806 (pending operations).
13. **Unmount and commit**: `Dismount-WindowsImage -Save` (PS-T) or `dism /unmount-image /commit` (PS-C; WinUI adds `/CheckIntegrity`).
14. **Export (recompress)**: `dism /Export-Image` of the edition to `install2.wim`, then delete the old and rename. PS-T uses `/Compress:recovery`; PS-C exports max-compressed WIM, then converts to `install.esd` with recovery compression at the end. WinUI offers recovery ESD, maximum WIM, or fast WIM.
15. **boot.wim**: take ownership, mount **index 2** (the Setup image), load hives, write the hardware-check bypass values (row 48; PS-C also `Setup\CmdLine`), unload, unmount with commit. Index 1 (WinPE) is untouched.
16. **Answer file** also copied to the ISO root as `autounattend.xml`.
17. **Build the ISO with oscdimg**, UDF 1.02, boot data for BIOS plus UEFI: options `-m -o -u2 -udfver102` and `-bootdata:2#p0,e,b<root>\boot\etfsboot.com#pEF,e,b<root>\efi\microsoft\boot\efisys.bin` (the WinUI app drops the BIOS entry if `etfsboot.com` is missing and uses `-bootdata:1#pEF,e,b<efisys.bin>`).
18. **Cleanup**: delete work folders, eject the source ISO, delete downloaded oscdimg and autounattend.

### Where oscdimg comes from
- Scripts: use the ADK copy if `C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\<arch>\Oscdimg` exists. Otherwise download from Microsoft's symbol server (`msdl.microsoft.com/download/symbols/oscdimg.exe/<hash>/oscdimg.exe`) to the script folder, delete afterward. The script does not verify a checksum.
- WinUI app: finds an installed one, otherwise downloads the symbol-server copy (a different hash path from the script) and refuses it unless SHA-256 equals `2000160B2C5044691B2F9A0AC308E5207F273D4880A572457AF16D05886BA861` (version 2.56, per usage.md). Hosts are restricted to Microsoft's symbol hosts, redirects included.

### Hardware-check bypass
Two places: (a) `LabConfig` + `MoSetup` values in the SYSTEM hive of **boot.wim index 2**, which Setup reads, and (b) the same values in `install.wim` so in-place upgrades also pass; DEFAULT/NTUSER `UnsupportedHardwareNotificationCache` zeroed to hide the "unsupported hardware" watermark.

### Unattend file contents (`autounattend.xml`, script version)
- `windowsPE` pass, component `Microsoft-Windows-Setup`: `DynamicUpdate/WillShowUI = OnError`; `ImageInstall/OSImage` with `Compact = true`, `WillShowUI = OnError`, install from `/IMAGE/INDEX` value `1`; `UserData/ProductKey/Key` left empty.
- `oobeSystem` pass, `Microsoft-Windows-Shell-Setup`: `OOBE/HideOnlineAccountScreens = true`.
- A second Shell-Setup component with `ConfigureChatAutoInstall = false` sits outside any `<settings>` element (structurally odd; effect unclear).
- WinUI generator differs: `HideOEMRegistrationScreen = true`, `HideOnlineAccountScreens` only if "prefer local account", `DynamicUpdate/Enable = false`, `Compact` from recipe, no ProductKey element, no password/user/EULA injection. It is architecture-aware (amd64 or arm64); the script file hard-codes amd64.

## 4. Known failures from open issues (20 open at fetch)

| Issue | Symptom | Cause / state | Relevance to NitrOS |
|---|---|---|---|
| #660 | `DISM failed (exit 1314)` at "Mounting the working copy" | Attached dism.log shows `0x80070522` "client does not hold a required privilege" from `WimApplyPrivileges` during `/Mount-Image` of `working.wim` (DISM 10.0.26100.8972). Maintainer reply: none. Why the privilege was missing: **unclear** | Do the mount from a truly elevated process, enable SeBackup/SeRestore/SeSecurity/SeTakeOwnership privileges, and check before mounting. 1314 = ERROR_PRIVILEGE_NOT_HELD (general Windows knowledge) |
| #664 | `dism.exe does not have a trusted Authenticode signature` (0x800B0101) | The app validates tool signatures; fails for custom-compiled Server 2025 ARM64 image on Parallels. State: open, no reply | If we add signature checks, allow user override or only warn |
| #659 | Edge still in installed programs but broken | Reopened old issue #59. WinUI added offline uninstall-key cleanup, but deletion is skipped if pending operations defer the Edge file removal | Remove the Uninstall keys together with the files; verify in image |
| #653 | Build fails on UUP dump 26H2 ISO: "Capability batch did not prove removal ... Uninstall Pending" for StepsRecorder, Handwriting/OCR/Speech/TTS en-US, MathRecognizer, WMP | Offline removals can finish only at first boot; app treated pending as failure (docs now say "deferred") | Treat `Uninstall Pending` as deferred, never as failure; component cleanup then returns 0x800F0806 |
| #648, #645 | Booted Core ISO: "Windows cannot read the `<ProductKey>` setting from the answer file", Setup exits (real hardware and VMs, Japanese 26H2, Surface 2017) | Only Core ISOs; root cause **unclear**. Community workaround: in a VM, disconnect the DVD right after the logo so Setup misses the answer file, reconnect, Rescan. Does not help real hardware | Do not ship Core-style legacy Setup; an unattend with an empty or absent ProductKey path may be involved, unproven |
| #640, #647 | `Cannot set offline registry value ...SearchSettings\IsDynamicSearchBoxEnabled` (Win32 error 5) in v0.1.1 | Protected default-user key; app 0.1.3 stopped writing it and uses the `EnableDynamicContentInWSB` machine policy | Write only machine policies for search highlights; do not write protected user prefs offline |
| #641, #650 | "image is build 26300 (servicing base 26100) but installed image tools are 10.0.19041.3636 / 10.0.22621.1" | Host DISM too old (Windows 10 host) | Check the DISM/servicing-stack version against the image before mounting; ADK Deployment Tools fixes it |
| #639 | Download SSL error | Official ISO download TLS handshake failure; app retries with TLS 1.2 | Not relevant if we use a local ISO |
| #658, #665 | Users ask whether updated media works and for slipstreaming updates | No answer in thread | Core cannot take updates |
| (docs) | Known issues listed in usage.md | Edge remnants in Settings; winget may need update before installs; Outlook and Dev Home may reappear; arm64 has no `OneDriveSetup.exe` in System32 so the delete errors harmlessly | Same traps apply to our offline mode |
| (docs) | 0x800F0806 (CBS_E_PENDING) from `/StartComponentCleanup` | Pending operations (often after .NET 3.5 or capability removal, or UUP media) | Run cleanup BEFORE enabling features and skip file deletes when pending |

Other issues (#646 navigation, #651/#652/#656 localisation, #642 font, #662/#661 feature requests, #663 scoop, #668 naming) are UI or requests with no bearing on image content.

## 5. Facts that matter for a handheld gaming build

- Do not take row 4 (Xbox apps) or row 23 (WebView2) for the ROG Xbox Ally X: both are called out by the repo as breaking gaming sign-in or apps.
- Do not take rows 26, 27, 40, 44, 45 (Core) if you want Windows to keep updating, keep Defender and keep Reset.
- The apps and policies in rows 1-3, 5-13, 15-16, 52, 56-63 are the lowest-risk set for fewer idle processes. No repo number shows how much RAM they save: **unclear, measure it ourselves**.
- Services are only disabled by `Start=4` in the repo's Custom mode; none of the presets disables game-relevant services other than Update/Defender in Core.
