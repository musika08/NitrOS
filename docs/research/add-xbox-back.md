# Add Xbox Back: Restoration Methods and Verification

Research date: 2026-10-28 (Plan 4, Task 13).
Sources verified from Microsoft primary documentation and packages.

## Context
Max Lean applies `store-remove`, `xbox-app-remove`, `game-bar-remove`, `xbox-sign-in-remove`, `gaming-services-remove` and turns `xbox-services-off`.
Because package removals (`appx.remove`) cannot be reversed by `nitros undo` (the application payloads are uninstalled from all users), NitrOS provides the "Add Xbox back" action (`nitros restore-xbox --yes`) to restore the Microsoft Store and Xbox stack together.

## Candidates from Microsoft Sources Only

All packages and downloads originate strictly from Microsoft servers:

1. **Microsoft Store (`Microsoft.WindowsStore`)**
   - **Method A (In-box):** `wsreset.exe -i`
     Documentation: https://learn.microsoft.com/windows/apps/develop/tools/wsreset (checked 2026-10-28).
     Triggers Windows in-box component store to reinstall the Store package without external downloads.
   - **Method B (WinGet):** `winget install --id 9MWPM2CQNLHN --source msstore --accept-source-agreements --accept-package-agreements --silent`
     Source: Microsoft Store catalog ID `9MWPM2CQNLHN`.
   - **Method C (In-box Staging Appx Re-registration):**
     `Get-AppxPackage -AllUsers *WindowsStore* | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml"}`

2. **Xbox Identity Provider (`Microsoft.XboxIdentityProvider`)**
   - **WinGet:** `winget install --id 9WZDNCRD1HKW --source msstore --accept-source-agreements --accept-package-agreements --silent`
     Product link: https://apps.microsoft.com/detail/9wzdncrd1hkw (checked 2026-10-28).

3. **Xbox Game Bar (`Microsoft.XboxGamingOverlay`)**
   - **WinGet:** `winget install --id 9NZKPSTSNW4P --source msstore --accept-source-agreements --accept-package-agreements --silent`
     Product link: https://apps.microsoft.com/detail/9nzkpstsnw4p (checked 2026-10-28).

4. **Gaming Services (`Microsoft.GamingServices`)**
   - **WinGet:** `winget install Microsoft.GamingServices --source msstore --accept-source-agreements --accept-package-agreements --silent`
   - **Official URI protocol:** `start ms-windows-store://pdp/?productid=9MWPM2CQNLHN`
     Source: https://support.xbox.com/en-US/help/games-apps/troubleshooting/troubleshoot-games-windows-10 (checked 2026-10-28).

5. **Xbox App (`Microsoft.GamingApp`)**
   - **WinGet:** `winget install --id 9MV0B5HZVK9Z --source msstore --accept-source-agreements --accept-package-agreements --silent`
   - **Official Microsoft Installer:** `https://aka.ms/XboxInstaller` (`XboxInstaller.exe`).
     Source: https://www.xbox.com/en-US/apps/xbox-app-for-pc (checked 2026-10-28). Automatically bootstraps Store, Gaming Services, and Xbox App.

6. **Xbox Services Restoration**
   - In-box services disabled by `xbox-services-off`:
     - `XblAuthManager` (Xbox Live Auth Manager)
     - `XblGameSave` (Xbox Live Game Save)
     - `XboxNetApiSvc` (Xbox Live Networking Service)
   - Windows default startup configuration: `Manual` (Demand Start).
   - Restore action sets all three services to `Manual` using Windows Service Control Manager (`ChangeServiceConfigW` or `Set-Service`).

## Selected Architecture
- Engine: `XboxRestore` in `NitrOS.Live`
  - Restores Xbox services to Windows default (`Manual`) first.
  - Calls `IXboxRestoreSteps` methods in order: Store -> Xbox Sign-In -> Game Bar -> Gaming Services -> Xbox App.
  - Verifies each component through `LiveSystems` (services config and AppX presence).
- Windows Implementation: `WinXboxRestore` in `NitrOS.Live.Windows` executes via `wsreset.exe -i` and `winget`.
- CLI Interface: `nitros restore-xbox --yes` guarded by `SessionGuard.Check`.
