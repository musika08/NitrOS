# Identifier checks for the switch inventory

Date: 2026-10-24.
PC: the owner's ASUS ROG Xbox Ally X, Windows 11 Pro, DisplayVersion 26H2, build 26300.9711 (`[Environment]::OSVersion` 10.0.26300.0).
Inventory: `docs/research/switch-inventory.md`, frozen by this check.

## Method

1. `tools/verify/collect-identifiers.ps1` (read-only) ran elevated on the PC and wrote `%USERPROFILE%\NitrOS-identifiers.json`: AllUsers Appx packages (161), services, scheduled tasks, optional features (138), capabilities (424), the three Uninstall roots, the HKLM and HKCU Run keys, both Startup folders, `powercfg /list` and `powercfg /qh`, and the OS version.
   - **Gap:** `Get-AppxProvisionedPackage -Online` returned "Access is denied." even elevated, and `DISM /Online /Get-ProvisionedAppxPackages` was denied too, so no provisioned list exists for this PC. Provisioned-only checks used the AllUsers list, the `AppxAllUserStore\Deprovisioned` registry list (package families that shipped with this build and were removed earlier by Winhance/winutil), the package family names in Windows' own `AppxPackageManager.admx`, and winutil plus Winhance agreement.
2. Extra read-only checks on the same PC, not elevated: registry values (`Get-ItemProperty`), `AppxAllUserStore\Deprovisioned`, Appx manifests (start-up tasks, display names), `C:\Windows\servicing\Packages`, folders under `Program Files (x86)`.
3. Policy values were checked against Windows' own templates in `C:\Windows\PolicyDefinitions` on build 26300 (parsed for key, value name, class and enabled/disabled data), then Microsoft Learn: WindowsAI Policy CSP, Microsoft Edge browser and update policy pages (one page per Edge policy), Manage Notepad, OneDrive Group Policy, Power setting GUIDs, Customize Windows RE, Security guidelines for disabling system services.
4. Vendor sources: Valve Developer Community (Steam command-line options), Playnite's command-line page and its GitHub source.
5. Rule (task 2 brief): a row is VERIFIED when every identifier it uses is on this PC with the exact name, or stated by a primary source, or agreed by winutil and Winhance in the research files. Unverifiable actions inside an otherwise checked row were removed (listed as corrections); a row whose core identifier could not be checked was DROPPED. Tiny11 alone, Winhance alone and winutil alone did not count. Registry values present on this PC count as "present"; note that this PC was already tuned with Winhance and winutil, so presence proves the name, not that Windows reads it, which is why policy values were checked against the templates instead.

## Counts per group

| Group | Rows | VERIFIED | DROPPED |
|---|---|---|---|
| bundled-apps | 43 | 31 | 12 |
| manufacturer-addons | 10 | 5 | 5 |
| edge-onedrive | 2 | 2 | 0 |
| windows-ai | 12 | 10 | 2 |
| xbox | 5 | 5 | 0 |
| security-updates | 3 | 3 | 0 |
| big-parts | 7 | 7 | 0 |
| background-services | 18 | 18 | 0 |
| privacy-ads | 14 | 14 | 0 |
| gaming-settings | 9 | 9 | 0 |
| startup | 10 | 6 | 4 |
| **Total** | **133** | **110** | **23** |

133 = the 132 candidate rows plus the new `bluetooth-off`. `windows-update-off` was replaced by `windows-update-auto-off` (same row, new id and actions).

## DROPPED rows

