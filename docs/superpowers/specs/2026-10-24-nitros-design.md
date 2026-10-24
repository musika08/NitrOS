# NitrOS design

Date: 2026-10-24
Status: draft, waiting for the owner's review

## 1. What NitrOS is

A Windows app that makes Windows 11 as lean as possible for gaming: fewer
processes, less memory in use, and Windows still working. It is "a fresh
Windows install with nothing in it, tailored to a gamer".

It works two ways, with the same switches:

- **Build a slim ISO:** take a Windows 11 ISO, remove things before Windows
  ever runs, and write a new ISO to install from.
- **Tune this PC:** apply the switches to the Windows already running, with
  undo.

The main target device is a handheld gaming PC (the owner's ASUS ROG Xbox
Ally X, Ryzen Z2 Extreme, 24 GB of which about 8 GB is currently reserved for
the GPU). It also has to work on desktops and other
handhelds.

### What the owner said

- Every removal is optional, an on/off switch, with the memory it saves
  shown beside it.
- Both modes (slim ISO and tune this PC), in one native Windows app.
- Ready-made cards (Recommended, Gamer, and so on) that pick switches
  automatically, each showing its memory saving so the user can compare.
- Bluetooth and printing matter and stay on by default (printing goes only
  in Max Lean).
- Max Lean assumes the user does not use Xbox. A user who wants Xbox installs
  it separately.
- Armoury Crate is removed on every card. No manufacturer add-ons at all.
- Cut memory wherever possible, not only in gaming-related parts.

### Assumptions (owner can correct)

- Version 1 uses the user's own Windows 11 ISO. A download button comes later.
- Windows 11 24H2 and newer only. Older builds are refused with a message.
- The project is private. Public release needs a name check first
  (Acer sells "Nitro" laptops and the "NitroSense" app).

### Success means

1. Each card measurably lowers memory in use and process count against plain
   Windows, and the numbers shown in the app come from real measurements.
2. After any card, the health check passes: Start menu, internet, sound,
   Bluetooth, and everything the card promised to keep.
3. Every Tune-mode switch marked reversible returns the PC to its exact
   previous values when undone.
4. NitrOS itself leaves no background process running.

## 2. Switch groups

1. **Bundled apps:** Copilot, Clipchamp, News, Weather, Solitaire, Teams,
   new Outlook, Phone Link, Feedback Hub, Get Help, Maps, To Do, Widgets, and
   the rest of the consumer apps. One switch each.
2. **Manufacturer add-ons:** Armoury Crate and other makers' control apps,
   plus a block that stops Windows Update reinstalling them. Hardware drivers
   are kept.
3. **Big Windows parts:** Edge (the WebView2 runtime is always kept, many
   apps need it), OneDrive, Defender, Windows Update, Microsoft Store, Xbox
   app, Game Bar, Gaming Services, Xbox full-screen home, search indexing,
   recovery tools, printing, Bluetooth, Memory Integrity.
4. **Background services:** services that sit idle for a gamer, such as
   usage reporting, fax, remote registry, maps updates and phone sync.
5. **Privacy and ads:** tracking, Start menu ads, tips, suggested apps.
6. **Gaming settings:** Game Mode, background game recording, GPU scheduling,
   power plan, mouse acceleration off (a feel option, off on every card).
   No network-delay tweaks: every one found is on the "not shipped" list.
7. **Start-up:** optionally launch Steam Big Picture or Playnite full screen
   at boot (off unless chosen).

A tweak only ships if measurement shows a real gain or it clearly removes
load. Popular "FPS boost" tweaks that do nothing are left out.

Added after research (section 12):

- **Fewer service processes:** raise the service-host grouping threshold
  (`SvcHostSplitThresholdInKB`). Microsoft's figures put it at roughly 70
  down to roughly 20 svchost processes, with only a modest RAM gain and weaker
  service isolation; one source reports it can upset Xbox services. Caution
  badge; measured before card defaults are set.
