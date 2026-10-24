# Owner's ROG Xbox Ally X: what runs today

Read-only snapshot, 2026-10-24, uptime 26 h, while the owner was working
(Claude, Brave, Node, Steam open). Nothing was changed.

Device: ASUS ROG Xbox Ally X (RC73XA), Ryzen Z2 Extreme, 24 GB installed,
15.6 GB visible to Windows: about 8 GB is reserved for the integrated GPU
(the UMA frame buffer, set in Armoury Crate or the BIOS). Windows 11 Pro 26H2,
build 10.0.26300.

## Totals

| Measure | Value |
|---|---|
| Memory in use | 14.0 GB of 15.6 GB (1.6 GB free) |
| Commit | 22.1 of 24.1 GB |
| Processes | 295 (85 of them svchost) |
| Running services | 116 |
| Enabled scheduled tasks | 165 |
| VBS | running; security services reported running: 3, 4 (Memory Integrity, id 2, appears not running; to confirm) |

Most of the memory is the owner's own apps (Claude about 5.4 GB private,
Brave 3.1 GB, Node 1.4 GB, Steam about 1 GB). Those are out of NitrOS's scope.
The rows below are the Windows and manufacturer layer NitrOS can cut.

## Windows and manufacturer layer, by private memory

| Item | Processes | Private MB | NitrOS view |
|---|---|---|---|
| ASUS Armoury Crate framework (`asus_framework`) | 4 | 1196 (one instance 1070) | Biggest single cost. Removed on every card. |
| ASUS helpers (`asus-helper`, `ROGLiveService`, `ROGScreenManager`, `AacAmbientLighting`) | 4 | about 54 | Removed with Armoury Crate. |
| Defender (`MsMpEng`, `MpDefenderCoreService`) | 2 | about 592 | Kept except Max Lean. |
| AMD Software panel (`RadeonSoftware`, `AMDRSSrcExt`, `AMDRSServ`, `amdow`) | 4 | about 506 | New switch: stop the panel running in the background; keep the driver. |
| svchost (all) | 85 | 409 | Cut by turning off idle services. |
| Explorer, dwm, Start, shell hosts | 6 | about 690 | Core, kept. |
| Windows Search (`SearchHost`) | 1 | 175 | Off in Gamer and up. |
| Snipping Tool (resident) | 1 | 94 | Switch: stop it staying resident. |
| Edge (startup boost and auto-launch, no window open) | 5 | 105 | Switch: no preloading; removed in Gamer and up. |
| Realtek audio service | 3 | 81 | Driver-provided; check what breaks before offering. |
| Touch keyboard (`TabTip`) | 1 | 50 | Kept on handhelds. |
| Office Click-to-Run | 1 | 45 | Only if Office is installed; switch to stop the background service. |
| Cross Device Resume | 1 | 28 | Removed with Phone Link. |
| Game Bar, Gaming Services, GameInput | several | about 20 | Kept except Max Lean (GameInput stays, controllers use it). |

## Installed apps worth switches

Manufacturer: ArmouryCrateSE, ASUSCommandCenter, ASUSPCAssistant,
ASUSAmbientHAL, Dolby Access, Realtek Audio Control, AMD Radeon Software.

Microsoft extras: Xbox Insider, Dev Home (Microsoft has retired it),
Office ActionsServer and PushNotificationUtility, Cross Device, Web
Experience (Widgets), Edge, Snipping Tool, Photos, Paint, Notepad,
Calculator.

Start-up entries: OneDriveSetup, Edge auto-launch, Edge Update, Brave Update,
Discord, Shadow Defender, Windows Security tray.

## Lessons for the design

1. On this device the biggest wins are manufacturer and vendor software
   (Armoury Crate about 1.2 GB, AMD panel about 0.5 GB) and Defender
   (about 0.6 GB), not the Windows consumer apps, which are small each.
2. Many small services add up: 85 svchost processes and 165 scheduled tasks
   are the main process-count target.
3. A switch for "keep the GPU driver, stop the vendor panel" is needed for
   AMD (and NVIDIA on desktops).
