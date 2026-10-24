# How maker apps come back, and what blocks it

Research 2026-10-25 (plan 3, task 11). Read-only checks on the owner's Ally X
(RC73XA, Windows build 26300) plus Microsoft Learn and ASUS pages. Nothing on
the PC was changed. "Unverified" marks anything not backed by a primary
source or a live check. Raw output from the PC is in `docs/research/evidence/`
and each section names its file. "Checked live, output not saved" marks the
few items without one.

## Short answer

There are four ways a maker app comes back. Only one of them has a
documented, reversible switch that leaves drivers and firmware alone, and it
is not proven to cover the main route on this PC:

| Route | Seen on this PC | Documented control | Keeps drivers and firmware updating |
|---|---|---|---|
| Driver package names a Store app (`AddSoftware`, SoftwareType 2) | Yes: Dolby, Realtek, AMD (not ASUS) | None that Microsoft names for this route | n/a |
| Device metadata apps ("Device installation settings") | Not seen | `PreventDeviceMetadataFromNetwork` policy | Yes (it names only apps) |
| Firmware hands Windows a program (WPBT) | No WPBT table in this firmware | None from Microsoft for users | n/a |
| ASUS ACPI device `ASUS9001` and its "Armoury Crate Control Interface" driver | Yes | Device install restriction by hardware ID (blocks only that driver) | Yes for every other device |
| ASUS motherboard BIOS "Download & Install ARMOURY CRATE app" | Not documented for the Ally | BIOS toggle (motherboards only) | Yes |

Proposal: one new switch using `PreventDeviceMetadataFromNetwork` (below),
plus VM and Ally tests before anything that touches the `ASUS9001` driver.
Neither goes in the catalog in this task.

## What is on this PC (checked 2026-10-25)

### Driver packages that name Store apps

Source: `evidence/driver-store.txt`.

`pnputil /enum-drivers` lists 224 packages: 140 Extension, 20
SoftwareComponent, 4 Firmware. Searching `C:\Windows\INF\oem*.inf` for
`AddSoftware` finds these Store links (SoftwareType 2, `pfn://`):

| INF | Provider | Store app (PFN) |
|---|---|---|
| oem47.inf `dax3_swc_hsa.inf` | Dolby | `DolbyLaboratories.DolbyAccess_rz1tebttyb220`, `DolbyAccessOEM`, `DolbySettings`, `DolbyAtmos`, and six more `DolbyLaboratories.*_rz1tebttyb220` |
| oem46.inf `realtekhsa.inf` | Realtek | `RealtekSemiconductorCorp.RealtekAudioControl_dt26b99r8h8gj` |
| oem41.inf, oem59.inf `uwppair.inf` | AMD | `AdvancedMicroDevicesInc-2.AMDRadeonSoftware_0a9344xs7nr4m` |

AMD's OpenCL, OpenGL and Vulkan packages also use `AddSoftware`, but those
install driver parts, not apps.

So the catalog's `dolby-access-remove` and `realtek-audio-console-remove`
remove apps that their own audio drivers can ask for again. Microsoft says the
system "queues software to be installed after device installation" and "tries
again when the referencing software component is updated", so a new Dolby or
Realtek driver can bring the app back. Whether an unchanged driver re-adds a
removed app is not stated (unverified).

No ASUS package uses `AddSoftware`. Armoury Crate does not come back this way
on this PC.

### ASUS packages

Source: `evidence/driver-store.txt` (packages, INF lines, present devices)
and `evidence/asus-armoury-crate-logs.txt` (the `System32\ASUSACCI` file
list).

| INF | Class | Version | What it adds |
|---|---|---|---|
| oem52.inf `asussci2.inf` (ASUS System Control Interface v3) | System | 3.1.72.0 | Services `ASUSOptimization`, `ASUSSystemDiagnosis`, `ASUSSystemAnalysis`, `ASUSSoftwareManager`, `AsusAppService`, `ASUSSwitch`, all run from the DriverStore |
| oem36.inf `armourycratecontrolinterface.inf` | System | 1.2.0.2 | Service `ArmouryCrateControlInterface` for device `ACPI\ASUS9001` ("Armoury Crate Control Interface"); files in `System32\ASUSACCI` include `ArmouryCrateKeyControl.exe` |
| oem211/212 `acsevirtualbus`, `acsehidremap` | System | 1.0.0.19 | Armoury Crate SE controller remap |
| oem37, oem32, oem33, oem40 | Firmware | | MCU, touch panel, power delivery, BIOS 317 capsules |

