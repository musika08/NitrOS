# Notes for later plans

Date: 2026-10-24. Collected from the final whole-branch review of plan 2 (the catalog). None of this changes the catalog; it is what the later plans need to know.

## Plan 3 (engines)

- The engines follow rules Core does not check:
  - An appx start-up task name is written `<PFN>!<TaskId>`.
  - `*` wildcards are allowed in start-up names.
  - A `run` start-up entry under HKLM is read from the 32-bit view.
  - A `program.uninstall` `args` value is appended to the stored uninstall command.
- Per-user service templates (CDPUserSvc, BluetoothUserService) need the template changed and also their existing `_xxxxx` instances (for example `CDPUserSvc_87fa4`).
- `program.uninstall` must handle several matching entries (ASUS Ambient HAL shows up in both registry views) and compare publishers case-insensitively.
- `power.set` has no scheme field, so apply `high-performance-plan`'s scheme first, and record which scheme was written so undo can put it back.
- Decide which user's HKCU an elevated run writes (the signed-in user, not the elevated account).
- Steam's real install path comes from the registry, not a fixed folder.
- Recall already has a "disabled with payload removed" state on some PCs; the engine must treat it as already done.
- Armoury Crate may come back until the reinstall block ships (ASUS Update Helper is left installed on purpose). See `reinstall-block.md` (2026-10-25): Dolby, Realtek and AMD apps come back through their drivers' Store links. Armoury Crate is tied to the BIOS device `ACPI\ASUS9001`. A proposed `device-companion-apps-off` switch is written up there, not in the catalog.
- Defender writes can be refused while Tamper Protection is on.
- DISM's provisioned-app query is denied for the elevated admin on the owner's PC, while SYSTEM still works. DISM's Appx provider probably fails opening a registry key for writing; the log names no key or access mode. Likely cause: Shadow Defender, installed 2026-10-20 (see `dism-access-denied.md`). The engines must treat 0x80070005 as "refused", not "no packages", name Shadow Defender when it is present, skip only the provisioned part, and never change permissions. The elevated token is also refused write access to `HKLM\SOFTWARE` itself (confirmed 2026-10-25, Win32 error 5), but `HKLM\SOFTWARE\Policies`, where most catalog policy switches write, is writable. Plain `HKLM\SOFTWARE` values outside `Policies` may fail, so test-open HKLM target keys for writing before applying.
- Done in plan 3 (2026-10-25, final review fixes): the "must do" items above. Before a switch runs, the engine test-opens for writing every HKLM key its `registry.set`, `registry.delete` and HKLM `startup.disable` actions write (the nearest existing parent for a missing key, nothing written). If Windows refuses one, the whole switch is Skipped with "Windows refuses changes to <key> on this PC" and none of its actions run. `appx.remove` treats access denied (0x80070005) from the provisioned query or the deprovision as "built-in app list refused": the removal for users still runs, the deprovision is skipped, and the action is Applied with "Windows refused to change the built-in app list". Naming Shadow Defender in that message is not done yet.
- `ExcludeWUDriversInQualityUpdate` = 1 is already set on the owner's PC (set by Winhance 2026-10-19), so Windows Update gives it no drivers. Device updates must cover drivers, or the owner clears it.
- A kernel driver `D` from `C:\WINDOWS\TEMP\D.sys` had 1,139 installs from 2026-10-20 03:02:16 to 2026-10-24 15:03:12 (1,395 events for service `D` from 2026-10-19 17:51:23 to 2026-10-24 23:03:07). Which program loads it is unknown; do not blame NitrOS for it.

## Plan 4 (VM)

- Location off versus automatic time zone: does the time zone stop updating?
- Defender removal live, with Tamper Protection on.
- The search web-view flag 1694661260.
- Background-apps force-deny versus the Xbox app and full-screen mode.
- Dolby tuning without the Dolby app.
- WebView2 after the Edge image removal (the runtime updater folders are now kept).
- App Installer is NonRemovable; check what the removal really does.
- Whether the svchost data value 4294967295 behaves like "installed RAM".
- Task 11: remove Dolby Access or Realtek Audio Control, then restart and install a newer driver. Does the app come back with and without `PreventDeviceMetadataFromNetwork` = 1?
- Task 11: on a clean VM, record the permissions on `Appx`, `AppModel` and `InboxApplications`. Then install Shadow Defender 1.5.0.726 and rerun `tools/verify/collect-dism-evidence.ps1` to confirm or rule it out.
- Task 11, on the Ally with the owner: look in the BIOS for an Armoury Crate download option, and log what `ArmouryCrateControlInterface` and `ASUSSoftwareManager` do after Armoury Crate is removed.
- Task 11: `ExcludeWUDriversInQualityUpdate` = 1 is already set on the owner's PC, so Windows Update offers no drivers. Check that Device updates still covers every driver and firmware item, or have the owner clear the value.
- Task 11: on the owner's PC, elevated write-opens are refused on `HKLM\SOFTWARE` (confirmed, Win32 error 5), while `HKLM\SOFTWARE\Policies` is writable. Run `tools/verify/collect-dism-evidence.ps1` on a clean VM to compare, and check that every live HKLM catalog key outside `Policies` can be written on the Ally.
- Task 11, open question for the owner: which program installs the kernel driver `C:\WINDOWS\TEMP\D.sys` (service `D`) over and over (1,139 installs from 2026-10-20 03:02:16 to 2026-10-24 15:03:12)?
- Kept from the plan 3 final review (2026-10-25):
  - Deprovisioning under the owner's permission state on `Appx`, `AppModel` and `InboxApplications`: does `DeprovisionPackageForAllUsersAsync` work there, or is it refused like DISM? (`PackageManager.FindProvisionedPackages()` works on the owner's PC, 31 packages; only DISM's provisioned query is refused.)
  - Edge `--force-uninstall` outside the EEA: does it really remove Edge, and what comes back?
  - Protected services and tasks (for example ones only SYSTEM or TrustedInstaller may change): how the refusal looks, and that the engine records it as Failed with a clean rollback.
  - Uninstallers that show a window even when asked to be quiet: list them and decide how to handle them. msiexec uninstalls are now always run with `/qn /norestart` when the catalog gives no args.

## Plan 5 (app)

- `BuildIso` `AppliesWholeCard` is false for every card, because some switches are live-only. Do not show "card partly applied" for that.
- Recommended in Tune mode shows about 30 "can't be undone" warnings. Group them into one.
- From the plan 3 final review (2026-10-25), the engine now provides these; the app must show them:
  - `TuneResult.MayBeChanged`: switches that may still have changed something, including a failed irreversible action or script whose Apply ran. Show it after a run, and `TuneResult.Problems` (the failure detail per switch).
  - Undo and drift refuse a journal written by another Windows account (`JournalOwnerException`, "This journal belongs to another account."). Offer only this account's journals.
  - A run lock (`Global\NitrOS.Tune`) is held for the whole of Apply and Undo; a second run is refused with "Another NitrOS change is already running." Disable the buttons while a run is in progress.
  - `DriftChecker` errors are kept in `LastErrors` on the checker; return them with the result instead of storing them on the object.
  - Never create `%ProgramData%\NitrOS` from a non-elevated process (the secured folder must be made by the elevated run).

## Plan 6 (release)

- Re-check the dropped "not on this PC" rows against a mounted stock ISO. The identifiers were checked on an already-debloated PC that had no provisioned-package list.