| Group | id | Reason |
|---|---|---|
| bundled-apps | mail-calendar-remove | Package `microsoft.windowscommunicationsapps` is Winhance/Tiny11 only and not on this PC; `Windows Mail\PreventRun` is in no policy template (PreventRun belongs to Windows Messenger) |
| bundled-apps | maps-remove | Package `Microsoft.WindowsMaps` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | tips-remove | Package `Microsoft.Getstarted` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | people-remove | Package `Microsoft.People` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | family-remove | Package `MicrosoftCorporationII.MicrosoftFamily` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | skype-remove | Package `Microsoft.SkypeApp` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | viewer-3d-remove | Package `Microsoft.Microsoft3DViewer` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | paint-3d-remove | Package `Microsoft.MSPaint` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | mixed-reality-portal-remove | Package `Microsoft.MixedReality.Portal` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | cortana-remove | Package `Microsoft.549981C3F5F10` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | onenote-legacy-remove | Package `Microsoft.Office.OneNote` is Winhance/Tiny11 only and not on this PC |
| bundled-apps | wallet-remove | Package `Microsoft.Wallet` is Tiny11 only and not on this PC |
| manufacturer-addons | intel-security-status-remove | Package `AppUp.IntelManagementandSecurityStatus` is Tiny11 only and not on this PC |
| manufacturer-addons | other-oem-apps-remove | No uninstall entry, publisher or package for any non-ASUS maker could be checked |
| manufacturer-addons | oem-reinstall-block | Owner decision 2026-10-24: waits for plan 3 (spec section 11) |
| manufacturer-addons | wpbt-off | Owner decision 2026-10-24: waits for plan 3 (spec section 11) |
| manufacturer-addons | nvidia-panel-background-off | No NVIDIA app start-up entry or package could be checked (AMD PC; none in the research) |
| windows-ai | windows-ai-core-remove | Owner decision 2026-10-24: needs winutil's EndOfLife removal method, which v1 does not ship |
| windows-ai | office-ai-off | No Office AI value has a checked key, name and data |
| startup | teams-startup-off | Teams' start-up task id could not be checked (Teams not on this PC, no id in the research) |
| startup | phone-link-startup-off | Phone Link's start-up task id could not be checked (not on this PC, no id in the research) |
| startup | oem-tray-apps-off | No per-maker tray entry list could be checked; the only ASUS entries belong to Armoury Crate's set |
| startup | snipping-tool-resident-off | `PrintScreenKeyForSnippingEnabled` was a guess; not on this PC and in no source |

## Corrections inside VERIFIED rows