The BIOS firmware exposes the `ACPI\ASUS9001` device, and Windows matches
the Armoury Crate Control Interface driver to it. That is how an ASUS hook
survives a Windows reinstall without WPBT. What the service does when
Armoury Crate is missing (prompt, download, nothing) is not documented by
ASUS (unverified). "ASUS Software Manager" lives inside the System Control
Interface package, which must stay for power and fan control; its job is not
documented either (unverified).

### WPBT

Source: `evidence/firmware-tables-and-policies.txt`.

`EnumSystemFirmwareTables('ACPI')` on this Ally X returns: ABLT APIC BGRT
ECDT FACP FIDT FPDT HPET IVRS MCFG MSDM PCCT SSDT TPM2 UEFI VFCT WSMT. No
WPBT, and `C:\Windows\System32\wpbbin.exe` does not exist. On this device
WPBT is not the route. `DisableWpbtExecution` (Session Manager) appears only
in third-party tools (winutil, a PowerShell Gallery script), not in Microsoft
documentation, so NitrOS should not rely on it.

### Policies already set

Source: `evidence/firmware-tables-and-policies.txt`.

- `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\ExcludeWUDriversInQualityUpdate` = 1.
  Windows Update currently offers **no drivers** on the owner's PC. Winhance
  set it: its change history logs "Driver Updates via Windows Update: Enabled
  → Disabled" at 2026-10-19 17:08 (same file). Whether it also stops
  firmware from Windows Update is unverified; Microsoft's text names only
  "drivers". The earlier note that BIOS 314 and power-delivery firmware came
  through Windows Update (`device-updates.md`) has no saved source and is
  unverified.
- `Device Metadata\PreventDeviceMetadataFromNetwork` = 0 (not blocked), no
  policy value.
- `DriverSearching\SearchOrderConfig` = 1 (default).
- No `DeviceInstall\Restrictions` policies.

### The October 2026 Armoury Crate return

