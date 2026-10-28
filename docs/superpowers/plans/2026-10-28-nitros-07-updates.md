# NitrOS Plan 7: Device Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `NitrOS.Updates`, which lists BIOS, firmware and driver updates for this PC from three layers of sources (Windows Update, the maker, the GPU vendor), merges them so the newest version wins, and installs one item only when the user presses Install, from the maker's own server, after its signature is checked. Add the Device updates screen (spec 4, screen 7) to the app on top of plan 5's navigation.

**Architecture:** One source interface, `IUpdateSource`, turns a `DeviceInfo` (maker, model, BIOS, firmware and driver versions read from Windows) into `UpdateItem`s. Sources: `WindowsUpdateSource` (Windows Update Agent COM API), `AsusSource` (the ASUS support-site JSON listing), `GpuVendorSource` (only for vendors the research task proves have a machine-readable listing) and `SupportPageSource` (everything else: a button that opens a page). `UpdateChecker` runs the sources, `Merger` joins items that target the same device or firmware resource, `KnownBadList` blocks pulled versions. `UpdateInstaller` downloads over HTTPS from an allow-listed host, checks the SHA-256 when the listing gives one and the Authenticode signer against the source's maker, then installs, or hands the verified file to the user for BIOS and firmware until their silent install is proven. Every Windows touch point sits behind a small interface with a fake; the real adapters are read-only on the owner's PC. The app reaches all of it through one port, `IDeviceUpdates`, in `NitrOS.App.Logic`.

**Tech Stack:** .NET 10 (SDK 10.0.402), C#, `net10.0-windows10.0.19041.0`. Windows Update Agent through late-bound COM (`Microsoft.Update.Session`). `System.Net.Http.HttpClient` and `System.Text.Json` from the base library. `Windows.Devices.Enumeration.Pnp.PnpObject` (from the Windows TFM) for installed drivers, with a CfgMgr32 fallback. WinVerifyTrust through `NitrOS.Live`'s existing code, moved to a public `Authenticode` class. No new NuGet packages. xUnit v3 as before. App: plan 5's `NitrOS.App.Logic` and `NitrOS.App` (WinUI 3).

**Spec:** `docs/superpowers/specs/2026-10-24-nitros-design.md` (sections 4 screen 7, 5, 5.4, 10, 11). Also `docs/research/device-updates.md`, `reinstall-block.md`, `ally-x-inventory.md`, `docs/research/notes-for-later-plans.md`, plan 3 (`2026-10-25-nitros-03-tuner.md`) and plan 5 (`2026-10-28-nitros-05-app.md`, "How plans 6 and 7 plug in").

**This is plan 7 of 7.** Plans 1 to 3 are merged. Plan 4 (measurement VM) runs the VM lane. Plan 5 (app screens) must be built first: this plan registers `ScreenKeys.DeviceUpdates` in plan 5's `Navigator` and `PageMap`. Plan 6 (ISO builder) is independent of this plan.

## Facts checked for this plan (2026-10-28)