- **Removed, single-source and not on this PC:** `MicrosoftTeams`, `Microsoft.Windows.Teams`, Teams `DisableInstallation` (teams-remove); `UScheduler_Oobe\OutlookUpdate` (outlook-new-remove); `Microsoft.Windows.CrossDevice` (phone-link-remove); `Microsoft.WidgetsPlatformRuntime`, `Microsoft.StartExperiencesApp` (widgets-remove); capability `App.Support.QuickAssist` (quick-assist-remove); `Microsoft.Windows.Copilot`, `Microsoft.Windows.Ai.Copilot.Provider`, `MicrosoftWindows.Client.CoPilot` (copilot-remove); `MicrosoftWindows.Client.AIX` (click-to-do-off, now policy only and reversible); `Microsoft.WritingAssistant` (ai-helper-apps-remove, renamed "Remove AI Manager"); `Microsoft.XboxApp` (xbox-app-remove); `Microsoft.XboxGameOverlay` (game-bar-remove); `ProgramDataUpdater` task (telemetry-tasks-off); `AllowAutoGameMode` (game-mode-on); `GlobalUserDisabled` (background-apps-off); `SysWOW64\OneDriveSetup.exe` (onedrive-remove).
- **Removed, not in any policy template or Microsoft Learn:** `TurnOffSavingSnapshots` (recall-off); `DisableAgentConnectors`, `DisableAgentWorkspaces`, `DisableRemoteAgentConnectors`, `AllowCopilotRuntime`, `LetAppsAccessGenerativeAI` (ai-agents-off); `DisableGenerativeErase`, `DisableRemoveBackground` (paint-ai-off); `DevToolsGenAiSettings` (edge-ai-off).
- **Removed, obsolete or deprecated per Microsoft Learn:** `CopilotCDPPageContext` (edge-ai-off); `EdgeCollectionsEnabled`, `WebWidgetAllowed`, `WalletDonationEnabled` (edge-tracking-off).
- **Wrong value fixed:** Paint AI policy key is `HKLM\Software\Microsoft\Windows\CurrentVersion\Policies\Paint`, not `SOFTWARE\Policies\Microsoft\Paint` (WindowsCopilot.admx). `TurnOffWindowsCopilot` is a user policy, so only the HKCU write stays. Recall tasks are `\Microsoft\Windows\WindowsAI\Recall\InitialConfiguration` (and `PolicyConfiguration`, kept running), not `RecallConfiguration`/`RecallPipeline`. The appraiser task on 26300 is `Microsoft Compatibility Appraiser Exp`. Maps auto-update uses the policy `SOFTWARE\Policies\Microsoft\Windows\Maps\AutoDownloadAndUpdateMapData`=0 (WinMaps.admx) instead of winutil's `SYSTEM\Maps\AutoUpdateEnabled`. The Defender image action no longer uses `Windows-Defender-Client-Package` (absent from `servicing\Packages` on 26300); the image applies the live actions offline.
- **Values filled from this PC or a source:** `Microsoft.XboxInsider`, `RealtekSemiconductorCorp.RealtekAudioControl`, `Microsoft.GamingServices`, `B9ECED6F.ASUSPCAssistant` (Appx, not Win32), the Armoury Crate packages and five uninstall entries, the AMD panel start-up task `AdvancedMicroDevicesInc-2.AMDRadeonSoftware_0a9344xs7nr4m!launcherrsxruntimeTask`, `Printing-PrintToPDFServices-Features`, the four Search web-view override values, the power subgroup and setting GUIDs, the Edge auto-launch Run value (`MicrosoftEdgeAutoLaunch_*`), Steam `-tenfoot`, Playnite `Playnite.FullscreenApp.exe`.
- **Data questions settled:** `DisableClickToDo`=1, `DisableSettingsAgent`=1, `LetAppsRunInBackground`=2 (force deny), `AllowTelemetry`=1 (required), `DODownloadMode`=99, `KFMBlockOptIn`=1 (HKLM), `GenAILocalFoundationalModelSettings`=1, both CloudContent user policies =1, `ChatIcon`=3.

## Content decisions applied (owner, 2026-10-24)

- `virtualization-off` cards off/off/off/on.
- `windows-update-off` became `windows-update-auto-off`, Max Lean only: one policy value (`AU\NoAutoUpdate`=1); services stay startable; the description says the user can still check.
- `bluetooth-off` added to big-parts: all cards off, risk breaks, breaks "Wireless controllers, headsets and earbuds stop working."
- Dropped `windows-ai-core-remove`, `oem-reinstall-block`, `wpbt-off`.
- `svchost-grouping-on` uses fixed data `4294967295`, cards off/pending/pending/on. No other row depended only on computed data.
- Every `NEEDS ACTION TYPE` replaced with a task 1 type (`program.uninstall`, `startup.disable`, `registry.delete`, `power.scheme`, `launch.at-logon`) or the row dropped.
- `requires` follows the spec's meaning: `xbox-app-remove` lists `store-remove` and `gaming-services-remove`; `game-recording-background-off` lists nothing; no other row lists anything.
- The proposed card defaults were accepted as written.

## Left for the engines and the VM

- `startup.disable` names: `appx-task` uses `<PackageFamilyName>!<TaskId>`; `run` names may contain `*`; HKLM Run values can sit in the 32-bit view (`WOW6432Node`). Core does not check these; the PC tuner must.
- `program.uninstall` appends `args` to the entry's own uninstall command (Edge `--force-uninstall`) and must shield the OneDrive folder.
- Defender: DisableAntiSpyware is reported ignored since platform 4.18.2007.8, and Tamper Protection blocks live service writes; prove in the VM.
- Search web-view override data (`EnabledState`=1) is Winhance's off state as seen on this PC, not a Microsoft-documented value.
- Removed single-source packages (Widgets runtime, Start Experiences, older Teams names, AIX, Copilot client packages, the retired apps) can come back once seen on a clean 24H2 or 25H2 image in the measuring VM.