- **Vendor GPU panel in the background:** keep the AMD or NVIDIA driver, stop
  the settings panel running (about 0.5 GB on the owner's Ally X).
- **Start-up cleanup:** Edge startup boost and auto-launch, OneDrive setup,
  Teams, Phone Link, OEM tray apps, resident Snipping Tool.
- **Background apps policy:** stop Store apps running in the background.
- **Search's background web engine:** the feature flag that runs WebView2
  inside Search, off.
- **Windows AI parts:** Copilot, Recall, AI Fabric service, Click to Do, and
  AI features in Edge, Paint and Office.
- **Delivery Optimization:** no peer-to-peer update sharing.
- **Virtual Machine Platform and Hyper-V:** off unless the user needs WSL or
  virtual machines.
- **Handheld power settings:** separate plugged-in and battery values for CPU
  boost, energy preference, USB and PCIe power saving. Never the Ultimate
  Performance plan on a handheld.

Not shipped, because the evidence says no effect or harm on Windows 11:
SysMain off, memory compression off, pagefile off, timer resolution, Nagle
and TcpAckFrequency, NetworkThrottlingIndex and SystemResponsiveness,
Win32PrioritySeparation, MSI mode, MPO off, core parking, HPET and bcdedit
timer tweaks, Spectre and Meltdown mitigations off, Office Click-to-Run off,
touch keyboard service off or TextInputHost rename, Error Reporting off.
These are listed in the app's help as "myths we tested" so users know why
they are missing.

## 3. Cards

Each card shows three lines before it is picked: memory saved, fewer
processes, and what stops working, in plain words. Picking a card ticks its
switches; the user can change any switch after. A running total at the
bottom updates live.

| Item | Recommended | Gamer | Streamer | Max Lean |
|---|---|---|---|---|
| Bundled consumer apps | remove | remove | remove | remove |
| Manufacturer add-ons (Armoury Crate etc.) | remove | remove | remove | remove |
| Privacy, ads, tips | off | off | off | off |
| Idle background services | off | off | off | off |
| Edge | keep | remove | remove | remove |
| OneDrive | keep | remove | remove | remove |
| Search indexing | keep | off | off | off |
| Game Bar background recording | keep | off | keep | remove |
| Camera and capture parts (Camera app, Snipping Tool) | keep | keep | keep | remove |
| Microsoft Store | keep | keep | keep | remove |
| Xbox app, Game Bar, Gaming Services, Xbox home | keep | keep | keep | remove |
| Windows Update automatic downloads and installs | keep | keep | keep | off (owner 2026-10-24: the service stays available so Device updates can still check when the user presses Check) |
| Defender | keep | keep | keep | remove |
| Memory Integrity | unchanged | unchanged | unchanged | off |
| Fewer service processes (grouping threshold) | no | decided by measurement | decided by measurement | yes |
| Vendor GPU panel in background | keep | stop | stop | stop |
| Start-up cleanup, background apps policy | yes | yes | yes | yes |
| Windows AI parts (Copilot, Recall, AI Fabric) | remove | remove | remove | remove |
| Virtual Machine Platform, Hyper-V | unchanged | unchanged | unchanged | off (owner 2026-10-24: Gamer and Streamer leave them, because Memory Integrity and anti-cheat use the hypervisor and the measuring VM runs on the owner's PC) |
| Recovery tools (ISO mode only) | keep | keep | keep | remove |
| Printing and Print to PDF | keep | keep | keep | remove |
| Location service | keep | keep | keep | off (owner 2026-10-24) |
| Bluetooth, microphone, touch, rotation, battery, sleep | keep | keep | keep | keep |
| Power plan | unchanged | high performance (desktop) | high performance (desktop) | high performance (desktop) |

On battery devices the power plan is left alone on every card, because high
performance drains handheld batteries.

OneDrive on a Recommended ISO: the OneDrive setup entry is turned off on every
card (owner 2026-10-24), so an ISO built from Recommended does not set up
OneDrive for new accounts. Tune mode keeps an installed OneDrive.

**Custom** starts with nothing ticked.

Memory and process numbers on the cards are measured per card (section 7),
not added up from switches, because switches overlap.

Max Lean's Done screen has an **Add Xbox back** button that restores the
Store and the Xbox parts together. The restore method must be proven in the
test virtual machine before it ships.

Gamer and Streamer keep anti-cheat games working (Valorant, Fortnite, Call of
Duty, Battlefield 6, FACEIT). NitrOS never touches TPM, Secure Boot or IOMMU
on any card, and leaves Memory Integrity and VBS as the user has them,
because Vanguard and FACEIT increasingly require them. Max Lean turns Memory
Integrity off and says plainly that some of these games will not run.

The manufacturer add-ons switch says what is lost on an Ally: control-mode
switching, back-button mapping, TDP and fan modes, the GPU memory reserve
setting, and firmware, BIOS and controller updates (these then come from the
ASUS website by hand). Third-party replacements such as Handheld Companion are
not installed by NitrOS.

Microsoft's Xbox full-screen mode claims to return about 2 GB to games by not
loading the desktop. Max Lean removes it (owner's decision); the Start-up
group's "Steam Big Picture at boot" switch is the replacement.

The Done screen and cards show memory as "available to Windows" and note any
amount reserved for the GPU, so a 24 GB device showing 15.6 GB is explained.

## 4. Screens

1. **Start:** "Build a slim ISO" or "Tune this PC".
2. **Cards:** the five cards with their totals.
3. **Switches:** all groups. Each switch shows name, one-line description,
   memory saved, risk badge (Safe, Caution, Breaks something), and warnings
   such as "Game Pass needs this". A search box and the running total are
   always visible. In Tune mode, ISO-only switches are greyed out with the
   reason.
4. **Review:** everything that will change and everything that will stop
   working. Nothing happens until Go.
5. **Working:** progress bar and plain log. Interrupted ISO builds resume.
6. **Done:** ISO mode shows where the ISO went and reminds the user to test it
   in a virtual machine. Tune mode shows a measured before/after on this PC
   (memory in use and process count) and offers a restart.
7. **Device updates:** reachable from Start at any time. Lists BIOS,
   firmware and drivers for this PC as "Up to date" or "New version", with
   an Install button per item. Details in section 5.4.

## 5. Parts

Built in C# on .NET with WinUI 3 (Windows App SDK). Exact versions are
checked from official sources when the project is set up, not assumed.

```
X:\Projects\NitrOS
  catalog\            switch list, one JSON file per group
  src\NitrOS.App      screens only
  src\NitrOS.Core     loads and checks the catalog, dependencies, totals, cards
  src\NitrOS.Live     applies switches to running Windows, backup, undo, health check
  src\NitrOS.Image    applies switches to a mounted ISO image, writes the new ISO
  src\NitrOS.Updates  device update sources, version compare, verified install
  measure\            test virtual machine scripts and measured results
  tests\              automatic checks
  docs\
```

Each part has one job and talks to the others only through Core's types.
The app never touches Windows directly.

### 5.1 Switch entry

Each switch is one JSON object:

- `id`, `group`, `name`, `description` (one plain line)
- `risk`: `safe`, `caution` or `breaks`
- `breaks`: plain sentences shown as warnings ("Valorant will not start")
- `requires`: if switch S lists T, the feature S removes depends on the
  feature T removes (the Xbox app switch lists the Store switch). Core warns
  when T is on while S is off: "Microsoft Store is needed by Xbox app, which
  you are keeping." `neededBy` is worked out by Core from those
- `modes`: `live`, `image`, or both
- `reversible`: true or false (false gets the "Can't be undone without
  reinstalling" badge and a second confirm)
- `cards`: default per card
- `live` and `image`: lists of actions
- `measured`: memory MB, process count, Windows build, date (filled by the
  measurement run, never by hand)

Action types: set registry value, set service start type, disable scheduled
task, remove app (installed and provisioned), remove Windows capability,
disable Windows feature, remove component package (image only), remove file
or folder, set power option. Added 2026-10-24 for the catalog: uninstall a
Win32 program (live only, not reversible), turn off a start-up entry
(reversible), delete a registry key or value after exporting it
(reversible), make a power plan active (live only, reversible) and start a
program at sign-in (live only, reversible). Blocking Windows Update from
reinstalling maker apps is still an open question (section 11) and waits
for plan 3. No switch ships with an identifier that has not been checked. A `script` action exists for rare cases and is
flagged in review.

Core rejects any entry missing a description, risk, undo information for a
reversible live action, or a valid `requires` reference.

### 5.2 Data flow

Catalog files -> Core (check, resolve dependencies and warnings, apply card
defaults, compute totals) -> App (show, user edits) -> Review -> Live or
Image engine (apply, report progress) -> Done screen.

### 5.3 ISO builder rules (from Tiny11's open issues)

- Check that the host's DISM is at least the image's servicing version before
  starting; refuse with a clear message otherwise.
- Treat "pending operations deferred to first boot" as deferred, not failed.
- Removing Edge also removes its uninstall entries, so it does not linger in
  the installed-programs list.
- Never write offline to protected user keys that return access denied; use
  first-logon setup for those.
- Download the ISO-writing tool only from Microsoft and check a pinned hash.
- Test that a Max Lean ISO gets through Setup (Tiny11 Core ISOs hit a
  "cannot read ProductKey" error).

### 5.4 Device updates

Replaces the update part of Armoury Crate and other makers' apps, for any
brand, in layers. Each layer is a "source" with one interface: given the
device (maker, model, installed BIOS and driver versions read from Windows),
return a list of items with name, kind (BIOS, firmware, driver), installed
version, latest version, download URL, size and release date.

1. **Windows Update (every brand):** driver and firmware updates through the
   documented Windows Update Agent API.
2. **Maker source:** ASUS in version 1 (support-site listing, checked live on
   the owner's Ally X). Then Lenovo and Dell, then HP, each only after a live
   test. MSI and brands without a public listing get a button that opens
   their support page.
3. **GPU vendor:** AMD, NVIDIA or Intel graphics drivers direct, because
   makers' listings lag behind.

Where two sources offer the same item, the newest version wins and the
source is shown.

Rules:

- Downloads only from the maker's own servers (for ASUS,
  `dlcdnets.asus.com`). Before running, the file's digital signature must
  belong to that maker; otherwise it is refused.
- Nothing installs without the user pressing Install. BIOS and firmware
  require the charger connected and show a "do not turn off" warning.
- A small "known bad" list shipped with NitrOS (for example, a BIOS a maker
  pulled for bricking devices) blocks those versions from being offered.
- Checks run only when NitrOS opens or the user presses Check. No background
  updater.
- Windows Update stays allowed for drivers and firmware even when the
  manufacturer add-ons switch is on; only the add-on apps themselves are
  blocked from reinstalling.
- The `ASUS System Control Interface` driver is always kept: power and fan
  control depend on it.
- If a maker's listing stops working (none are officially documented except
  Windows Update and Dell), the source falls back to opening the support
  page and says why.

## 6. Undo and safety

- Before Tune mode changes anything: a Windows restore point, plus NitrOS's
  own record of every old value it is about to change, saved under
  `%ProgramData%\NitrOS\backups`.
- Tune mode turns things off rather than deleting wherever it can, so undo is
  flipping them back.
- Undo per switch, and Undo everything.
- After restart, a health check: Start menu, internet, sound, Bluetooth, and
  anything the card kept (Store, Game Pass). A failure points to the likely
  switch and offers to undo it.
- Big Windows updates can switch things back on. NitrOS checks when it is
  opened and asks before turning them off again. An optional "check after
  updates" task is off by default and exits when done, so nothing stays
  resident.
- ISO mode never changes the source ISO; it always writes a new file.
- Refuses to run on unsupported Windows builds, and warns when a handheld or
  laptop is on battery during an ISO build.

## 7. Measuring memory

- A Hyper-V virtual machine on the owner's PC. Hyper-V is not on yet; turning
  it on needs admin approval and a restart, done when this stage is reached.
- Fixed resources: 4 GB memory (dynamic memory off), 4 cores. Same ISO as
  NitrOS uses. A clean snapshot is the plain-Windows baseline.
- For each switch, automatically: restore snapshot, apply that switch,
  restart, idle 10 minutes, sample memory in use and process count five
  times, keep the middle value, subtract from the baseline.
- Each card is measured as a whole the same way.
- Results are written into the catalog's `measured` fields with Windows build
  and date, and rerun after big Windows releases.
- The app labels these as estimates. Tune mode's Done screen measures the
  real PC.

## 8. Testing

1. **Automatic checks** on every change: every catalog entry valid,
   dependency warnings correct, card defaults consistent, and apply then undo
   restores exact original values (against a test registry and service layer,
   not the real PC).
2. **Virtual machine:** build an ISO from each card, install it unattended,
   run the health check, measure. Tune mode the same, plus Undo everything
   checked against the clean snapshot.
3. **Anti-cheat check:** decides what Gamer and Streamer must keep for the
   listed games to start.
4. **Owner's Ally X, last:** only after the virtual machine passes. Start with
   Recommended in Tune mode with a restore point, then move up. Check what the
   controller, back buttons and power controls do without Armoury Crate.

## 9. Licensing and credit

- NitrOS code is new.
- Code borrowed from ChrisTitusTech/winutil (MIT) keeps its copyright notice,
  credited in `NOTICE.md`.
- Nothing is copied from Winhance (PolyForm Shield, forbids competing use) or
  Tiny11 Builder (no license). Only knowledge of what Windows settings do is
  used.

## 10. Not in version 1

Overclocking, per-game settings, built-in Windows download, Windows 10,
public release, maker update sources other than ASUS (Lenovo, Dell, HP come
after live tests). Driver installs are only those offered on the Device
updates screen.

## 11. To verify during the build (not assumed)

- Current .NET and Windows App SDK versions.
- The free Microsoft tool for writing the final ISO (oscdimg from the Windows
  ADK) and its download source.
- How to restore the Store and Xbox after Max Lean.
- How Windows Update reinstalls manufacturer apps, and how to block it.
  Partly answered 2026-10-25 in `docs/research/reinstall-block.md`. Driver
  packages bring back Dolby, Realtek and AMD apps through Store links. On the
  Ally, Armoury Crate is tied to the BIOS device `ACPI\ASUS9001`, and there is
  no WPBT table. The only documented, reversible block that leaves drivers
  alone is the device-metadata apps policy, which is not proven to stop
  either route. VM and Ally tests are still open.
- Why DISM's provisioned-app query is denied even when elevated on the
  owner's PC. Answered 2026-10-25 in `docs/research/dism-access-denied.md`.
  The likely cause is Shadow Defender: SYSTEM still works, the elevated admin
  is refused below the registry permissions. Not yet proven. NitrOS detects
  the refusal and tells the user, and never changes permissions.
- What the Ally X controller, back buttons and power controls do without
  Armoury Crate, and how to change the GPU memory reserve without it.
- Primary BattlEye, Easy Anti-Cheat and FACEIT requirement pages (the
  research could not read them).
- Whether Smart App Control blocks anything NitrOS does.
- How to read the Ally X controller (MCU) firmware version without Armoury
  Crate.
- How ASUS's BIOS and MCU updaters run silently or with progress, and their
  exit codes.
- Lenovo (Legion Go), Dell and HP listings, each tried live before shipping.
- How to check AMD, NVIDIA and Intel's latest driver versions.

## 12. Research

Findings behind this design, in `docs/research/`:

- `ally-x-inventory.md`: what runs on the owner's Ally X today.
- `winutil-tweaks.md`: every Chris Titus winutil tweak (commit 2f0bdc8).
- `winhance-settings.md`: every Winhance setting, facts only (commit db80ecf).
- `tiny11-removals.md`: Tiny11 removals, offline method, open issues
  (commit 8909da1).
- `web-evidence.md`: real vs myth vs harmful, anti-cheat requirements, Ally
  specifics, with dated sources.
- `device-updates.md`: update sources per brand; ASUS listing checked live.
- `reinstall-block.md`: how maker apps come back and what blocks it.
- `dism-access-denied.md`: why the provisioned-app query is denied on the
  owner's PC, and what NitrOS does about it.

Main conclusions:

1. On a real device the biggest Windows-side costs are manufacturer and vendor
   software and Defender. The bundled consumer apps are small each.
2. Process count is driven by service-host splitting, services and scheduled
   tasks.
3. Most popular registry "FPS tweaks" have no measurable effect on Windows 11.
4. None of the three reference projects publishes memory measurements, so
   NitrOS's test machine is the only source of real numbers.