| What | Finding | Source |
|---|---|---|
| WUA search criteria | `Type='Driver'`, `IsInstalled=0`, `IsHidden=0`, `BrowseOnly`, `CategoryIDs contains`; default is `IsInstalled=0 and IsHidden=0`; `OR` only at top level | https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-search (updated 2024-02-22) |
| WUA driver fields | `IWindowsDriverUpdate`: `DriverClass`, `DriverHardwareID`, `DriverManufacturer`, `DriverModel`, `DriverProvider`, `DriverVerDate`, `DeviceProblemNumber`, `DeviceStatus`. No driver version property. | https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iwindowsdriverupdate (updated 2024-02-22) |
| WUA session, download, install | ProgID `Microsoft.Update.Session`; `CreateUpdateSearcher`, `CreateUpdateDownloader`, `CreateUpdateInstaller`; collection ProgID `Microsoft.Update.UpdateColl`; `EulaAccepted` and `AcceptEula()`; `InstallationBehavior.CanRequestUserInput`; result codes 2 Succeeded, 3 Succeeded with errors, 4 Failed, 5 Cancelled; `ServerSelection` 2 is Windows Update, 3 with Microsoft Update service id `7971f918-a847-4430-9279-4a52d1efe18d` | https://learn.microsoft.com/en-us/windows/win32/wua_sdk/searching--downloading--and-installing-updates (updated 2025-03-11) |
| `ExcludeWUDriversInQualityUpdate` | "Do not include drivers with Windows Updates"; all or nothing | Update Policy CSP, https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-update (as cited in `reinstall-block.md`, checked 2026-10-25) |
| **WUA driver search with that policy on** (live, read-only, owner's PC) | `ExcludeWUDriversInQualityUpdate` = 1 is set. A WUA search `IsInstalled=0 and Type='Driver'` still returned 3 items, with `ServerSelection` 0 and 2 alike: "Realtek - Net - 11.19.20.602" (USB\VID_0BDA&PID_8153, driver date 2018-03-30), "ASUSTeK COMPUTER INC. Firmware Driver Update (10.1.2.20)" (UEFI\RES_{cbf5a488-...}, ITE PD), "ASUSTeK COMPUTER INC. Firmware Driver Update (10.1.2.319)" (UEFI\RES_{68e0fa36-...}, system firmware). So the policy stops automatic driver installs but not an API search. Whether a WUA **install** of such an item works with the policy on is not checked (VM lane, Task 14). | PowerShell COM search on the owner's PC, 2026-10-28 |
| Firmware versions on the owner's Ally X (read-only) | ESRT under `HKLM\HARDWARE\UEFI\ESRT\{guid}`: system firmware `{68e0fa36-5d2a-5046-91c6-e0bfe84f77f3}` Type 1 Version 793 (= 0x319, BIOS `RC73XA.319`), its firmware driver package 10.1.2.317 (stale: the BIOS was flashed by ASUS's tool). ITE PD `{cbf5a488-f654-3fce-b3fc-8a1d94ead498}` Version 21 (= 0x15), package 10.1.2.15. NOVATEK touch panel `{222a79d6-d2aa-358b-b1ac-8771faf8fec0}` Version 9, package 10.1.2.2002. So the BIOS number and the PD number read as hex equal the ESRT version; the touch panel does not follow that rule. | `Get-ItemProperty` and `Get-PnpDevice` on the owner's PC, 2026-10-28 |
| Device identity (read-only) | `HKLM\HARDWARE\DESCRIPTION\System\BIOS`: `SystemManufacturer` "ASUSTeK COMPUTER INC.", `SystemProductName` "ROG Xbox Ally X RC73XA_RC73XA", `BaseBoardProduct` "RC73XA", `BIOSVersion` "RC73XA.319", `BIOSReleaseDate` "09/23/2026". GPU "AMD Radeon(TM) 890M Graphics" driver 32.0.32015.7029, `PCI\VEN_1002&DEV_150E&SUBSYS_33591043&REV_C7`. Controller `USB\VID_0B05&PID_1B4C&REV_0002` (REV is not the MCU version 326); an "ASUS Component Firmware Update" HID device `HID\VID_0B05&PID_1B4C&MI_04` exists. | Read on the owner's PC, 2026-10-28 |
| ASUS listing shape (live, GET only) | Both URLs in `device-updates.md` return 200 with `{ "Result": { "Count", "Obj": [ { "Name", "Count", "Files": [ ... ] } ] }, "Status": "SUCCESS", "Message" }`. File fields: `Id`, `Version`, `Title`, `Description`, `FileSize` ("15.36 MB"), `ReleaseDate` ("2026/10/14"), `IsRelease`, `DownloadUrl.Global`, `ExeModule` ("ASUS_RC73XA_319_BIOS_Update.exe%%/SUPPRESSMSGBOXES /VERYSILENT /NORESTART"), `Reboot`, `Ac_power`, `Severity`, `Sign`, `sha256`, `HardwareInfoList` ([{ "hardwareid" }] or null). BIOS listing objects: "BIOS Update(Windows)" 319, "BIOS" (EZ Flash zip) 319, "Firmware" (Touch_Panel_FW 2002, ROG MCU Firmware Update tool V326 with no `ExeModule` and `Ac_power` false, PD_FW 0020). Several versions per object, newest first. Driver listing includes "ASUS System Control Interface v3" V3.1.72.0 (hardware ids `ACPI\ASUS2018`, `ACPI\ASUS2025`), "Armoury Crate Control Interface" (`ACPI\VEN_ASUS&DEV_9001`), "Armoury Crate SE Installer", Microsoft Store links (MyASUS, GlideX) and "MyASUS in WinRE for USB" (1.07 GB). Downloads are under `https://dlcdnets.asus.com/pub/ASUS/...`. | `curl` GET on 2026-10-28 (saved only to the controller's scratchpad, not the repo) |
| WinVerifyTrust revocation | `WTD_REVOKE_WHOLECHAIN` = 1; `dwProvFlags` `WTD_REVOCATION_CHECK_CHAIN` = 0x40, `WTD_REVOCATION_CHECK_NONE` = 0x10, `WTD_CACHE_ONLY_URL_RETRIEVAL` = 0x1000 | https://learn.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-wintrust_data (updated 2024-11-20) |
| Support pages | `https://rog.asus.com/support/`, `https://www.asus.com/support/download-center/`, `https://www.amd.com/en/support/download/drivers.html` and `https://www.nvidia.com/en-us/drivers/` answer 200. Intel's download center and `https://www.msi.com/support/download` answer 403 to a script, so they are "verify first" in a browser (Task 1). | `curl` on 2026-10-28 |

Not confirmed, so each is a "verify first" step in its task: GPU vendors' version sources (spec 11), ASUS's updater switches and exit codes and how to read the MCU version (spec 11), the signer names on ASUS's downloads, whether `dynamic` late binding to WUA works under .NET 10 with `TreatWarningsAsErrors`, the ESRT registry layout being documented, `PnpObject` property keys, and launching a browser or Explorer unelevated from an elevated process.

## Rulings (choices the spec left open)

1. **Project references.** `NitrOS.Updates` references `NitrOS.Live` (and Core through it) for one reason: the Authenticode check and publisher matching already reviewed in plan 3. Task 3 moves that code into a public `NitrOS.Live.Windows.Authenticode` class; `WinUninstaller` calls it unchanged. Updates keeps its own Windows adapters in `src/NitrOS.Updates/Windows/`.
2. **Windows Update with the "no drivers" policy on.** NitrOS never reads-and-changes the policy. When `ExcludeWUDriversInQualityUpdate` is 1, the screen says so in plain words and still searches Windows Update when the user presses Check (the live check above shows the search still returns drivers and firmware).
3. **Which Windows Update service.** `ServerSelection` 2 (Windows Update) for the search and install. The owner's PC gave the same list for 0 and 2; 2 avoids a company WSUS server hiding drivers.
4. **Merge key.** Items join when they target the same thing: `dev:<device instance id>` for a driver matched to a present device by hardware id, `fw:<ESRT guid>` for firmware tied to an ESRT resource, otherwise `src:<sourceId>:<title>` (never joined). Newest version wins and its source is shown. **On a tie, Windows Update wins**, because it installs through Windows' own path with no maker installer and no unproven switches.
5. **Firmware versions are compared in ESRT space when proven.** A small embedded `firmware-map.json` says, per maker and model, which listing title belongs to which ESRT resource and which rule turns a version into the ESRT number (`EsrtHex`: the listing number read as hex; `PackageLastField`: the last field of the installed firmware package). A rule is used only when it holds on this PC right now (a self-check, Task 6); otherwise the installed version shows as "can't tell". This stops Windows Update offering BIOS 10.1.2.319 on an Ally already flashed to 319 by ASUS's tool.
6. **What the ASUS source offers.** Only items downloadable from `dlcdnets.asus.com`. Kept objects: "BIOS Update(Windows)", "Firmware", and the driver categories. Dropped: the EZ Flash "BIOS" zip, "OS Assistant", Microsoft Store links, and every "Software and Utility" item except "ASUS System Control Interface". Always dropped by name (case-insensitive `*` patterns): `Armoury Crate*`, `MyASUS*`, `GlideX*`, `Aura*`, `ROG Live Service*`, `Dolby*`. Armoury Crate is removed on every card, so Device updates must never bring it or its control-interface driver back.
7. **ASUS System Control Interface is always kept.** Device updates has no remove or roll-back action of any kind, and the ASUS source never filters this item out. It may be offered as an update like any driver.
8. **Install paths by kind and source.**
   - Windows Update items (drivers and firmware): WUA download and install, as Windows Settings' optional updates do. Firmware needs the charger and the warning.
   - ASUS drivers: run the downloaded installer with the switches the listing gives after `%%` in `ExeModule`, only after Task 14's VM test proves them on at least one ASUS driver package; until then, hand off.
   - ASUS BIOS, firmware and MCU, and GPU vendor packages: **hand off**: download, check hash and signature, then the user starts the maker's updater in its own window from NitrOS ("Start the updater", no switches, while NitrOS still holds the checked file locked) or opens its folder. Never a silent run until `docs/research/asus-updaters.md` proves the switches and exit codes, and the owner agrees.
9. **Support pages for brands without a source.** ASUS (when the listing fails or the model is unknown), MSI, Lenovo, Dell and HP get a "Open <maker>'s support page" button. Spec 10 keeps Lenovo, Dell and HP listings out of version 1; a link is not a listing. Other makers get the sentence "NitrOS has no update list for <maker>. Look on your maker's website." Pages open in the signed-in user's unelevated browser; if that cannot be done (Task 11), the address is shown with a Copy button. NitrOS never starts a browser elevated.
10. **Downloads folder.** `%ProgramData%\NitrOS\downloads`, made by `BackupFolder.Ensure` only when the user presses Install (plan 5 ruling 3: the secured folder is made only by a run). A finished install deletes its file; a hand-off keeps it until the next Check.
11. **Signatures on downloads check revocation online** (`WTD_REVOKE_WHOLECHAIN`, `WTD_REVOCATION_CHECK_CHAIN`, no cache-only flag). If revocation cannot be confirmed, the file is refused with "Could not confirm the maker's signature is still valid. Try again when online." Plan 3's uninstaller keeps its offline check.
12. **A restore point before every install**, through plan 3's `IRestorePoint`. Unavailable does not block; the result line says no restore point was made.
13. **Charger rule.** BIOS and firmware Install buttons are disabled with "Connect the charger first." when the PC has a battery and is not on mains power, and the check is repeated right before the run. Desktops (no battery) pass.
14. **Known-bad list** is an embedded resource `src/NitrOS.Updates/KnownBad/known-bad.json`, not a catalog file (Core's loader owns `catalog/`). A blocked newest version falls back to the newest unblocked version that is still newer than the installed one.
15. **Versions that cannot be parsed** (ASUS "latest version at the MS store") drop the item from the ASUS source; for other sources they give state "Can't tell".
16. **Closing during an install** is refused like a run (plan 5 ruling 12), with its own text (Task 12 adds a reason overload to `RunGate`; see the plan 5 seam note in Task 12).

## Global Constraints

- **Plan 6 lands before this plan.** Where this plan edits a plan 5 or Live file, start from the version plan 6 left. Plan 6 added `ScreenKeys.IsoSource`, `IsoRunner`, `Draft.Iso`, the Live `Authenticode` wrapper and `IPowerSource`, and it rewrote some plan 5 composition tests. Keep all of plan 6's tests passing.
- Plan 1 to 6 constraints still hold: SDK pin in `global.json`, `Nullable`, `TreatWarningsAsErrors`, xUnit v3 Exe test projects, no `using Xunit;` (global using in the test `.csproj`), namespaces mirror folders. `NitrOS.Core` stays free of Windows APIs. `NitrOS.App.Logic` has no WinUI reference (plan 5's `Logic_HasNoWinUiReference` keeps passing).
- **Commits carry no Claude attribution** (owner rule, 2026-10-24): no `Co-Authored-By` trailer, no "Generated with" line. Before committing, `git config user.email` must print `225994527+musika08@users.noreply.github.com`. Each commit stages only its task's paths.
- **Nothing in this plan changes the owner's real Windows** (an ASUS ROG Xbox Ally X, Windows 11 build 26300). Tests on the owner's PC only read, or write to sandbox targets they create and remove themselves (the test's own temp folder; `HKCU\Software\NitrOS.Tests`). Anything that installs, downloads into `%ProgramData%`, creates a restore point or changes a driver carries `[Trait("Lane","Vm")]` and `Assert.SkipUnless(Environment.GetEnvironmentVariable("NITROS_VM") == "1", "VM lane only")`.
- **BIOS and MCU flashing is never automated in any test, in the VM or on the Ally.** No test calls a BIOS, firmware or MCU updater, and no test installs a Windows Update item whose `DriverClass` is `Firmware`. The only firmware step is the owner's own choice at the final OWNER CHECKPOINT (Task 14).
- **Live network tests only read.** Tests that call ASUS or Windows Update for real carry `[Trait("Lane","LiveNet")]` and `Assert.SkipUnless(Environment.GetEnvironmentVariable("NITROS_LIVE_NET") == "1", "live network lane only")`. They send GET requests or a WUA search, never a download of an installer, never an install. Every other test uses recorded fixtures and fakes.
- Any step that needs admin on the owner's PC is a marked **OWNER CHECKPOINT**. The controller runs it through a UAC-elevated `.ps1` that writes its output to `%USERPROFILE%`. A restart needs the owner's explicit OK first. Never change Defender settings or permissions (ACLs) on the owner's PC. Never change the `ExcludeWUDriversInQualityUpdate` policy or any Windows Update setting.
- **Nothing installs without Install.** No view model or service downloads, installs, creates a folder, makes a restore point or opens a page from a constructor, from `OnShown`, or from Check. Only the Install, hand-off and page buttons do, after the confirms this plan names.
- **No background updater** (spec 5.4). Checks run only when the Device updates screen opens or the user presses Check. Nothing registers a task, service, timer or start-up entry.
- **Downloads only from the maker's own host.** HTTPS only. Redirects are followed by hand (at most 5) and every hop must stay on the source's allowed hosts (`dlcdnets.asus.com` for ASUS; GPU hosts from Task 1). Any other host, any `http:` hop or a size over the listing's size plus 10% → refused, nothing kept.
- **Signature before running.** No downloaded file is started unless its SHA-256 matches the listing (when given) and `Authenticode.TrustedSignerNames` on the locked file returns a name that `Authenticode.NamesMatch` pairs with one of the source's expected signer names. The file stays open with `FileShare.Read` from the check until its process has started.
- Do not guess APIs, versions, package names, flags or download URLs. Where a task says "verify first", do it and write the source and date into the task's commit message body (and the research doc for Tasks 1 and 2). If it fails, stop and report NEEDS_CONTEXT.
- **Research downloads need the owner's OK.** Tasks 1 and 2 download installers only to inspect them. Before each download, the controller tells the owner the file name, source URL and size, and waits for a yes. Nothing downloaded is ever run on the owner's PC, and each file is deleted after it is inspected.
- G-Helper (GPL-3.0) may be read for knowledge of the ASUS endpoints. No code is copied from it.
- Plain English in every on-screen text: short sentences, no em-dashes, no emojis. On-screen texts that tests assert live in `src/NitrOS.App.Logic/Texts.cs` (plan 5) or `src/NitrOS.Updates/UpdateTexts.cs`.

## Review Focus

1. A download whose host, redirect, hash or signer is wrong never runs, and the file is deleted. (Task 11, `Install_RefusesOtherHost`, `Install_RefusesRedirectOffHost`, `Install_RefusesHashMismatch`, `Install_RefusesWrongSigner`)
2. BIOS and firmware need the charger, show the "do not turn off" warning, and are handed to the user, never run silently. (Task 11, `Install_FirmwareNeedsCharger`, `Install_BiosIsHandedOffNotRun`; Task 12, `DeviceUpdates_FirmwareInstallAsksWithWarning`)
3. Version comparison and merging: BIOS "319" vs "310", dotted drivers, installed newer than offered, Windows Update's stale BIOS package. (Task 4 `Version_*`, Task 6 `Merge_*`)
4. Nothing happens until Install, and checks run only on open and Check. (Task 12, `DeviceUpdates_NothingInstallsUntilInstall`, `DeviceUpdates_ChecksOnlyOnOpenAndCheck`)
5. Armoury Crate never comes back through Device updates, and the System Control Interface is never dropped. (Task 8, `Asus_DropsArmouryCrateAndStoreLinks`, `Asus_KeepsSystemControlInterface`)
6. A broken maker listing falls back to the support page with a reason. (Task 8, `Asus_*FallsBackToSupportPage`)

---

### Task 1: Research GPU vendors' driver versions (verify first, no code)

**Files:**
- Create: `docs/research/gpu-driver-sources.md`
- Modify: `docs/superpowers/specs/2026-10-24-nitros-design.md` (section 11, the "How to check AMD, NVIDIA and Intel's latest driver versions" line: mark answered and link the doc)

- [x] **Step 1:** For each of AMD, NVIDIA and Intel, find whether the vendor publishes a machine-readable way to learn the latest driver version for a given GPU (device id, or product family): an official, documented API or feed first; then an endpoint the vendor's own site or app uses (undocumented, like ASUS's). For each candidate, record: the URL pattern, the request it needs, a real response captured with GET only on this date (store it under `docs/research/evidence/gpu-<vendor>-<date>.json` or `.txt`), the version field, the release date field, the download URL field and its host, the file size field, whether the response is per device id, and whether it is documented. Use web search and fetch. Use only GET requests.
- [x] **Step 2:** For each vendor, write a verdict: **Reliable** (documented, or undocumented but returning the AMD Radeon 890M / device `PCI\VEN_1002&DEV_150E` or the equivalent test GPU correctly for both the latest and one older version) or **Page only**. Spec 11 says this is unverified; if a vendor has no reliable machine-readable source, the verdict is Page only and NitrOS opens its driver page. Check each vendor's driver page URL in a browser-like fetch (Intel and MSI refuse plain scripts) and record the final URL.
- [x] **Step 3:** For every Reliable vendor, record the download host(s) and the expected Authenticode signer name of the installer, as shown by Microsoft's signature properties or `Get-AuthenticodeSignature` on a downloaded copy in a scratch folder outside the repo (download only the smallest installer the vendor offers; delete it after). Record whether the vendor's own install switches and exit codes are documented, with the source.
- [x] **Step 4:** For AMD on handhelds, note whether AMD's generic driver supports the Ally X's GPU (the owner runs 32.0.32015.7029 "from AMD", newer than ASUS's 32.0.32015.5008), with the source.
- [x] **Step 5:** Update spec section 11 and commit with the message `Research GPU vendor driver version sources`.

---

### Task 2: Research ASUS updaters, signers and the MCU version (verify first, no code)

**Files:**
- Create: `docs/research/asus-updaters.md`, `tools/verify/read-ally-firmware.ps1`
- Modify: `docs/superpowers/specs/2026-10-24-nitros-design.md` (section 11: the MCU-version line and the "BIOS and MCU updaters" line), `docs/research/device-updates.md` (add the 2026-10-28 WUA search result and the ESRT findings from this plan's "Facts checked" table)

- [x] **Step 1: Updaters.** From ASUS's FAQ pages, ASUS's listing (`ExeModule` gives "/SUPPRESSMSGBOXES /VERYSILENT /NORESTART" after `%%` for the BIOS, PD and touch-panel updaters and for drivers), and the installers' own help (`/?` or the Inno Setup documentation, if the files prove to be Inno Setup installers by their version resource), record for the BIOS updater, the PD and touch-panel firmware updaters, the ROG MCU Firmware Update tool and one driver package: the installer technology, documented silent switches, documented exit codes, whether it restarts the PC by itself, whether it needs mains power, and whether it shows progress. Download each to a scratch folder outside the repo with GET, then inspect only (version resource, signature, `/?` is **not** run). Record the URL, size, SHA-256 and whether it matches the listing's `sha256`. Delete the files after. Nothing is run.
- [x] **Step 2: Signers.** For the same files, record the leaf signer common name and organisation as WinVerifyTrust reports them (through a small read-only use of `Authenticode.TrustedSignerNames` once Task 3 lands, or `Get-AuthenticodeSignature` now). Decide the ASUS source's expected signer list. If any ASUS-hosted file (for example the AMD graphics package) is signed by its chip vendor, not ASUS, write it down as an open question for the owner; until answered, the ASUS source accepts only ASUS's own signer and such items are refused with a plain reason.
- [x] **Step 3: MCU version without Armoury Crate.** Find how the Ally X controller firmware version can be read. Candidates to check, from documentation first: Microsoft's Component Firmware Update (CFU) protocol (the "ASUS Component Firmware Update" HID device `HID\VID_0B05&PID_1B4C&MI_04`, driver 10.82.0.24, is present), PnP device properties of the controller's devices (`DEVPKEY_Device_FirmwareVersion` was empty on 2026-10-28; `REV_0002` is not the MCU number), files or registry values the ASUS updater writes, and what G-Helper documents (knowledge only). Write `tools/verify/read-ally-firmware.ps1`: read-only, prints ESRT entries, firmware PnP devices with driver versions, and any MCU version candidate it can read **without sending anything to the device**.
- [x] **Step 4: OWNER CHECKPOINT (only if the owner agrees).** If a candidate needs a HID feature-report read (a CFU "get firmware version" query), describe exactly what is sent and why it cannot change the firmware, with the CFU documentation link, and ask the owner before running it on the Ally. The controller runs the script through its UAC-elevated `.ps1` runner with output in `%USERPROFILE%\nitros-ally-firmware.txt`. Record the result. If the owner says no, the MCU stays "can't tell" in version 1.
- [x] **Step 5:** Write the conclusions: for each kind, "silent install proven: yes/no" with evidence; the MCU read method or "not found". Until a kind says yes **and** the owner agrees, its install path stays the hand-off (ruling 8). Update spec 11 and `device-updates.md`. Commit with the message `Research ASUS updaters, signers and the controller firmware version`.

---

### Task 3: Projects and the shared signature check

**Files:**
- Create: `src/NitrOS.Updates/NitrOS.Updates.csproj`, `tests/NitrOS.Updates.Tests/NitrOS.Updates.Tests.csproj`, `tests/NitrOS.Updates.Tests/SmokeTests.cs`
- Modify: `NitrOS.slnx`, `src/NitrOS.Live/Windows/Authenticode.cs` and `tests/NitrOS.Live.Tests/Windows/AuthenticodeTests.cs` (both created by plan 6 Task 1), `src/NitrOS.Live/Windows/WinUninstaller.cs`, `src/NitrOS.Live/Windows/NativeMethods.cs`

Plan 6 Task 1 already made `Authenticode` a thin public wrapper (`TrustedSignerNames(string path)`, `SignedBy(string path, string organisation)`) that calls `WinUninstaller`. This task moves the code itself into `Authenticode` and turns the direction round: `Authenticode` holds the code and `WinUninstaller` forwards to it. Keep `SignedBy` and plan 6's Authenticode tests passing; do not duplicate tests plan 6 already has (for example its unsigned temp file test).

**Interfaces:**
- Produces:
  - Project `NitrOS.Updates`: class library, `net10.0-windows10.0.19041.0`, references `NitrOS.Live` and `NitrOS.Core`, `InternalsVisibleTo NitrOS.Updates.Tests`. Embedded resources `KnownBad\known-bad.json` and `Firmware\firmware-map.json` (added in Task 6).
  - `NitrOS.Updates.Tests`: xUnit v3 Exe, same framework and package versions as `NitrOS.Live.Tests` (`xunit.v3` 4.0.2, `Microsoft.NET.Test.Sdk` 18.11.0, `xunit.runner.visualstudio` 4.0.1), references Updates, Live, Core; `<None Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />`.
  - In Live, namespace `NitrOS.Live.Windows`: `public enum RevocationMode { OfflineOnly, WholeChainOnline }` and `public static class Authenticode` with:
    - `SafeFileHandle OpenLocked(string path)` (moved from `WinUninstaller`, same behaviour: read, `FileShare.Read`)
    - `IReadOnlyList<string>? TrustedSignerNames(SafeFileHandle handle, string path, RevocationMode mode = RevocationMode.OfflineOnly)` and the `string path` overload (moved; `OfflineOnly` is today's `WTD_REVOKE_NONE` plus `WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL`; `WholeChainOnline` is `WTD_REVOKE_WHOLECHAIN` plus `WTD_REVOCATION_CHECK_CHAIN`, values in the "Facts checked" table)
    - `string NormaliseName(string name)` and `bool NamesMatch(string a, string b)` (moved, same suffix and alias rules)
  - `WinUninstaller.NormaliseName`, `NamesMatch`, `TrustedSignerNames` and `OpenLocked` stay as one-line forwards, so plan 3's tests are untouched. New constants `WTD_REVOKE_WHOLECHAIN = 1`, `WTD_REVOCATION_CHECK_CHAIN = 0x40` in `NativeMethods`.

- [x] **Step 1:** Write the failing tests:
  - `UpdatesSmoke_ReferencesLive`.
  - `Authenticode_SignedSystemFileHasMicrosoftSigner` (read-only: `%windir%\System32\notepad.exe` or `msiexec.exe` returns a name that `NamesMatch` pairs with "Microsoft Windows" or "Microsoft Corporation"; both modes).
  - `Authenticode_UnsignedFileReturnsNull` (a text file in the test's temp folder).
  - `Authenticode_NamesMatchUnchanged` (the plan 3 cases: "Valve Corp." = "Valve Corporation", "ASUS" = "ASUSTeK COMPUTER INC.", "Valve GmbH" is not "Valve Corporation").
- [x] **Step 2:** Run `dotnet test --filter "UpdatesSmoke|Authenticode"`. Expected: build fails.
- [x] **Step 3:** Create the projects, add both to `NitrOS.slnx` (`/src/` and `/tests/`), move the code into `Authenticode` and make `WinUninstaller` forward to it.
- [x] **Step 4:** Run `dotnet test`. Expected: all pass, including every plan 3 `WinUninstaller` test.
- [x] **Step 5:** Commit with the message `Add NitrOS.Updates projects and share the signature check`.

---

### Task 4: Update items, versions, sizes and dates

**Files:**
- Create: `src/NitrOS.Updates/Model/UpdateKind.cs`, `Model/UpdateItem.cs`, `Model/DeviceInfo.cs`, `Model/MakerId.cs`, `Versions/VersionText.cs`, `Versions/FirmwareNumber.cs`, `Model/ListingParse.cs`, `src/NitrOS.Updates/UpdateTexts.cs`, tests `tests/NitrOS.Updates.Tests/Versions/VersionTextTests.cs`, `Versions/FirmwareNumberTests.cs`, `Model/ListingParseTests.cs`, `Model/MakerIdTests.cs`

**Interfaces:**
- Produces, namespace `NitrOS.Updates.Model`:
  - `public enum UpdateKind { Bios, Firmware, Driver }`
  - `public enum MakerId { Asus, Msi, Lenovo, Dell, Hp, Other }` and `public static class Makers { MakerId FromManufacturer(string manufacturer); }` using `Authenticode.NamesMatch` against "ASUSTeK COMPUTER INC.", "Micro-Star International Co., Ltd.", "LENOVO", "Dell Inc.", "HP" / "Hewlett-Packard" (verify each manufacturer string against the maker's SMBIOS value from a public source in Step 1; record it).
  - `public sealed record InstalledDevice(string InstanceId, string Name, string? DeviceClass, IReadOnlyList<string> HardwareIds, string? DriverVersion, DateOnly? DriverDate, string? DriverProvider)`
  - `public sealed record FirmwareResource(Guid Id, int Type, uint Version, uint LastAttemptVersion, uint LastAttemptStatus, string? PackageVersion, string? Name)` (`Type` 1 is system firmware, 2 device firmware)
  - `public sealed record DeviceInfo(MakerId Maker, string Manufacturer, string Model, string? ModelCode, string? BiosVersion, DateOnly? BiosDate, IReadOnlyList<InstalledDevice> Devices, IReadOnlyList<FirmwareResource> Firmware)` with `IReadOnlyList<InstalledDevice> Gpus` = devices whose class is `Display`.
  - `public enum InstallMethod { WindowsUpdate, MakerPackage, HandOff, PageOnly }`
  - `public sealed record UpdateItem(string Key, string Name, UpdateKind Kind, string? InstalledVersion, string? LatestVersion, Uri? DownloadUrl, long? SizeBytes, DateOnly? ReleaseDate, string SourceId, string SourceName)` with init-only extras: `string? Sha256`, `InstallMethod Method`, `string? SilentArgs`, `Uri? Page`, `string? Note`, `string? WindowsUpdateId` (WUA `UpdateID`), `bool RestartLikely`, `string? CompareRule` (null, `EsrtHex` or `PackageLastField`). Spec 5.4's seven fields are the positional ones.
- Produces, namespace `NitrOS.Updates.Versions`:
  - `public static class VersionText`:
    - `bool TryParse(string? text, out IReadOnlyList<long> parts)`: trims, drops one leading `V`/`v`, takes the part after the last `.` when the text starts with a model code followed by `.` and digits only (`RC73XA.319` → `[319]`), splits on `.`, each part decimal digits only (leading zeros allowed: `0020` → 20). Anything else (letters inside parts, empty) → false.
    - `int? Compare(string? a, string? b)`: null when either fails to parse; otherwise part by part, missing trailing parts count as 0 (`1.2.31` = `1.2.31.0`).
    - `string? FromTitle(string title)`: the last match of `\d+(\.\d+){1,3}` in a Windows Update title, or null.
  - `public static class FirmwareNumber`: `bool TryEsrtHex(string? listingVersion, out uint esrt)` (the digits of the version's last part read as hex: `319` → 0x319 = 793, `0020` → 32, `10.1.2.319` → 793); `bool TryPackageLastField(string? packageVersion, out long last)`.
- Produces, namespace `NitrOS.Updates.Model`: `public static class ListingParse`: `long? SizeBytes(string? text)` ("15.36 MB" → 15.36 × 1024 × 1024 rounded; KB, MB, GB, case-insensitive; else null), `DateOnly? AsusDate(string? text)` (`yyyy/MM/dd`, invariant culture).

- [x] **Step 1: Verify first.** Look up the SMBIOS `SystemManufacturer` strings for MSI, Lenovo, Dell and HP in each maker's or Microsoft's documentation (or a public hardware database page); record the sources in the commit body. ASUS is "ASUSTeK COMPUTER INC." (read on the owner's PC).
- [x] **Step 2:** Write the failing tests, each named case asserted exactly:
  - `Version_BiosNumbers` ("319" > "310"; "RC73XA.319" = "319"; "V326" > "V325").
  - `Version_DottedDrivers` ("V32.0.32015.5008" < "32.0.32015.7029"; "11.19.20.602" < "11.19.602.2025"; "1.2.31.0" = "1.2.31"; "V3.1.72.0" = "3.1.72.0").
  - `Version_LeadingZeros` ("0020" > "0015"; "0020" = "20").
  - `Version_UnparseableIsNull` ("latest version at the MS store", "", null, "1.2a" → `Compare` null).
  - `Version_FromWindowsUpdateTitles` ("Realtek - Net - 11.19.20.602" → "11.19.20.602"; "ASUSTeK COMPUTER INC. Firmware Driver Update (10.1.2.319)" → "10.1.2.319"; "No version here" → null).
  - `FirmwareNumber_EsrtHex` ("319" → 793; "0015" → 21; "0020" → 32; "10.1.2.20" → 32; "2002" → 8194).
  - `ListingParse_Sizes` ("15.36 MB", "1.07 GB", "4.2 MB", "818.9 MB", "" → null) and `ListingParse_AsusDate` ("2026/10/14" → 2026-10-14; "14/10/2026" → null).
  - `Makers_FromManufacturer` ("ASUSTeK COMPUTER INC." → Asus, "Micro-Star International Co., Ltd." → Msi, "Contoso" → Other).
- [x] **Step 3:** Run `dotnet test --filter "Version_|FirmwareNumber|ListingParse|Makers_"`. Expected: build fails.
- [x] **Step 4:** Implement.
- [x] **Step 5:** Run `dotnet test`. Expected: all pass.
- [x] **Step 6:** Commit with the message `Add update items, version comparison and listing parsing`.

---

### Task 5: Reading this PC (identity, drivers, firmware, power, policy)

**Files:**
- Create: `src/NitrOS.Updates/Reading/IDeviceReader.cs`, `Reading/DeviceReader.cs`, `Reading/WindowsUpdatePolicy.cs`, `src/NitrOS.Updates/Windows/WinIdentity.cs`, `Windows/WinInstalledDevices.cs`, `Windows/WinFirmwareTable.cs`, `Windows/UpdatesNative.cs`, tests `tests/NitrOS.Updates.Tests/Reading/DeviceReaderTests.cs`, `Reading/WindowsUpdatePolicyTests.cs`, `Windows/WinReadersTests.cs`, `tests/NitrOS.Updates.Tests/Fakes/FakeDeviceSources.cs`, `Fixtures/ally-x-device-2026-10-28.json`

**Interfaces:**
- Produces, namespace `NitrOS.Updates.Reading`:
  - `public interface IIdentitySource { string Manufacturer { get; } string ProductName { get; } string? BaseBoardProduct { get; } string? BiosVersion { get; } string? BiosReleaseDate { get; } }`
  - `public interface IInstalledDevices { IReadOnlyList<InstalledDevice> Present(); }`
  - `public interface IFirmwareTable { IReadOnlyList<FirmwareResource> Read(); }`
  - `public sealed class DeviceReader(IIdentitySource identity, IInstalledDevices devices, IFirmwareTable firmware) : IDeviceReader` with `DeviceInfo Read()`. `ModelCode`: `BaseBoardProduct` when it is letters and digits only; else the last `_`- or space-separated token of `ProductName` that is letters and digits only (`"ROG Xbox Ally X RC73XA_RC73XA"` → `RC73XA`); else null. `BiosDate` from `BiosReleaseDate` (`MM/dd/yyyy`). A reader that throws becomes an empty list for that part, never a crash.
  - `public sealed class WindowsUpdatePolicy(IRegistry registry)`: `bool DriversExcluded()` reads `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate` value `ExcludeWUDriversInQualityUpdate` (dword 1 → true). It only reads; it has no write method.
- Produces, namespace `NitrOS.Updates.Windows` (real adapters, read-only):
  - `WinIdentity : IIdentitySource`: `HKLM\HARDWARE\DESCRIPTION\System\BIOS` values `SystemManufacturer`, `SystemProductName`, `BaseBoardProduct`, `BIOSVersion`, `BIOSReleaseDate` (the same key `WinDeviceFacts` reads).
  - `WinInstalledDevices : IInstalledDevices`: `Windows.Devices.Enumeration.Pnp.PnpObject.FindAllAsync(PnpObjectType.Device, properties)` with the property keys for hardware ids, compatible ids, driver version, driver date, driver provider, class and friendly name, waited with a 60-second timeout. Present devices only.
  - `WinFirmwareTable : IFirmwareTable`: each subkey of `HKLM\HARDWARE\UEFI\ESRT` (`Type`, `Version`, `LowestSupportedVersion`, `LastAttemptVersion`, `LastAttemptStatus`), joined with the present device `UEFI\RES_{guid}\0` for `PackageVersion` and `Name`.
- Consumes (from plan 6 Task 1, namespace `NitrOS.Live.Systems`): `record PowerState(bool HasBattery, bool OnBattery, int? BatteryPercent)`, `interface IPowerSource { PowerState Read(); }` and `NitrOS.Live.Windows.WinPowerSource`. Do not add a second power reader; everywhere this plan says `IPowerSource` or `WinPowerSource` it means plan 6's.

- [ ] **Step 1: Verify first.** (a) The `PnpObject` property key names (`System.Devices.HardwareIds` or the DEVPKEY `{guid} pid` form for `DEVPKEY_Device_HardwareIds`, `DEVPKEY_Device_DriverVersion`, `DEVPKEY_Device_DriverDate`, `DEVPKEY_Device_DriverProvider`, `DEVPKEY_Device_Class`, `DEVPKEY_Device_FriendlyName`) against Microsoft Learn; if `PnpObject` cannot return them for all devices within 60 seconds, use CfgMgr32 (`CM_Get_Device_ID_ListW`, `CM_Locate_DevNodeW`, `CM_Get_DevNode_PropertyW`) instead, verified the same way. (b) Whether Microsoft documents the ESRT registry values (search Learn for "ESRT" and "UEFI\ESRT"); if not, note "observed, not documented" in the code comment and the commit body; the reader then treats a missing key as "no firmware table". (c) nothing for power: plan 6 Task 1 already verified `GetSystemPowerStatus`. Record sources and date.
- [ ] **Step 2:** Record the fixture `Fixtures/ally-x-device-2026-10-28.json`: a `DeviceInfo` for the owner's Ally X written by a throw-away read of the real adapters (identity, the four ESRT resources and their packages from the "Facts checked" table, the GPU, the Realtek USB GbE `USB\VID_0BDA&PID_8153` at 11.19.602.2025, the MediaTek, Genesys, FocalTech, TI devices, the `ACPI\ASUS2018` / `ACPI\ASUS2025` System Control Interface devices and `ACPI\VEN_ASUS&DEV_9001`). Review it by hand: no user names, serial numbers or MAC addresses; drop any instance id part that looks like a serial.
- [ ] **Step 3:** Write the failing tests:
  - `DeviceReader_ModelCodeFromBaseBoard`, `DeviceReader_ModelCodeFromProductName` (no base board), `DeviceReader_NoModelCode` ("Desktop PC" → null).
  - `DeviceReader_ReaderFailureGivesEmptyPart` (fake devices throws; result has firmware and identity, empty devices).
  - `DeviceReader_GpusAreDisplayClass`.
  - `WindowsUpdatePolicy_ReadsExcludeFlag` (FakeRegistry from Live tests, compiled by link as plan 5 does: 1 → true, missing → false) and `WindowsUpdatePolicy_NeverWrites` (FakeRegistry records zero writes).
  - Read-only on this PC: `WinIdentity_ReadsThisPc` (manufacturer not empty), `WinInstalledDevices_FindsADisplayDevice`, `WinFirmwareTable_ReadsWithoutThrowing` (on the Ally, at least one Type 1 entry).
- [ ] **Step 4:** Run `dotnet test --filter "DeviceReader|WindowsUpdatePolicy|WinIdentity|WinInstalledDevices|WinFirmwareTable"`. Expected: build fails.
- [ ] **Step 5:** Implement.
- [ ] **Step 6:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 7:** Commit with the message `Read device identity, drivers, firmware and power for updates`.

---

### Task 6: Known-bad list, firmware map and merging

**Files:**
- Create: `src/NitrOS.Updates/KnownBad/known-bad.json`, `KnownBad/KnownBadList.cs`, `src/NitrOS.Updates/Firmware/firmware-map.json`, `Firmware/FirmwareMap.cs`, `Merging/ItemState.cs`, `Merging/MergedItem.cs`, `Merging/Merger.cs`, tests `tests/NitrOS.Updates.Tests/KnownBad/KnownBadListTests.cs`, `Firmware/FirmwareMapTests.cs`, `Merging/MergerTests.cs`

**Interfaces:**
- Produces, namespace `NitrOS.Updates.KnownBad`:
  - JSON: `{ "entries": [ { "maker": "Lenovo", "model": "*", "kind": "bios", "version": "N3CN42WW", "reason": "Lenovo pulled this BIOS after boot failures.", "source": "https://hwbusters.com/news/lenovo-pulls-the-legion-go-bios-that-was-bricking-handhelds-and-says-nothing-about-it/", "added": "2026-10-24" } ] }`. That one entry comes from `device-updates.md`; Step 1 adds any ASUS Ally pulls found.
  - `public sealed record KnownBadEntry(MakerId Maker, string Model, UpdateKind Kind, string Version, string Reason, string Source)`; `public sealed class KnownBadList(IReadOnlyList<KnownBadEntry> entries)` with `static KnownBadList Shipped()` (embedded resource), `static KnownBadList Parse(string json)`, `KnownBadEntry? Find(MakerId maker, string? modelCode, UpdateKind kind, string version)`. Model `*` or exact, case-insensitive; version equal by `VersionText.Compare == 0`, or ordinal case-insensitive when either does not parse.
- Produces, namespace `NitrOS.Updates.Firmware`:
  - JSON `firmware-map.json`: `{ "models": [ { "maker": "Asus", "model": "RC73XA", "items": [ { "title": "BIOS Update for Windows", "esrt": "system", "rule": "EsrtHex" }, { "title": "PD_FW Firmware", "esrt": "cbf5a488-f654-3fce-b3fc-8a1d94ead498", "rule": "EsrtHex" }, { "title": "Touch_Panel_FW Firmware", "esrt": "222a79d6-d2aa-358b-b1ac-8771faf8fec0", "rule": "PackageLastField" } ] } ] }` (values observed on the owner's PC on 2026-10-28; `"system"` means the ESRT entry with Type 1).
  - `public enum CompareRule { EsrtHex, PackageLastField }`; `public sealed record FirmwareLink(FirmwareResource Resource, CompareRule Rule)`; `public sealed class FirmwareMap` with `static FirmwareMap Shipped()`, `FirmwareLink? ForListing(DeviceInfo device, string listingTitle)`, `FirmwareLink? ForResource(DeviceInfo device, Guid esrtId)`, and `static bool RuleHolds(DeviceInfo device, FirmwareLink link)`:
    - `EsrtHex` on Type 1: `FirmwareNumber.TryEsrtHex(device.BiosVersion)` equals `Resource.Version` (`RC73XA.319` → 793 = 793: holds).
    - `EsrtHex` on Type 2: `TryEsrtHex(Resource.PackageVersion)` equals `Resource.Version` (PD 10.1.2.15 → 21 = 21: holds).
    - `PackageLastField`: the package version parses (touch 10.1.2.2002 → 2002: holds).
  - `static string? InstalledText(FirmwareLink link, DeviceInfo device)`: the installed version in the listing's own format when the rule holds (`EsrtHex` system → the BIOS number, "319"; `EsrtHex` device → hex of `Version`, padded to the listing's width, "0015"; `PackageLastField` → "2002"), else null.
  - `static int? CompareOffered(FirmwareLink link, DeviceInfo device, string offeredVersion)`: under `EsrtHex`, `TryEsrtHex(offered)` against `Resource.Version`; under `PackageLastField`, the offered last field against the package's last field. Null when the rule does not hold.
- Produces, namespace `NitrOS.Updates.Merging`:
  - `public enum ItemState { NewVersion, UpToDate, CantTell, Blocked, PageOnly }`
  - `public sealed record MergedItem(UpdateItem Item, ItemState State, string? BlockedReason, IReadOnlyList<UpdateItem> AlsoOfferedBy)`
  - `public static class Merger { IReadOnlyList<MergedItem> Merge(IReadOnlyList<UpdateItem> items, DeviceInfo device, KnownBadList knownBad, FirmwareMap map); }`. Algorithm (not decided by the signature):
    1. Group by `Key`. Within a group, drop known-bad versions (remember the newest dropped one and its reason).
    2. Winner = highest `LatestVersion` by the group's comparison: for `fw:` keys, `FirmwareMap.CompareOffered` translated into ESRT numbers; otherwise `VersionText.Compare`. Ties go to source id `windows-update` (ruling 4), then to the earlier source in checker order. Items whose versions do not compare with the others are kept in `AlsoOfferedBy`, never chosen over a comparable one.
    3. State: `PageOnly` when the winner's `Method` is `PageOnly`; `Blocked` when every version in the group was known-bad and nothing newer than installed remains; `CantTell` when installed is null or the comparison is null; `UpToDate` when offered ≤ installed; else `NewVersion`.
    4. Output order: NewVersion, then CantTell, Blocked, PageOnly, UpToDate; within each, Bios, Firmware, Driver; then name.

- [ ] **Step 1: Verify first.** Search for ROG Ally, Ally X and Xbox Ally BIOS or firmware versions that ASUS pulled or warned about (ASUS pages, ASUS ROG forum announcements, reputable news). Add each confirmed one with its source to `known-bad.json`; record what was searched in the commit body even when nothing is found.
- [ ] **Step 2:** Write the failing tests (fixtures from Task 5 for the device):
  - `KnownBad_ShippedListParses`, `KnownBad_FindsByMakerModelKindVersion`, `KnownBad_StarModelMatchesAll`.
  - `FirmwareMap_BiosRuleHoldsOnAllyFixture`, `FirmwareMap_PdRuleHolds`, `FirmwareMap_TouchUsesPackageLastField`, `FirmwareMap_RuleFailsWhenBiosAndEsrtDisagree` (fixture copy with ESRT 791: `RuleHolds` false, `InstalledText` null).
  - `Merge_NewestWinsAndSourceShown` (ASUS AMD driver 32.0.32015.5008 and a GPU-vendor item 32.0.33000.1 on the same `dev:` key: GPU item wins, ASUS in `AlsoOfferedBy`).
  - `Merge_InstalledNewerThanOfferedIsUpToDate` (Ally fixture: ASUS AMD 32.0.32015.5008 vs installed 32.0.32015.7029).
  - `Merge_WindowsUpdateOlderThanInstalledIsUpToDate` (WU "Realtek - Net - 11.19.20.602" vs installed 11.19.602.2025).
  - `Merge_WuBiosAlreadyFlashedByMakerToolIsUpToDate` (WU 10.1.2.319 on `fw:{68e0fa36-...}` and ASUS BIOS "319" on the same key; installed BIOS RC73XA.319, ESRT 793, package 10.1.2.317: state UpToDate, not NewVersion).
  - `Merge_PdFirmwareNewVersion` (WU 10.1.2.20 and ASUS "0020" on `fw:{cbf5a488-...}`; ESRT 21: NewVersion; tie on ESRT number 32 goes to Windows Update).
  - `Merge_UnknownInstalledIsCantTell` (MCU "V326" with no installed version).
  - `Merge_KnownBadNewestFallsBackToOlderNewer` (319 blocked, 318 offered, installed 317: winner 318, NewVersion) and `Merge_AllBlockedIsBlockedWithReason`.
  - `Merge_UnparseableNeverBeatsParseable`.
  - `Merge_OrderNewFirstThenKind`.
- [ ] **Step 3:** Run `dotnet test --filter "KnownBad|FirmwareMap|Merge_"`. Expected: build fails.
- [ ] **Step 4:** Implement.
- [ ] **Step 5:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 6:** Commit with the message `Add known-bad list, firmware map and update merging`.

---

### Task 7: Windows Update source

**Files:**
- Create: `src/NitrOS.Updates/Sources/IUpdateSource.cs`, `Sources/SourceResult.cs`, `Sources/WindowsUpdateSource.cs`, `Sources/WuRecord.cs`, `src/NitrOS.Updates/Windows/IWindowsUpdateAgent.cs`, `Windows/WinUpdateAgent.cs`, tests `tests/NitrOS.Updates.Tests/Sources/WindowsUpdateSourceTests.cs`, `Fakes/FakeUpdateAgent.cs`, `Fixtures/wu-ally-x-2026-10-28.json`, `tests/NitrOS.Updates.Tests/LiveNet/WindowsUpdateLiveTests.cs`

**Interfaces:**
- Produces, namespace `NitrOS.Updates.Sources` (**the one source interface**):

  ```csharp
  public enum SourceStatus { Ok, Unavailable, NotForThisDevice }

  public sealed record SourceResult(SourceStatus Status, IReadOnlyList<UpdateItem> Items, string? Problem, Uri? FallbackPage);

  public interface IUpdateSource
  {
      string Id { get; }            // "windows-update", "asus", "gpu-amd", "page-msi", ...
      string Name { get; }          // shown: "Windows Update", "ASUS", "AMD"
      bool AppliesTo(DeviceInfo device);
      /// <summary>Reads only. Never throws for a network or format problem: returns Unavailable with a plain Problem.</summary>
      Task<SourceResult> CheckAsync(DeviceInfo device, CancellationToken ct);
  }
  ```
- Produces, namespace `NitrOS.Updates.Windows`:
  - `public sealed record WuRecord(string UpdateId, int Revision, string Title, string? DriverClass, string? DriverHardwareId, string? DriverManufacturer, string? DriverProvider, string? DriverModel, DateTime? DriverVerDate, long? MaxDownloadSize, bool BrowseOnly, bool EulaAccepted, bool CanRequestUserInput, int RebootBehavior)` (plain data; fixtures are lists of it).
  - `public interface IWindowsUpdateAgent { Task<IReadOnlyList<WuRecord>> SearchDriversAsync(CancellationToken ct); Task<WuInstallResult> InstallAsync(string updateId, IProgress<string>? progress, CancellationToken ct); }`; `public sealed record WuInstallResult(int ResultCode, int HResult, bool RebootRequired)`.
  - `WinUpdateAgent`: `Microsoft.Update.Session` created with `Type.GetTypeFromProgID` and used through `dynamic`, on a dedicated STA thread per call, `ClientApplicationID = "NitrOS"`, searcher `ServerSelection = 2`, criteria `IsInstalled=0 and IsHidden=0 and Type='Driver'`. Search timeout 3 minutes (`ct` linked). `InstallAsync` searches `UpdateID='<id>'`, refuses (`ResultCode` 4, detail) an update with `CanRequestUserInput` true or `EulaAccepted` false (NitrOS never accepts a licence for the user), then `CreateUpdateDownloader` and `CreateUpdateInstaller` with a one-item `Microsoft.Update.UpdateColl`. COM errors become `WuInstallResult` with the HRESULT, or an `InvalidOperationException` for the search, which the source turns into `Unavailable`.
- `WindowsUpdateSource(IWindowsUpdateAgent agent)`: `Id` "windows-update", `Name` "Windows Update", applies to every device. Each record becomes an `UpdateItem`:
  - Kind: `DriverClass` "Firmware" → `Bios` when the hardware id is `UEFI\RES_{guid}` of the ESRT Type 1 entry, else `Firmware`; anything else → `Driver`.
  - Key: `fw:<guid>` for `UEFI\RES_{guid}`; `dev:<instance id>` of the present device whose hardware or compatible ids contain `DriverHardwareId` (case-insensitive); otherwise `src:windows-update:<UpdateId>`.
  - Installed: for `fw:` keys, `FirmwareMap.InstalledText` when a link exists, else the resource's `PackageVersion`; for `dev:` keys the device's `DriverVersion`.
  - Latest: `VersionText.FromTitle(Title)`; `ReleaseDate` from `DriverVerDate`; `SizeBytes` from `MaxDownloadSize`; `DownloadUrl` null; `Method` `WindowsUpdate`; `WindowsUpdateId` = `UpdateId`; `RestartLikely` = `RebootBehavior > 0`; `CompareRule` from the map link.
  - Name: `Title`.

- [ ] **Step 1: Verify first.** (a) That `dynamic` late binding to a COM object created from a ProgID compiles and runs under .NET 10 on Windows with `TreatWarningsAsErrors` (Microsoft Learn on COM interop and `dynamic` in .NET); if it does not, declare the few `wuapi` interfaces with `[ComImport]` from the IDL GUIDs on Learn instead, and say which in the commit body. (b) The `IUpdateDownloader.Download`, `IUpdateInstaller.Install`, `IUpdate.InstallationBehavior.RebootBehavior` and `IUpdateSearcher.ServerSelection` reference pages. (c) Whether WUA calls must run on an STA thread. Record the sources and date.
- [ ] **Step 2:** Record `Fixtures/wu-ally-x-2026-10-28.json` with the three records from the "Facts checked" table (fill `UpdateId` from a fresh read-only search, or a placeholder GUID marked as such when the search no longer returns them).
- [ ] **Step 3:** Write the failing tests (fixture records through `FakeUpdateAgent`, Ally device fixture):
  - `Wu_FirmwareOnSystemResourceIsBios` (`{68e0fa36-...}` → Bios, key `fw:68e0fa36-...`, installed "319").
  - `Wu_FirmwareOnDeviceResourceIsFirmware` (`{cbf5a488-...}` → Firmware, installed "0015").
  - `Wu_DriverMatchedToPresentDevice` (Realtek → `dev:` key, installed 11.19.602.2025, latest 11.19.20.602).
  - `Wu_UnmatchedDriverHasOwnKey`.
  - `Wu_SearchFailureIsUnavailable` (fake throws; `Problem` = `UpdateTexts.WindowsUpdateUnavailable` = "Windows Update did not answer. Try Check again later."; no fallback page).
  - `Wu_EmptyIsOkWithNoItems`.
  - `WindowsUpdateLive_SearchReadsOnly` `[Trait("Lane","LiveNet")]`: real `WinUpdateAgent.SearchDriversAsync` returns without throwing; nothing else is called.
- [ ] **Step 4:** Run `dotnet test --filter "Wu_|WindowsUpdateLive"`. Expected: build fails.
- [ ] **Step 5:** Implement.
- [ ] **Step 6:** Run `dotnet test`. Expected: all pass, LiveNet lane skipped.
- [ ] **Step 7:** Commit with the message `Add Windows Update source`.

---

### Task 8: ASUS source

**Files:**
- Create: `src/NitrOS.Updates/Sources/AsusSource.cs`, `Sources/AsusListing.cs` (JSON records), `Sources/SupportPages.cs`, `src/NitrOS.Updates/Net/HttpFactory.cs`, tests `tests/NitrOS.Updates.Tests/Sources/AsusSourceTests.cs`, `Fakes/FakeHttpHandler.cs`, `Fixtures/asus-RC73XA-bios-2026-10-28.json`, `Fixtures/asus-RC73XA-drivers-2026-10-28.json`, `Fixtures/asus-status-fail.json`, `Fixtures/asus-shape-changed.json`, `tests/NitrOS.Updates.Tests/LiveNet/AsusLiveTests.cs`

**Interfaces:**
- Produces:
  - `AsusListing` records for the fields in the "Facts checked" table (`System.Text.Json`, case-sensitive names as ASUS sends them; unknown fields ignored; `DownloadUrl` as `{ Global, China }`).
  - `public static class SupportPages`: `Uri? For(MakerId maker)`. ASUS `https://rog.asus.com/support/` for ROG models (product name contains "ROG") else `https://www.asus.com/support/download-center/`; AMD, NVIDIA, Intel, MSI, Lenovo, Dell, HP from Task 1 Step 2 and Step 1 below. Other → null.
  - `public static class HttpFactory { HttpClient Create(HttpMessageHandler? handler = null); }`: `AllowAutoRedirect` false, 30-second timeout, a `User-Agent` decided in Step 1.
  - `AsusSource(HttpClient http, FirmwareMap map)`: `Id` "asus", `Name` "ASUS", applies when `Maker == Asus`. Requests, with `model` and `cpu` = `ModelCode`, the two URLs from `device-updates.md` (`osid=52`, `systemCode=rog`, `website=global`).
    - Fallback, with `FallbackPage = SupportPages.For(Asus)` and `Status` Unavailable: no `ModelCode` → "NitrOS could not read this PC's ASUS model code."; HTTP error or timeout → "ASUS's update list did not answer (<code or timeout>)."; `Status` not "SUCCESS" or `Result.Obj` missing → "ASUS's update list has changed, so NitrOS cannot read it."; zero usable files for the model → "ASUS's update list has nothing for model <code>." Each is a `UpdateTexts` constant with a `{0}` placeholder where shown.
    - Filtering (ruling 6), then per object `Name`, per distinct `Title`: every file version, newest first, becomes a candidate; the newest is the item, but all versions are kept internally so `Merger` can fall back past a known-bad one (each version is its own `UpdateItem` with the same `Key`).
    - Kind: object "BIOS Update(Windows)" → Bios; "Firmware" → Firmware; everything else → Driver.
    - Key: Bios → `fw:<system guid>` when the firmware map links it, else `src:asus:<title>`; Firmware → the map's `fw:` key or `src:asus:<title>`; Driver → `dev:<instance id>` of the first present device whose hardware or compatible ids equal one `HardwareInfoList[].hardwareid` (case-insensitive), else `src:asus:<title>`.
    - Installed: Bios/Firmware from `FirmwareMap.InstalledText` (null when unmapped, so the MCU tool is CantTell); Driver from the matched device's `DriverVersion`, else null.
    - `DownloadUrl` = `DownloadUrl.Global` (must parse as `https` on `dlcdnets.asus.com`, else the file is dropped); `SizeBytes` = `ListingParse.SizeBytes(FileSize)`; `ReleaseDate` = `ListingParse.AsusDate(ReleaseDate)`; `Sha256` = `sha256`; `SilentArgs` = the text after `%%` in `ExeModule`, or null; `RestartLikely` = `Reboot == 1`.
    - `Method`: Driver → `MakerPackage` only when `AsusDriverInstallsProven` (an `internal const bool`, false until Task 14 Step 3 sets it) is true and `SilentArgs` is not null; otherwise `HandOff`. Bios and Firmware → `HandOff` always in version 1 (ruling 8).

- [ ] **Step 1: Verify first.** With GET only: whether the ASUS endpoints answer without a browser `User-Agent` (use a plain `NitrOS/<version>` agent if they do; otherwise record what is needed); whether `DownloadUrl.Global` redirects (HEAD request on the touch-panel firmware URL, no body downloaded) and to which host; the MSI and Intel support page URLs in a browser-like fetch. Record in the commit body.
- [ ] **Step 2:** Capture the two fixture files with a GET of each URL for `RC73XA` (pretty-printed, unchanged otherwise). Write `asus-status-fail.json` (`"Status": "FAIL"`) and `asus-shape-changed.json` (`Result` renamed) by hand.
- [ ] **Step 3:** Write the failing tests (fixtures through `FakeHttpHandler`, Ally device fixture):
  - `Asus_BiosItemFromListing` (Bios, latest "319", 2026-10-14, about 15.36 MB, `https://dlcdnets.asus.com/...`, sha256 set, `HandOff`, installed "319", key `fw:68e0fa36-...`).
  - `Asus_PdAndTouchFirmware` (PD latest "0020" installed "0015"; touch latest "2002" installed "2002").
  - `Asus_McuToolIsCantTell` (installed null, method `HandOff`).
  - `Asus_DriverMatchedByHardwareId` (AMD graphics `PCI\VEN_1002&DEV_150E&REV_C7` → GPU `dev:` key, installed 32.0.32015.7029).
  - `Asus_KeepsSystemControlInterface` (a Driver item titled "ASUS System Control Interface v3" is present, key on the `ACPI\ASUS2018` or `ACPI\ASUS2025` device).
  - `Asus_DropsArmouryCrateAndStoreLinks` (no item title matches `Armoury Crate*`, `MyASUS*`, `GlideX*`, `Dolby*`; no item whose URL host is not `dlcdnets.asus.com`; no "OS Assistant" and no EZ Flash zip).
  - `Asus_KeepsOlderVersionsForKnownBadFallback` (two PD items with the same key: "0020" and "0019").
  - `Asus_DriverInstallsHandOffUntilProven` (with the constant false, every Driver item is `HandOff`).
  - `Asus_HttpErrorFallsBackToSupportPage`, `Asus_StatusFailFallsBackToSupportPage`, `Asus_ShapeChangeFallsBackToSupportPage`, `Asus_NoModelCodeFallsBackToSupportPage`, `Asus_EmptyModelFallsBackToSupportPage` (each: `Unavailable`, the exact `UpdateTexts` problem, `FallbackPage` = the ASUS page).
  - `Asus_NotForOtherMakers`.
  - `AsusLive_ListingStillParses` `[Trait("Lane","LiveNet")]`: real HTTP GET of both URLs for `RC73XA`; `Status` Ok, at least one Bios item, every URL on `dlcdnets.asus.com`.
- [ ] **Step 4:** Run `dotnet test --filter "Asus"`. Expected: build fails.
- [ ] **Step 5:** Implement.
- [ ] **Step 6:** Run `dotnet test`. Expected: all pass, LiveNet lane skipped.
- [ ] **Step 7:** Commit with the message `Add ASUS update source with support-page fallback`.

---

### Task 9: GPU vendor sources and support-page sources

**Files:**
- Create: `src/NitrOS.Updates/Sources/GpuVendor.cs`, `Sources/SupportPageSource.cs`, `Sources/GpuPageSource.cs`, and one `Sources/Gpu<Vendor>Source.cs` per vendor Task 1 marked Reliable, tests `tests/NitrOS.Updates.Tests/Sources/GpuSourcesTests.cs`, `Sources/SupportPageSourceTests.cs`, fixtures `Fixtures/gpu-<vendor>-<date>.json` for each Reliable vendor, and a LiveNet test per Reliable vendor in `tests/NitrOS.Updates.Tests/LiveNet/GpuLiveTests.cs`

**Interfaces:**
- Produces:
  - `public enum GpuVendor { Amd, Nvidia, Intel }`; `public static class GpuVendors { GpuVendor? FromHardwareId(string id); }` (`VEN_1002` AMD, `VEN_10DE` NVIDIA, `VEN_8086` Intel; verify the three PCI vendor ids against the PCI-SIG list or Microsoft's documentation in Step 1).
  - `GpuPageSource(GpuVendor vendor)`: `Id` "gpu-page-<vendor>", applies when a GPU of that vendor is present. One item per GPU: `Key` `dev:<instance id>`, Kind Driver, installed = the GPU's `DriverVersion`, latest null, `Method` `PageOnly`, `Page` = the vendor's driver page from Task 1, `Note` = "Check <vendor>'s site for a newer driver." This is the source for every vendor Task 1 marked **Page only**.
  - `Gpu<Vendor>Source` for each **Reliable** vendor, exactly as Task 1's doc describes: same item shape with `LatestVersion`, `DownloadUrl` on the allowed host(s) from Task 1, `Sha256` if published, `Method` `HandOff` (ruling 8), and the same `Unavailable` plus `FallbackPage` = the driver page when its listing fails, with problem text "<Vendor>'s driver list did not answer as expected, so here is <Vendor>'s driver page.".
  - `SupportPageSource(MakerId maker)`: `Id` "page-<maker>", applies when `device.Maker == maker` and the maker has no listing source (MSI, Lenovo, Dell, HP; ASUS never, it has `AsusSource`). Returns `Ok` with no items and `FallbackPage = SupportPages.For(maker)`, which the checker shows as a button (ruling 9).
  - `public static class SourceSet { IReadOnlyList<IUpdateSource> Default(IWindowsUpdateAgent agent, HttpClient http, FirmwareMap map); }`: Windows Update, ASUS, each GPU source (Reliable or page), each support-page source. Order matters for ties after Windows Update: ASUS before GPU vendors.

- [ ] **Step 1: Verify first.** The PCI vendor ids, and that Task 1's doc is committed. Implement only what it says; if Task 1 marked all three Page only, create no `Gpu<Vendor>Source` and say so in the commit body.
- [ ] **Step 2:** Write the failing tests:
  - `GpuVendors_FromHardwareId`.
  - `GpuPage_OneItemPerGpuWithPage` (Ally fixture: one AMD item, installed 32.0.32015.7029, `PageOnly`, AMD's page).
  - `GpuPage_NotForAbsentVendor`.
  - `SupportPage_MsiGetsPageButton`, `SupportPage_NeverForAsus`, `SupportPage_OtherMakerHasNone`.
  - `SourceSet_OrderWindowsUpdateFirstThenAsus`.
  - For each Reliable vendor: `Gpu<Vendor>_ItemFromFixture`, `Gpu<Vendor>_FailureFallsBackToDriverPage`, `Gpu<Vendor>_DropsOtherHosts`, and `Gpu<Vendor>Live_ListingStillParses` `[Trait("Lane","LiveNet")]`.
- [ ] **Step 3:** Run `dotnet test --filter "Gpu|SupportPage|SourceSet"`. Expected: build fails.
- [ ] **Step 4:** Implement.
- [ ] **Step 5:** Run `dotnet test`. Expected: all pass, LiveNet lane skipped.
- [ ] **Step 6:** Commit with the message `Add GPU vendor and support-page sources`.

---

### Task 10: The update checker

**Files:**
- Create: `src/NitrOS.Updates/Checking/UpdateChecker.cs`, `Checking/CheckReport.cs`, tests `tests/NitrOS.Updates.Tests/Checking/UpdateCheckerTests.cs`, `Fakes/FakeSource.cs`

**Interfaces:**
- Produces, namespace `NitrOS.Updates.Checking`:
  - `public sealed record SourceNotice(string SourceName, string Problem, Uri? Page)`
  - `public sealed record SupportLink(string Text, Uri Page)`
  - `public sealed record CheckProgress(string SourceName, int Done, int Total)`
  - `public sealed record CheckReport(DeviceInfo Device, IReadOnlyList<MergedItem> Items, IReadOnlyList<SourceNotice> Notices, IReadOnlyList<SupportLink> Links, bool WindowsUpdateDriversExcluded, DateTimeOffset CheckedAt)`
  - `public sealed class UpdateChecker(IDeviceReader reader, IReadOnlyList<IUpdateSource> sources, KnownBadList knownBad, FirmwareMap map, WindowsUpdatePolicy policy, TimeProvider clock)` with `Task<CheckReport> CheckAsync(IProgress<CheckProgress>? progress, CancellationToken ct)`:
    - Reads the device once, runs every applicable source **in parallel** (each with its own 3-minute timeout linked to `ct`), reports progress as each finishes.
    - A source that throws despite its contract becomes a notice "<Name> could not be checked: <message>."
    - `Unavailable` → a `SourceNotice` with its problem and page. `Ok` with a `FallbackPage` and no items → a `SupportLink` "Open <maker>'s support page".
    - When the maker is Other: one `SupportLink`-less notice `UpdateTexts.NoMakerSource` = "NitrOS has no update list for {0}. Look on your maker's website.".
    - Merges all items with `Merger.Merge`.
    - `WindowsUpdateDriversExcluded` = `policy.DriversExcluded()`.
    - It never downloads, installs or writes anything.

- [ ] **Step 1:** Write the failing tests:
  - `Checker_RunsOnlyApplicableSources`.
  - `Checker_SourceFailureBecomesNoticeWithPage` (fake ASUS Unavailable with page → notice with that page, other sources' items still listed).
  - `Checker_ThrowingSourceDoesNotStopOthers`.
  - `Checker_SupportPageSourceBecomesLink` (MSI device).
  - `Checker_OtherMakerSaysNoList`.
  - `Checker_ReportsPolicyFlag` (FakeRegistry with the value 1 → true; zero writes).
  - `Checker_MergesAcrossSources` (Ally fixtures for WU and ASUS through fakes: BIOS UpToDate, PD NewVersion, MCU CantTell, AMD driver UpToDate, Realtek UpToDate).
  - `Checker_CancelStopsAndThrowsOperationCanceled`.
- [ ] **Step 2:** Run `dotnet test --filter Checker_`. Expected: build fails.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit with the message `Add update checker`.

---

### Task 11: Download, verify and install

**Files:**
- Create: `src/NitrOS.Updates/Installing/IUpdateInstaller.cs`, `Installing/UpdateInstaller.cs`, `Installing/InstallTypes.cs`, `Installing/Downloader.cs`, `Installing/IFileVerifier.cs`, `Installing/IPackageRunner.cs`, `Installing/IUnelevatedLauncher.cs`, `Installing/SourcePolicy.cs`, `src/NitrOS.Updates/Windows/WinFileVerifier.cs`, `Windows/WinPackageRunner.cs`, `Windows/WinUnelevatedLauncher.cs`, tests `tests/NitrOS.Updates.Tests/Installing/UpdateInstallerTests.cs`, `Installing/DownloaderTests.cs`, `Fakes/FakeVerifier.cs`, `Fakes/FakeRunner.cs`, `Fakes/FakeLauncher.cs`, `Fakes/FakePower.cs`, `Windows/WinFileVerifierTests.cs`, `Windows/WinUnelevatedLauncherTests.cs`, `tests/NitrOS.Updates.Tests/Vm/InstallVmTests.cs`

**Interfaces:**
- Produces, namespace `NitrOS.Updates.Installing`:
  - `public sealed record SourcePolicy(string SourceId, IReadOnlyList<string> AllowedHosts, IReadOnlyList<string> ExpectedSigners)`; `static IReadOnlyList<SourcePolicy> Shipped()`: `asus` → hosts `dlcdnets.asus.com` (plus any redirect host Task 8 Step 1 found, only if it is an ASUS-owned domain), signers from Task 2 Step 2; each Reliable GPU vendor → hosts and signers from Task 1.
  - `public enum InstallStage { Checking, Downloading, Verifying, Installing, Done }`; `public sealed record InstallProgress(InstallStage Stage, long? Bytes, long? Total, string? Line)`.
  - `public enum InstallStatus { Installed, InstalledRestartNeeded, HandedOff, Refused, Failed, Cancelled }`
  - `public sealed record InstallOutcome(InstallStatus Status, string Message, string? HandOffPath, RestorePointOutcome? RestorePoint)`
  - `public sealed record InstallRequest(MergedItem Item, DeviceInfo Device, bool UserConfirmedFirmwareWarning)`
  - `public interface IUpdateInstaller { Task<InstallOutcome> InstallAsync(InstallRequest request, IProgress<InstallProgress>? progress, CancellationToken ct); }`
  - `public interface IFileVerifier { bool Sha256Matches(SafeFileHandle file, string expectedHex); IReadOnlyList<string>? Signers(SafeFileHandle file, string path); }` (real: `Authenticode.TrustedSignerNames(handle, path, RevocationMode.WholeChainOnline)` and `SHA256.HashData` over a stream on the handle).
  - `public interface IPackageRunner { int Run(string path, SafeFileHandle lockedFile, string? arguments, TimeSpan timeout); void StartInteractive(string path, SafeFileHandle lockedFile); }` (real: `Process.Start` with `UseShellExecute` false and an argument list split by plan 3's `WinUninstaller.SplitArguments`; `Run` waits up to 60 minutes; `StartInteractive` does not wait).
  - `public interface IUnelevatedLauncher { bool OpenPage(Uri page); bool ShowInFolder(string path); }` (real: Step 1).
  - `public sealed class Downloader(HttpClient http)`: `Task<string> DownloadAsync(Uri url, SourcePolicy policy, long? expectedSize, string folder, IProgress<InstallProgress>? progress, CancellationToken ct)`. Rules from Global Constraints (HTTPS, host per hop, at most 5 redirects, size cap). Writes `<folder>\<file name from the URL path>.part` with `FileShare.None`, renames on success, deletes on any failure.
  - `public sealed class UpdateInstaller(IWindowsUpdateAgent agent, Downloader downloader, IFileVerifier verifier, IPackageRunner runner, IPowerSource power, IRestorePoint restorePoint, KnownBadList knownBad, IReadOnlyList<SourcePolicy> policies, string downloadFolder, Action<string> ensureFolder)` with this order (not decided by the signature):
    1. `Item.State` must be `NewVersion` or `CantTell`; `PageOnly`, `Blocked` and `UpToDate` → Refused with a plain reason.
    2. Known-bad check again on the winning version → Refused with its reason.
    3. Bios or Firmware: `UserConfirmedFirmwareWarning` must be true, and `power.Read() is { HasBattery: true, OnBattery: true }` → Refused `UpdateTexts.NeedsCharger` = "Connect the charger first. Firmware and BIOS updates must not lose power."
    4. `restorePoint.Create("NitrOS device update: <name>")`.
    5. `Method` WindowsUpdate: `agent.InstallAsync(WindowsUpdateId)`. Result 2 → Installed (or InstalledRestartNeeded when `RebootRequired`); 3 → Installed with "Windows reported some errors."; 4 → Failed with the HRESULT in hex; 5 → Cancelled. Steps 6 to 9 do not apply.
    6. Other methods: `ensureFolder(downloadFolder)`, then `Downloader.DownloadAsync` with the item's source policy (no policy → Refused "NitrOS has no download rules for <source>.").
    7. Open the file with `Authenticode.OpenLocked`. `Sha256` given and not matching → delete, Refused "The download does not match the maker's checksum.". `Signers` null, or none `NamesMatch` an expected signer → delete, Refused "The file is not signed by <maker>, so NitrOS will not run it.".
    8. `HandOff` → keep the file and the lock until the user acts: return HandedOff with `HandOffPath`; the screen offers "Start the updater" (`IPackageRunner.StartInteractive` with the lock) and "Open the folder" (`IUnelevatedLauncher.ShowInFolder`). For Bios and Firmware the screen repeats the charger check before "Start the updater".
    9. `MakerPackage` → `runner.Run(path, lock, SilentArgs, 60 min)`; exit 0 → Installed (`RestartLikely` → InstalledRestartNeeded); exit codes Task 2 proved as "restart needed" → InstalledRestartNeeded; anything else → Failed "The installer stopped with code <n>."; delete the file after.
    It never restarts the PC.

- [ ] **Step 1: Verify first.** How an elevated process opens a URL or an Explorer window as the signed-in, unelevated user. Look for Microsoft documentation of the shell-dispatch approach (`IShellWindows` → desktop `IShellView` → `IShellDispatch2.ShellExecute`, as described on Raymond Chen's "The Old New Thing" blog on Microsoft Learn/devblogs) and check each interface name on Learn. Implement `WinUnelevatedLauncher` with it; if it cannot be made to work, `OpenPage` and `ShowInFolder` return false and the screen shows the address or path with a Copy button (ruling 9). Never `Process.Start` a URL from the elevated app. Record the sources and date.
- [ ] **Step 2:** Write the failing tests (fakes, temp folders, `FakeHttpHandler` serving small byte arrays):
  - `Install_RefusesOtherHost` (`https://example.com/x.exe` for source asus: Refused, no file in the folder).
  - `Install_RefusesRedirectOffHost` (`dlcdnets.asus.com` → 302 to `evil.example`: Refused, `.part` deleted).
  - `Install_RefusesHttp` (an `http:` hop).
  - `Install_RefusesOversize` (body larger than size × 1.1).
  - `Install_RefusesHashMismatch`, `Install_RefusesWrongSigner`, `Install_RefusesUnsigned` (file deleted in each).
  - `Install_FirmwareNeedsCharger` (battery, not on mains: Refused with `NeedsCharger`, no download).
  - `Install_FirmwareNeedsConfirmedWarning`.
  - `Install_BiosIsHandedOffNotRun` (fake runner records zero `Run` calls; `HandOffPath` set).
  - `Install_DriverRunsWithListingSwitchesWhenProven` (`MakerPackage` item: `Run` called once with `/SUPPRESSMSGBOXES /VERYSILENT /NORESTART`, file deleted after).
  - `Install_NonZeroExitIsFailedWithCode`.
  - `Install_WindowsUpdateMapsResultCodes` (2, 2 with reboot, 3, 4, 5).
  - `Install_KnownBadRefusedAtInstallTime`.
  - `Install_PageOnlyAndUpToDateRefused`.
  - `Install_MakesRestorePointFirst` (and `Unavailable` does not block).
  - `Install_NeverRestarts` (no restart port exists on the installer; assert the constructor signature in a reflection test).
  - `Downloader_ProgressReportsBytes`.
  - Read-only on this PC: `WinFileVerifier_HashOfTempFile`, `WinFileVerifier_SignersOfSystemFileOnline` (`notepad.exe`, revocation online; skipped with a reason when offline).
  - `WinUnelevatedLauncher_ExistsAndNeverElevates` reads only: asserts the type has no `Process.Start` with `UseShellExecute` true on a URL (a source scan test over `src/NitrOS.Updates`), since opening a real page on the owner's PC is left to Task 14.
  - VM lane, `[Trait("Lane","Vm")]`: `InstallVm_WindowsUpdateDriverInstallsWithPolicyOn` (in the VM, set `ExcludeWUDriversInQualityUpdate` = 1, search drivers, install the first non-firmware driver item if any, otherwise skip with a reason; record the result), `InstallVm_AsusDriverPackageSilentSwitches` (download one small ASUS driver package from the Ally fixture listing, verify, run with its listing switches, record the exit code; skip with a reason when the installer refuses the VM's hardware).
- [ ] **Step 3:** Run `dotnet test --filter "Install_|Downloader|WinFileVerifier|WinUnelevatedLauncher"`. Expected: build fails.
- [ ] **Step 4:** Implement.
- [ ] **Step 5:** Run `dotnet test`. Expected: all pass, VM lane skipped.
- [ ] **Step 6:** Commit with the message `Add verified download and install for device updates`.

---

### Task 12: Device updates screen logic

**Files:**
- Create: `src/NitrOS.App.Logic/Ports/IDeviceUpdates.cs`, `src/NitrOS.App.Logic/Running/LiveDeviceUpdates.cs`, `src/NitrOS.App.Logic/Screens/DeviceUpdatesViewModel.cs`, `Screens/UpdateRow.cs`, tests `tests/NitrOS.App.Tests/DeviceUpdatesViewModelTests.cs`, `tests/NitrOS.App.Tests/Fakes/FakeDeviceUpdates.cs`
- Modify: `src/NitrOS.App.Logic/NitrOS.App.Logic.csproj` (reference `NitrOS.Updates`), `src/NitrOS.App.Logic/Texts.cs`, `src/NitrOS.App.Logic/Running/RunGate.cs`, `tests/NitrOS.App.Tests/NitrOS.App.Tests.csproj` (reference `NitrOS.Updates`), `tests/NitrOS.App.Tests/RunGateTests.cs`

**Plan 5 seam note:** plan 5's `ScreenKeys.DeviceUpdates`, `IScreen`, `Navigator.Register`, `AppComposition.RegisterScreens`, `PageMap.Add`, `IDialogs`, `RunGate` and `ITuneLockProbe` are used exactly as named. Two small additions, no renames: (a) `RunGate.TryEnter(string closeMessage)` overload and `string? BusyMessage`, because plan 5's window-closing text names the Working screen, which an install does not use; `TryEnter()` keeps plan 5's text. (b) Plan 5's tests `Composition_DeviceUpdatesNotRegistered` and `Composition_RegistersEveryScreenOfThisPlan` (every key except `DeviceUpdates`) are changed in Task 13, because registering the screen is the point of this plan.

**Interfaces:**
- Produces, namespace `NitrOS.App.Logic.Ports`: `public interface IDeviceUpdates { Task<CheckReport> CheckAsync(IProgress<CheckProgress>? progress, CancellationToken ct); Task<InstallOutcome> InstallAsync(InstallRequest request, IProgress<InstallProgress>? progress, CancellationToken ct); void StartHandedOff(string path); bool OpenPage(Uri page); bool ShowInFolder(string path); IPowerSource Power { get; } }`.
- Produces, namespace `NitrOS.App.Logic.Running`: `LiveDeviceUpdates(UpdateChecker checker, IUpdateInstaller installer, IPackageRunner runner, IUnelevatedLauncher launcher, IPowerSource power)`. It keeps the locked handle of each handed-off file (from `InstallOutcome`) until `StartHandedOff` or the next `CheckAsync`, then disposes it and deletes the file.
- Produces, namespace `NitrOS.App.Logic.Screens`:
  - `public sealed class UpdateRow : ObservableObject` with `string Name`, `string KindText` ("BIOS", "Firmware", "Driver"), `string InstalledText` ("Installed: 319" or "Installed: can't read it on this PC"), `string LatestText` ("Latest: 0020 from ASUS, 8 Oct 2026, 4.2 MB"; "Check AMD's site"), `string StateText` ("Up to date", "New version", "Can't tell", "Blocked", "On the maker's site"), `string? BlockedReason`, `string? AlsoOfferedText` ("Also offered by Windows Update: 10.1.2.20"), `bool IsFirmware`, `IAsyncRelayCommand? Install` (NewVersion; label "Install" for `WindowsUpdate` and `MakerPackage`, "Download and check" for `HandOff`), `IAsyncRelayCommand? GetAnyway` (CantTell with a download URL or Windows Update id: same as Install, after a confirm "NitrOS can't read the installed version. Get this one anyway?"), `IRelayCommand? OpenPage` (PageOnly), `string? Progress`, `string? Result`, `IRelayCommand? StartUpdater` and `IRelayCommand? OpenFolder` (after a hand-off), `string? Unavailable` (why Install is disabled: session refusal, charger, another change running).
  - `DeviceUpdatesViewModel : ObservableObject, IScreen` with `Key = ScreenKeys.DeviceUpdates`. Constructor `(IDeviceUpdates updates, IDialogs dialogs, RunGate gate, ITuneLockProbe probe, AppContextInfo info, IRestarter restarter)`.
    - `OnShown` starts one check (spec 5.4: "when NitrOS opens Device updates"); `IAsyncRelayCommand Check` starts another; nothing else does. While checking: `IsChecking`, `string? CheckProgress` ("Checking Windows Update... 1 of 3").
    - `IReadOnlyList<UpdateRow> Rows`, `IReadOnlyList<string> Notices` ("<source>: <problem>"), `IReadOnlyList<(string Text, IRelayCommand Open)> Links` (from `SourceNotice.Page` and `SupportLink`), `string? PolicyNote` = `Texts.WuDriversExcludedNote` = "Windows Update on this PC is set not to install drivers by itself. NitrOS still asks Windows Update when you press Check, and does not change that setting." when `WindowsUpdateDriversExcluded`, `string? LastChecked` ("Checked at 14:05.").
    - Install flow: `CanExecute` = `info.Session.Ok && gate.CanChange(probe) && !IsChecking`. On press: one `ConfirmAsync`. Drivers: title "Install <name>?", message names the version and source and "A restore point is made first." Bios and Firmware: title "Before you update the <BIOS|firmware>", `Items` = ["Keep the charger connected.", "Do not turn off or restart the PC until it says it is done.", "It can take several minutes."], confirm "I understand". Then `gate.TryEnter(Texts.InstallingCloseMessage)` (= "NitrOS is installing an update. Wait until it finishes."), `InstallAsync` with `UserConfirmedFirmwareWarning` true for those kinds, release the gate, show the outcome on the row. `InstalledRestartNeeded` → a Restart offer with plan 5's confirm text, through `IRestarter`, gated.
    - Hand-off: "Start the updater" asks the same firmware confirm again and re-checks `Power` (charger) before `StartHandedOff`.
    - Page and folder buttons call `OpenPage` / `ShowInFolder`; a false return shows the address or path in `Result` with `IRelayCommand CopyText` (copy through `IDialogs.ShowAsync` text in plan 5's dialogs; no clipboard API in Logic).
- `Texts.cs` additions: `WuDriversExcludedNote`, `InstallingCloseMessage`, `DeviceUpdatesNoItems` ("NitrOS found no BIOS, firmware or driver updates for this PC."), the confirm texts above.

- [ ] **Step 1:** Write the failing tests (`FakeDeviceUpdates` returns a canned `CheckReport` built from the Ally fixtures and records every call):
  - `DeviceUpdates_NothingInstallsUntilInstall`: construct, `OnShown`, read every property: zero `InstallAsync`, `StartHandedOff`, `OpenPage`, `ShowInFolder` calls and zero dialogs.
  - `DeviceUpdates_ChecksOnlyOnOpenAndCheck`: exactly one `CheckAsync` after `OnShown`, two after `Check`, none from anything else (toggling rows, reading properties).
  - `DeviceUpdates_RowsShowStatesAndSources` (BIOS "Up to date", PD "New version" with "Also offered by" text, MCU "Can't tell", AMD driver "Up to date").
  - `DeviceUpdates_FirmwareInstallAsksWithWarning` (exactly one confirm with the three `Items`; "no" → no install).
  - `DeviceUpdates_DriverInstallAsksOnce`.
  - `DeviceUpdates_InstallDisabledWhenSessionRefusedOrBusy` (session not Ok; gate held; lock probe true).
  - `DeviceUpdates_GateHeldDuringInstallWithItsMessage` (`gate.BusyMessage == Texts.InstallingCloseMessage` while the fake install waits).
  - `DeviceUpdates_PolicyNoteShownWhenExcluded`.
  - `DeviceUpdates_NoticesAndLinksFromReport` (ASUS unavailable notice with its page button).
  - `DeviceUpdates_HandOffOffersStartAndFolder` and `DeviceUpdates_StartUpdaterRechecksCharger` (power fake flips to battery: refused, no start).
  - `DeviceUpdates_PageOpenFailureShowsAddress`.
  - `DeviceUpdates_RestartOfferedOnlyWhenNeeded`.
  - `RunGate_TryEnterWithMessageSetsBusyMessage`.
- [ ] **Step 2:** Run `dotnet test --filter "DeviceUpdates_|RunGate_"`. Expected: build fails.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit with the message `Add Device updates screen logic`.

---

### Task 13: Device updates page, composition and smoke

**Files:**
- Create: `src/NitrOS.App/Pages/DeviceUpdatesPage.xaml`, `src/NitrOS.App/Pages/DeviceUpdatesPage.xaml.cs`
- Modify: `src/NitrOS.App.Logic/AppComposition.cs`, `src/NitrOS.App/PageMap.cs` (or wherever plan 5 fills it), `src/NitrOS.App/ShellWindow.xaml.cs` (closing text from `RunGate.BusyMessage`), `src/NitrOS.App/NitrOS.App.csproj` (reference `NitrOS.Updates`), `src/NitrOS.App.Logic/Startup/SmokeRunner.cs`, `tests/NitrOS.App.Tests/AppCompositionTests.cs`, `tests/NitrOS.App.Tests/SmokeTests.cs`

**Interfaces:**
- Consumes: plan 5's `AppComposition.CreateReal(string exePath, IDialogs dialogs)`, `CreateSmoke`, `RegisterScreens`, `PageMap`, `Navigator`, `ScreenKeys.DeviceUpdates`; this plan's `SourceSet.Default`, `UpdateChecker`, `UpdateInstaller`, `LiveDeviceUpdates`.
- Produces:
  - `CreateReal` builds `LiveDeviceUpdates` with: `DeviceReader(new WinIdentity(), new WinInstalledDevices(), new WinFirmwareTable())`, `SourceSet.Default(new WinUpdateAgent(), HttpFactory.Create(), FirmwareMap.Shipped())`, `KnownBadList.Shipped()`, `WindowsUpdatePolicy(new WinRegistry())`, `UpdateInstaller` with `WinFileVerifier`, `WinPackageRunner`, `WinPowerSource`, `WinRestorePoint`, `SourcePolicy.Shipped()`, download folder `%ProgramData%\NitrOS\downloads` (under the same internal root override plan 5's composition tests use) and `ensureFolder = p => BackupFolder.Ensure(p)`. Building it reads nothing from the network and creates no folder.
  - `RegisterScreens` also registers `ScreenKeys.DeviceUpdates` → `DeviceUpdatesViewModel`. `PageMap.Add(ScreenKeys.DeviceUpdates, typeof(DeviceUpdatesPage))`. Start's entry enables itself (plan 5 Task 4).
  - `CreateSmoke` registers the screen with a `IDeviceUpdates` whose check returns the recorded Ally report from embedded test data (no network) and whose install, start, page and folder calls throw. `SmokeRunner` also visits Device updates once and saves its PNG.
  - `DeviceUpdatesPage`: one column (plan 5's `Sizes.xaml`, 48-pixel rows, `XYFocusKeyboardNavigation="Enabled"`), first focus on Check. Top: Check button, progress text, last checked, the policy note, notices with their page buttons. List: one card per row with name, kind, installed, latest, state badge, also-offered line, blocked reason, and the row's buttons; the result and progress lines under the buttons. Bottom: support links. Firmware rows show the "do not turn off" line on the card itself, not only in the confirm. No logic in code-behind beyond `DataContext` and first focus.

- [ ] **Step 1:** Change and write the tests:
  - Replace plan 5's `Composition_DeviceUpdatesNotRegistered` with `Composition_DeviceUpdatesRegistered`; change `Composition_RegistersEveryScreenOfThisPlan` to include every `ScreenKeys` value.
  - `Composition_CreateRealCreatesNoDownloadsFolder` (temp root: no `downloads` folder after `CreateReal`).
  - `Composition_DeviceUpdatesHasNoBackgroundWork` (after `CreateReal`, no timer, task registration or check has run: the fake-backed overload records zero `CheckAsync` calls until the screen is shown).
  - `Smoke_DeviceUpdatesCannotInstall` (in `CreateSmoke`, every install, start, page and folder call throws; the check returns the recorded report).
- [ ] **Step 2:** Run `dotnet test --filter "Composition|Smoke_"`. Expected: fail.
- [ ] **Step 3:** Implement the composition changes, the page and the smoke visit.
- [ ] **Step 4:** Run `dotnet build` (timeout 10 minutes), then `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit with the message `Add Device updates page and register the screen`.

---

### Task 14: VM lane run, proof of driver switches, and the Ally OWNER CHECKPOINT

**Files:**
- Create: `tools/smoke/run-device-updates-check.ps1`, `docs/research/device-updates-ally-checklist.md`
- Modify: `src/NitrOS.Updates/Sources/AsusSource.cs` (only the `AsusDriverInstallsProven` constant, and only per Step 3), `docs/research/device-updates.md`, `docs/research/notes-for-later-plans.md`, `docs/superpowers/specs/2026-10-24-nitros-design.md` (section 11: mark the WUA-with-policy question answered)

- [ ] **Step 1: VM lane.** In plan 4's VM (`NITROS_VM=1`, and `NITROS_LIVE_NET=1` for the network tests), run `dotnet test --filter "Lane=Vm|Lane=LiveNet"` for `NitrOS.Updates.Tests` and `NitrOS.App.Tests` (timeout 30 minutes). Record every result, including skips and their reasons, in `docs/research/device-updates.md`. Firmware items are never installed (Global Constraints).
- [ ] **Step 2:** Also run the LiveNet lane on the owner's PC (read-only: ASUS GET and a WUA search; no admin needed). Record that the ASUS listing still parses and what WUA returns with the policy on.
- [ ] **Step 3: Driver switches.** If `InstallVm_AsusDriverPackageSilentSwitches` ran (not skipped) and returned exit 0 with the driver installed in the VM, and Task 2 found no contrary evidence, set `AsusDriverInstallsProven = true`, update `Asus_DriverInstallsHandOffUntilProven` into `Asus_DriverInstallsUseListingSwitches`, and note the evidence in the commit body. Otherwise leave it false and write why. BIOS, firmware and MCU stay hand-off regardless.
- [ ] **Step 4:** Write `docs/research/device-updates-ally-checklist.md` and `tools/smoke/run-device-updates-check.ps1` (the smoke mode cannot reach the network, so this script starts the normal app, which needs the owner's UAC approval; the script itself writes only its log). The checklist, each line a checkbox the owner ticks:
  1. Plug in the charger. Open NitrOS, then Device updates from Start.
  2. The list appears within about a minute without pressing anything else. Nothing installs.
  3. The note about Windows Update's driver setting is shown.
  4. BIOS shows "Up to date" (319), not "New version", although Windows Update offers 10.1.2.319.
  5. PD firmware shows "New version" 0020 (if still newer than installed), touch panel "Up to date" 2002, the controller (MCU) tool "Can't tell" unless Task 2 found a way to read it.
  6. The AMD graphics driver shows "Up to date" (32.0.32015.7029 is newer than ASUS's), and the AMD row says where to check AMD's site if AMD is Page only.
  7. No Armoury Crate, MyASUS or GlideX row. "ASUS System Control Interface v3" is listed or up to date, never offered for removal.
  8. Press Check: the list refreshes. Leave the screen and come back: it checks again. Close NitrOS: Task Manager shows no NitrOS process left.
  9. "Open ASUS's support page" (if shown) opens in the normal browser, not as administrator.
  10. With the charger unplugged, BIOS and firmware Install buttons say "Connect the charger first."
  11. **Optional, only if the owner chooses:** press "Download and check" on the PD firmware row with the charger in. NitrOS downloads, checks and offers "Start the updater". Starting ASUS's updater is the owner's own decision; NitrOS does not start it without the button, and this checklist does not require it.
- [ ] **Step 5: OWNER CHECKPOINT.** Ask the owner whether to go through the checklist on the Ally now, and wait for a yes. The controller runs `tools/smoke/run-device-updates-check.ps1` through its UAC-elevated `.ps1` runner (output in `%USERPROFILE%\nitros-device-updates-check.txt`) and the owner ticks the list. Look at a close-up screenshot of the Device updates page rows (states, the policy note, the firmware warning line). No restart without the owner's explicit OK. No BIOS or MCU flash unless the owner starts it himself in step 11.
- [ ] **Step 6:** Write the outcomes into `device-updates.md`, the notes file (anything left for later: Lenovo, Dell, HP listings; silent BIOS and MCU installs; any GPU vendor still Page only) and spec 11. Commit with the message `Record device updates VM run and Ally checklist`.

---

## Spec and notes coverage

| Item | Task |
|---|---|
| Spec 4 screen 7: list, "Up to date" / "New version", Install per item, Check, progress, reachable from Start | 12, 13 (Start entry from plan 5 Task 4) |
| Spec 5: `src\NitrOS.Updates` with sources, version compare, verified install | 3 to 11 |
| Spec 5.4 one source interface (name, kind, installed, latest, URL, size, date) | 4, 7 |
| Spec 5.4 layer 1, Windows Update Agent API | 7, 11 |
| Spec 5.4 layer 2, ASUS listing checked live; MSI and others get a support page | 8, 9 |
| Spec 5.4 layer 3, GPU vendors, verify first, page fallback | 1, 9 |
| Spec 5.4 newest wins and source shown | 6, 12 |
| Spec 5.4 downloads only from the maker's servers, signature belongs to the maker | 3, 11 |
| Spec 5.4 Install only on press; BIOS and firmware need the charger and a warning | 11, 12 |
| Spec 5.4 known-bad list | 6, 11 |
| Spec 5.4 checks only on open or Check; no background updater | 10, 12, 13 |
| Spec 5.4 Windows Update allowed for drivers and firmware with the add-ons switch on | catalog unchanged (no switch writes `ExcludeWUDriversInQualityUpdate`); 7 |
| Spec 5.4 ASUS System Control Interface always kept | 8 (never filtered), 11 (no remove path) |
| Spec 5.4 listing stops working → support page and why | 8, 9, 10 |
| Spec 10 only Device updates installs drivers; Lenovo, Dell, HP listings not in v1 | 9 (links only), ruling 9 |
| Spec 11 `ExcludeWUDriversInQualityUpdate` = 1 on the owner's PC | Facts table, 5, 10, 12, 14 |
| Spec 11 MCU version without Armoury Crate; BIOS and MCU updater switches and exit codes | 2, 14 |
| Spec 11 AMD, NVIDIA, Intel latest versions | 1 |
| Notes (plan 4, Task 11): check Device updates covers drivers and firmware with the policy on | Facts table, 14 |
| `reinstall-block.md`: Device updates must cover drivers or the owner clears the value | 7, 10 (screen note; never changed) |
