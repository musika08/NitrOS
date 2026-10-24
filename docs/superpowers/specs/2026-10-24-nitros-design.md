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
Ally X, Ryzen Z2 Extreme, 16 GB). It also has to work on desktops and other
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
   power plan, mouse acceleration off, network delay.
7. **Start-up:** optionally launch Steam Big Picture or Playnite full screen
   at boot (off unless chosen).

A tweak only ships if measurement shows a real gain or it clearly removes
load. Popular "FPS boost" tweaks that do nothing are left out.

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
| Camera and capture parts | keep | keep | keep | remove |
| Microsoft Store | keep | keep | keep | remove |
| Xbox app, Game Bar, Gaming Services, Xbox home | keep | keep | keep | remove |
| Windows Update | keep | keep | keep | off |
| Defender | keep | keep | keep | remove |
| Memory Integrity | keep | decided by anti-cheat test | decided by anti-cheat test | off |
| Recovery tools (ISO mode only) | keep | keep | keep | remove |
| Printing and Print to PDF | keep | keep | keep | remove |
| Bluetooth, microphone, touch, rotation, battery, sleep | keep | keep | keep | keep |
| Power plan | unchanged | high performance (desktop) | high performance (desktop) | high performance (desktop) |

On battery devices the power plan is left alone on every card, because high
performance drains handheld batteries.

**Custom** starts with nothing ticked.

Memory and process numbers on the cards are measured per card (section 7),
not added up from switches, because switches overlap.

Max Lean's Done screen has an **Add Xbox back** button that restores the
Store and the Xbox parts together. The restore method must be proven in the
test virtual machine before it ships.

Gamer and Streamer keep anti-cheat games working (Valorant, Fortnite, Call of
Duty, Battlefield 6): TPM, Secure Boot and whatever security parts the tests
show they need stay on. Max Lean says plainly that these games will not run.

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
- `requires`: switch ids that must stay kept for this one to work, and
  `neededBy` is worked out by Core from those
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
or folder, set power option. A `script` action exists for rare cases and is
flagged in review.

Core rejects any entry missing a description, risk, undo information for a
reversible live action, or a valid `requires` reference.

### 5.2 Data flow

Catalog files -> Core (check, resolve dependencies and warnings, apply card
defaults, compute totals) -> App (show, user edits) -> Review -> Live or
Image engine (apply, report progress) -> Done screen.

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

Driver installs, overclocking, per-game settings, built-in Windows download,
Windows 10, public release.

## 11. To verify during the build (not assumed)

- Current .NET and Windows App SDK versions.
- The free Microsoft tool for writing the final ISO (oscdimg from the Windows
  ADK) and its download source.
- How to restore the Store and Xbox after Max Lean.
- How Windows Update reinstalls manufacturer apps, and how to block it.
- What the Ally X controller, back buttons and power controls do without
  Armoury Crate.
- Which security parts each listed anti-cheat needs.
