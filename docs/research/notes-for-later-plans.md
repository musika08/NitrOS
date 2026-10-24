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
- Armoury Crate may come back until the reinstall block ships (ASUS Update Helper is left installed on purpose).
- Defender writes can be refused while Tamper Protection is on.
- DISM online servicing returned Access Denied even elevated on the owner's PC (possibly Shadow Defender). Investigate before relying on DISM for live removals.

## Plan 4 (VM)

- Location off versus automatic time zone: does the time zone stop updating?
- Defender removal live, with Tamper Protection on.
- The search web-view flag 1694661260.
- Background-apps force-deny versus the Xbox app and full-screen mode.
- Dolby tuning without the Dolby app.
- WebView2 after the Edge image removal (the runtime updater folders are now kept).
- App Installer is NonRemovable; check what the removal really does.
- Whether the svchost data value 4294967295 behaves like "installed RAM".

## Plan 5 (app)

- `BuildIso` `AppliesWholeCard` is false for every card, because some switches are live-only. Do not show "card partly applied" for that.
- Recommended in Tune mode shows about 30 "can't be undone" warnings. Group them into one.

## Plan 6 (release)

- Re-check the dropped "not on this PC" rows against a mounted stock ISO. The identifiers were checked on an already-debloated PC that had no provisioned-package list.