ASUS logs under `C:\ProgramData\ASUS` show (source:
`evidence/asus-armoury-crate-logs.txt`; the dism.log entry for the ASUS
installer's provisioning at 03:37 is in `evidence/dism-log-excerpts.txt`):

- 2026-10-20 03:22 to 03:23: Armoury Crate uninstaller and MSI uninstall logs.
- 03:28:50: "Armoury Crate Full Installation Package" 1.4.4.3 starts
  (`ALL\Armoury Crate Full Installation Package_01_*.log`), VC++ redist logs
  land in the signed-in user's Temp folder.
- 03:29:03 to 03:29:14: its UI starts with `-supersetstart -win armourycrate
  -guideuser`, reads `ModelName = RC73XA`, and logs the "Next" and
  "DownloadAndInstall" button steps.
- 03:38 to 03:44: Armoury Crate SE, ROG Live Service, Aura and the AsIO3
  driver are back. Armoury Crate's services were running on 2026-10-25
  (checked live, output not saved).

The logs show button clicks in the user's session, which looks like a person
ran the full installer, not Windows Update or a silent agent. What started the
installer is not recorded (unverified). This is not evidence of an automatic
reinstall.

## What Microsoft and ASUS document

1. **Software components and `AddSoftware`.** A driver package may name an
   app; SoftwareType 2 is a Microsoft Store link (`pfn://`), allowed in any
   INF. "There is no guarantee when or if the software will be installed"; a
   failed install is retried when the component is updated; the app is not
   removed when the device is. No user or admin switch is described.
   Sources: INF AddSoftware Directive, ms.date 2022-07-08, updated
   2025-03-25, https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-addsoftware-directive ;
   Using a Component INF File, ms.date 2023-05-08, updated 2025-03-25,
   https://learn.microsoft.com/en-us/windows-hardware/drivers/install/using-a-component-inf-file
2. **Device metadata apps.** Policy "Prevent automatic download of
   applications associated with device metadata"
   (`SOFTWARE\Policies\Microsoft\Windows\Device Metadata`,
   `PreventDeviceMetadataFromNetwork`, DeviceSetup.admx, Pro and later,
   Windows 10 1809+). When enabled, "Windows doesn't download applications
   associated with device metadata for installed devices" and it overrides the
   Device Installation Settings dialog. It does not mention drivers. Whether
   it also stops `AddSoftware` Store links is not stated (unverified). A
   Level1Techs forum thread, "Does the 'Device Installation Settings'
   actually do anything?",
   https://forum.level1techs.com/t/does-the-device-installation-settings-actually-do-anything-blocking-auto-installed-bloatware/258882 ,
   reportedly says the dialog did not stop maker stub installers. I saw only a
   search-result summary, not the thread, so that is unverified and
   anecdotal. Source: DeviceInstallation Policy CSP, ms.date 2026-10-14,
   updated 2026-10-23,
   https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-deviceinstallation
3. **Device installation restrictions.** "Prevent installation of devices
   that match any of these device IDs"
   (`Software\Policies\Microsoft\Windows\DeviceInstall\Restrictions`,
   value `DenyDeviceIDs`, DeviceInstallation.admx) blocks installing a device
   whose hardware or compatible ID is listed; other devices still install and
   update. "Prevent installation of devices using drivers that match these
   device setup classes" (`DenyDeviceClasses`) does the same by class. Both
   are logged in `C:\Windows\INF\setupapi.dev.log`. The exact registry layout
   of the ID list (a `DenyDeviceIDs` subkey with numbered values) and the
   name of the "also apply to installed devices" registry value are not on
   that page (unverified). Same source as 2.
4. **Windows Update drivers.** `ExcludeWUDriversInQualityUpdate`
   ("Do not include drivers with Windows Updates", WindowsUpdate.admx): all
   or nothing; "Enable this policy to not include drivers with Windows quality
   updates". Source: Update Policy CSP, ms.date 2026-10-14, updated
   2026-10-23,
   https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-update
   It cannot block one maker's apps while keeping drivers, so it is not a
   reinstall block.
5. **ASUS BIOS setting.** "Download & Install ARMOURY CRATE app" under Tool >
   ASUS Armoury Crate, default Enabled, for ASUS motherboards; laptops and
   handhelds are not mentioned; WPBT is not mentioned. Source: ASUS FAQ
   1043788, last update 2025-06-02, https://rog.asus.com/support/faq/1043788
   Whether the Ally X BIOS has a similar toggle: unverified (the owner can
   look in the BIOS; NitrOS cannot change BIOS settings).
6. **Store updates.** ASUS says turning off Microsoft Store "App updates"
   stops Armoury Crate's Store versions updating, and warns it applies to all
   Store apps. It is an update switch, not a reinstall block. Source: ASUS
   FAQ 1041654, last update 2026-10-21, https://asus.com/support/FAQ/1041654

## Proposed catalog entry (not added)

Documented, reversible, one existing action type (`registry.set`), and it
names only apps, so drivers and firmware are untouched:

```json
{
  "id": "device-companion-apps-off", "name": "Stop automatic device apps",
  "feature": "Device companion apps",
  "description": "Stops Windows downloading the apps that come with device information; drivers still install and update.",
  "risk": "safe", "breaks": ["Apps for newly plugged-in devices must be installed by hand."],
  "modes": ["live", "image"], "reversible": true,
  "cards": { "recommended": "on", "gamer": "on", "streamer": "on", "maxLean": "on" },
  "live": [ { "type": "registry.set", "hive": "HKLM", "key": "SOFTWARE\\Policies\\Microsoft\\Windows\\Device Metadata", "name": "PreventDeviceMetadataFromNetwork", "kind": "dword", "data": "1" } ],
  "image": [ { "type": "registry.set", "hive": "HKLM", "key": "SOFTWARE\\Policies\\Microsoft\\Windows\\Device Metadata", "name": "PreventDeviceMetadataFromNetwork", "kind": "dword", "data": "1" } ]
}
```

It does not, by itself, stop Armoury Crate, and it may not stop Dolby Access
or Realtek Audio Control (point 2). Its description must not promise more.

Not proposed yet:

- **Block the `ACPI\ASUS9001` driver** with "Prevent installation of devices
  that match any of these device IDs". Documented and reversible, but it is
  two registry writes with an unverified layout, and the Armoury Crate
  Control Interface also ships `ArmouryCrateKeyControl.exe`, so blocking it
  may break the Armoury Crate and Command Center buttons that Decky and
  Overdrive use. Test on the Ally first.
- **Disable `ASUSSoftwareManager`** (`service.start`). Fits an action type,
  but what it does is undocumented and it sits inside the System Control
  Interface package that must stay.

## Tests to add

- Plan 4 (VM): install a Dolby or Realtek driver with `AddSoftware`, remove
  the app, then check whether it returns with and without
  `PreventDeviceMetadataFromNetwork` = 1, by restarting and by installing a
  newer driver (`setupapi.dev.log` and AppX deployment events).
- Ally (owner present): remove Armoury Crate with the switch on, leave the PC
  for a day with Windows Update and Store on, log what comes back, and watch
  `ArmouryCrateControlInterface` and `ASUSSoftwareManager` with the ASUS logs
  in `C:\ProgramData\ASUS\ALL`. Look in the Ally X BIOS for an Armoury Crate
  download option.
- Tell the owner `ExcludeWUDriversInQualityUpdate` is on, so Windows Update
  gives no drivers or firmware now; Device updates must cover them or the
  value must be cleared.
