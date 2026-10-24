# NitrOS web evidence: Windows 11 gaming and memory tweaks

Compiled 2026-10-24 from web sources fetched in this session. Target device: ROG Xbox Ally X (Ryzen AI Z2 Extreme).

Note on the device: spec sources list the Xbox Ally X with **24 GB LPDDR5X-8000** and the plain Xbox Ally (Z2 A) with 16 GB ([Tom's Hardware, 2025-26](https://www.tomshardware.com/pc-components/the-rog-xbox-ally-handheld-console-gets-its-biggest-price-drop-yet-currently-usd100-off-get-the-ryzen-z2-a-powered-handheld-for-less-than-usd500), [Wikipedia](https://en.wikipedia.org/wiki/ROG_Xbox_Ally)). The brief says "Z2 Extreme, 16 GB"; confirm on the unit (Task Manager > Memory).

Verdict key: **REAL** = measurable gain in published data. **small** = real but low single digits or only on weak hardware. **none** = no effect or no credible evidence. **harmful** = breaks things or loses safety for no gain. **depends** = trade-off decided by use.

Evidence quality: [P] primary (Microsoft, AMD, ASUS, game/anti-cheat vendor), [B] measured benchmark by an outlet, [C] community or forum, unverified.

## Summary table

| Tweak | Verdict | One-line reason | Best evidence |
|---|---|---|---|
| Xbox mode / full screen experience (Ally) | REAL | Defers desktop, startup apps, background work; ~2 GB RAM back per Microsoft | P + B |
| Lower UMA frame buffer (iGPU VRAM) | depends | Every GB reserved for GPU is gone from Windows; raise only for VRAM-hungry games | P |
| Disable startup apps (Teams, Phone Link, Edge boost, OEM tray apps) | REAL | Fewer processes, hundreds of MB; supported toggles | P + C |
| Widgets board off / removed | small-REAL | WebView2 host processes, a few hundred MB once opened | B (Ars via Slashdot, 2022) |
| Copilot / Recall / AI Fabric (Copilot+ only) | depends | WSAIFabricSvc reported at GBs on Copilot+ PCs; Recall is a supported optional feature | P + C |
| Phone Link / Cross Device | REAL (if leaking) | Reports of 15-20 GB leaks; turning off sharing is supported | C (many) |
| Memory Integrity (HVCI) / VBS off | small, conflicts with anti-cheat | ~5% avg, up to ~10-15% lows in CPU-bound cases; Vanguard on-demand, FACEIT, Fortnite tourneys want VBS/IOMMU | P + B |
| Virtual Machine Platform off | small | Microsoft lists it as a possible gaming cost; needed for WSL2/WSA | P |
| Game Mode | small (keep on) | Mostly 1% lows / pausing updates; mixed tests | P + B |
| HAGS | depends | Little FPS change; needed for NVIDIA frame gen; not required for AMD AFMF | P + B |
| Fullscreen / windowed optimizations | keep on | Flip model lowers latency (Microsoft) | P |
| MPO disable | none (fix only) | Only a flicker/stutter workaround | C |
| Game DVR background recording off | small | Encoder + buffer cost; no controlled test | C |
| SysMain off | none | No measured gaming gain on SSD; Intel says do not disable on SSD | C |
| Memory compression off | harmful on 16 GB shared RAM | Compression keeps data in RAM instead of paging | C |
| Pagefile off / tiny | harmful | Commit limit = RAM + pagefile; crash dumps need it | P |
| Search indexing off | small | Already throttles during gaming; breaks Start/Outlook search | P |
| svchost split threshold | none for RAM (cosmetic process count) | Grouping cuts process count ~70 to ~20 but split is by design | P |
| DiagTrack (telemetry) off | small | No Microsoft guidance either way; low cost | P |
| Delivery Optimization peer mode off (99) | small | Stops peer upload; supported setting | P |
| Defender off | harmful | Anti-cheat and Smart App Control rely on a sane security stack; no gaming benchmark | C |
| Windows Update services off | harmful | Vanguard on-demand needs 25H2; anti-cheat and Store games need current builds | P |
| Gaming Services removal | harmful | Breaks Store / Game Pass games and Xbox app | C (Microsoft advisors) |
| Ultimate Performance plan on handheld | harmful | Not offered on battery devices by design; burns battery; use Armoury Crate modes | P (2018) |
| Core parking off | none-small | Anecdotal; Armoury Crate / AMD firmware manage it | C |
| HPET / useplatformclock / disabledynamictick | harmful-none | No controlled gain; benchmarks show losses | C |
| Timer resolution tools / GlobalTimerResolutionRequests | none | Per-process since 2004; Windows 11 drops it for hidden windows | P |
| Nagle / TcpAckFrequency | none | Modern shooters use UDP | C |
| NetworkThrottlingIndex / SystemResponsiveness | none | MMCSS playback knobs, not game knobs | C |
| Win32PrioritySeparation | none | Default already boosts foreground | C |
| MSI mode | none-small | Modern GPU drivers already use MSI | C |
| Mouse acceleration off | depends | Only matters for non-raw-input games | P (via) + C |
| Spectre/Meltdown mitigations off | harmful | Little measured gain on modern AMD; security loss | C |
| Smart App Control off | depends | Can block Armoury Crate; re-enable no longer needs reinstall on newer 24H2/25H2 | C (Microsoft quoted) |
| Print Spooler, Fax, MapsBroker, Phone, Geolocation off | small | Microsoft lists as OK to disable if unused (Server guidance) | P |
| WER, TabletInputService off | harmful | Microsoft: do not disable; touch keyboard needed on a handheld | P |
| AMD Software minimal / driver-only | depends | Fewer processes but loses tuning, per-game profiles | P |
| Office ClickToRun service off | harmful | Stops Office security updates | P (ADMX) + C |
| Tiny11-style stripped image | harmful for this device | ~1% FPS; breaks Store/Xbox/anti-cheat paths; unsupported | C |

## Memory tweaks

### SysMain (Superfetch)
- Verdict: **none**. Leave on.
- Evidence: no controlled gaming benchmark shows gains on SSDs; Eleven Forum users report no difference, and a quoted Intel SSD guide says SysMain should not be disabled for SSDs on Windows 8+. Microsoft's service guidance gives "No guidance". SysMain uses free (standby) memory that is released on demand.
- Sources: [Eleven Forum thread, 2022](https://www.elevenforum.com/t/does-disabling-sysmain-superfetch-make-any-sense-with-an-nvme-system-drive.6625), [Microsoft service guidance, updated 2026-02](https://learn.microsoft.com/en-us/windows-server/security/windows-services/security-guidelines-for-disabling-system-services-in-windows-server).

### Memory compression (MMAgent)
- Verdict: **harmful on a 16-24 GB shared-memory handheld**.
- Evidence: compression trades a little CPU for keeping pages in RAM instead of writing them to the pagefile; guides report no difference on many systems and most experts advise keeping it. On a device where the iGPU takes 4-8 GB, losing compression means earlier paging.
- Sources: [Eleven Forum tutorial](https://www.elevenforum.com/t/enable-or-disable-memory-compression-in-windows-10-and-windows-11.3555/), [NinjaOne](https://www.ninjaone.com/blog/enable-or-disable-memory-compression-in-windows-11/).

### Pagefile
- Verdict: **harmful** to disable or shrink hard.
- Evidence: Microsoft says size depends on crash-dump setting and peak commit charge; commit limit is RAM plus all page files, so removing it lowers what games can allocate. Leave system-managed.
- Sources: [Microsoft Learn: page file size for 64-bit Windows](https://learn.microsoft.com/cs-cz/troubleshoot/windows-client/performance/how-to-determine-the-appropriate-page-file-size-for-64-bit-versions-of-windows), [TechNet wiki summary](https://learn.microsoft.com/en-us/archive/technet-wiki/53548.windows-troubleshooting-file-page-considerations).

### Windows Search indexing
- Verdict: **small**. Prefer "Respect device power mode" and a narrower scope.
- Evidence: Microsoft warns "Optimize your PC" tools that disable Windows Search break search; the indexer already backs off during gaming, low power, or high CPU/disk. Disabling breaks Start and Outlook search.
- Sources: [Microsoft Learn: Troubleshoot Windows Search performance](https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/windows-search-performance-issues), [Thurrott on 20H1 throttling, 2019](https://www.thurrott.com/?p=222164).

### Widgets and WebView2 processes
- Verdict: **small-REAL**.
- Evidence: Ars Technica (2022) found Widgets and Teams spawn WebView2 processes totalling a few hundred MB; Widgets processes start only when opened, then stay resident. A 2026 test found the Weather app (WebView2) over 1.2 GB while open. Taskbar toggle is supported; removing widgets.exe is a hack.
- Sources: [Slashdot summary of Ars, 2022-02](https://tech.slashdot.org/story/22/02/02/007226/hiding-windows-11s-teams-icon-doesnt-just-save-taskbar-space----it-also-saves-ram), [PCGH Weather app, 2026](https://www.pcgameshardware.de/Windows-Software-277633/News/Wetter-App-RAM-Verbrauch-WebView2-1-2-GiB-1550747/).

### Edge startup boost / background mode
- Verdict: **small**, supported.
- Evidence: Microsoft's StartupBoostEnabled policy starts Edge processes at sign-in and keeps them after the last window closes; BackgroundModeEnabled is the related policy. No Microsoft memory figure.
- Source: [Microsoft Learn: StartupBoostEnabled](https://learn.microsoft.com/DeployEdge/microsoft-edge-browser-policies/startupboostenabled).

### Phone Link / Cross Device service
- Verdict: **REAL when it leaks**, otherwise small.
- Evidence: multiple reports of the Cross Device Service growing to 15-20 GB; Microsoft has not acknowledged a bug. Supported fix: turn off "Share across devices" / Phone Link startup. No source found naming a "CrossDeviceResume" process specifically.
- Sources: [Neowin report](https://www.neowin.net/reports/this-windows-feature-has-been-eating-users-ram-for-years-microsoft-still-hasnt-fixed-it/), [Windows Report](https://windowsreport.com/windows-11-phone-link-bug-causes-massive-ram-usage-users-say/), [The Windows Club](https://www.thewindowsclub.com/microsoft-cross-device-service-high-memory-or-cpu-usage).

### Copilot, Recall, Click to Do, AI components
- Verdict: **depends** (Copilot+ PCs only for Recall/AI Fabric; Ally X has an NPU marketing name "Ryzen AI", check if WSAIFabricSvc exists).
- Evidence: one report of WSAIFabricSvc host processes using ~5 GB idle; disabling Recall alone did not stop it. Recall can be removed via Optional features / "Allow Recall to be enabled" policy, which Microsoft supports.
- Sources: [Eleven Forum WorkloadsSessionHost thread](https://www.elevenforum.com/t/task-manager-workloadssessionhost-consuming-cpu-memory-usage-recall.45675/post-736198), [Windows Latest, 2025-04-28](https://windowslatest.com/2025/04/28/disable-remove-recall-feature-in-windows-11), [Pureinfotech](https://pureinfotech.com/uninstall-recall-windows-11/).

### Teams / new Outlook in background
- Verdict: **REAL** (startup disable is supported).
- Evidence: Teams auto-starts and spawns WebView2 even without sign-in; disabling it in Startup apps or unpinning stops that. Nothing found on olk.exe specifically.
- Sources: [Winaero](https://winaero.com/unpin-teams-from-taskbar-in-windows-11-to-save-ram), [Guiding Tech](https://www.guidingtech.com/reduce-microsoft-teams-memory-usage-in-windows-10-and-windows-11/).

### Game Bar, Game DVR, Gaming Services
- Verdict: DVR background recording off = **small**; Gaming Services removal = **harmful**.
- Evidence: no controlled DVR benchmark; claims of 5-15% are unsourced; recording uses the hardware encoder. Removing Gaming Services broke Minecraft and Store games; Microsoft advisors say it is needed for most Store games. Game Bar is an entry point for Xbox mode.
- Sources: [Eleven Forum Record what happened](https://www.elevenforum.com/t/enable-or-disable-record-what-happened-for-gaming-captures-in-windows-11.17900), [Microsoft Q&A Gaming Services](https://learn.microsoft.com/en-us/answers/questions/702009/microsoft-gaminig-services-is-not-working), [Microsoft Community un-uninstalling Gaming Services](https://answers.microsoft.com/en-us/xbox/forum/all/un-uninstalling-gaming-services-windows-11/fd6fd0a7-c9d1-485a-be27-a0a3293c1037).

### Microsoft Defender
- Verdict: **harmful** to disable; use exclusions and scan scheduling instead.
- Evidence: only anecdotal disk/CPU spikes at game launch; no benchmark. A Q&A user saw MsMpEng memory unchanged even with real-time protection off. Disabling also weakens the security baseline anti-cheats increasingly check.
- Sources: [Windows Report](https://windowsreport.com/microsoft-defender-high-hdd-cpu/), [Microsoft Q&A](https://learn.microsoft.com/en-us/answers/a/6896541).

### Windows Update services and Delivery Optimization
- Verdict: Update off = **harmful**; DO peer mode off = **small**, supported.
- Evidence: Vanguard on-demand needs 25H2 features (Runtime Driver Attestation). DO download mode 99 = HTTP only, no peering; 100 (bypass) is deprecated in Windows 11.
- Sources: [Microsoft Learn DO reference](https://learn.microsoft.com/en-us/windows/deployment/do/waas-delivery-optimization-reference), [Intune DO settings](https://learn.microsoft.com/en-us/intune/device-configuration/templates/ref-delivery-optimization-settings-windows).

### Telemetry (DiagTrack)
- Verdict: **small**.
- Evidence: Microsoft's service guidance gives "No guidance" for DiagTrack; it also powers some connected experiences. Low memory cost; setting diagnostic data to Required via policy is the supported route.
- Source: [Microsoft service guidance](https://learn.microsoft.com/en-us/windows-server/security/windows-services/security-guidelines-for-disabling-system-services-in-windows-server).

### svchost split threshold (SvcHostSplitThresholdInKB)
- Verdict: **none for real RAM**; changes process count only.
- Evidence: Microsoft: since 1703, services split into separate svchost processes when RAM is above 3.5 GB; grouped instance counts are ~17-21 vs ~67-74 split. Microsoft says grouping gives only a "modest reduction" in footprint, and splitting brings reliability, security and per-service diagnostics. The registry value itself is not documented by Microsoft; third-party guides describe it as the RAM threshold (default 380000 KB). Raising it above installed RAM regroups services: fewer processes, slightly less RAM, worse isolation (one crash takes down siblings).
- Sources: [Microsoft Learn: Service host grouping, updated 2025-09](https://learn.microsoft.com/windows/application-management/svchost-service-refactoring), [NinjaOne](https://www.ninjaone.com/blog/change-the-svchost-exe-split-threshold).

### Background apps policy
- Verdict: **small**, supported.
- Evidence: Group Policy "Let Windows apps run in the background" (LetAppsRunInBackground under AppPrivacy) with per-app allow/deny lists. Guides disagree on value meanings; verify against ADMX before shipping (commonly 2 = force deny).
- Source: [Eleven Forum tutorial](https://elevenforum.com/t/enable-or-disable-background-apps-in-windows-11.923/), [NinjaOne](https://www.ninjaone.com/blog/configure-background-apps-in-windows-11/).

## General memory reductions

Microsoft itself announced in 2026 that lowering Windows 11 memory use (allocator, WinUI 3, WebView2/Chromium efficiency) is a priority through end of 2026, with no figures yet ([Guru3D](https://www.guru3d.com/story/microsoft-prioritizes-lower-windows-11-ram-usage-for-8gb-pcs/), [Windows Latest, 2026-03-23](https://windowslatest.com/2026/03/23/microsoft-once-tried-to-cut-windows-11-ram-usage-install-disk-size-by-20-now-its-trying-again-in-2026)). NitrOS gains may shrink with future builds.

Service recommendations below come from Microsoft's "Security guidelines for system services" (written for Server 2016 Desktop Experience, page updated 2026-02-16). It is the only Microsoft per-service list; client behavior can differ. [Link](https://learn.microsoft.com/en-us/windows-server/security/windows-services/security-guidelines-for-disabling-system-services-in-windows-server).

| Item | Verdict | Evidence |
|---|---|---|
| Startup apps (Settings > Apps > Startup) | REAL | Supported; Xbox mode itself delays startup apps until the desktop is first opened ([Microsoft KB5070297, 2025-10-20](https://support.microsoft.com/en-US/servicing/os/windows/docs/2025/10/windows-gaming-full-screen-experience)). |
| Widgets board, Start web search | small-REAL | WebView2 cost above. Start web search: no memory measurement found. |
| Recall, Click to Do, AI Fabric | depends | See above; Copilot+ only. |
| Copilot app | small | Startup toggle; no measurement found. |
| Windows Studio Effects | none found | No memory data found; tied to NPU camera drivers. |
| Windows Hello / Biometric (WbioSrvc) | depends | Microsoft: no guidance. Needed for face/fingerprint sign-in. |
| Touch keyboard (TabletInputService) | harmful | Microsoft: do not disable when desktop is installed. A handheld needs it. |
| Phone Link / Cross Device | REAL if leaking | See above. |
| Smart App Control | depends | Blocks some OEM tools (Armoury Crate reports); re-enable without reinstall reported on newer 24H2/25H2 ([Neowin](https://www.neowin.net/news/this-windows-11-feature-no-longer-requires-clean-installing-the-system-to-activate-it/), [Windows Latest, 2025-12-16](https://windowslatest.com/2025/12/16/microsoft-confirms-you-can-soon-disable-smart-app-control-without-reinstalling-windows-11)). |
| Windows Error Reporting (WerSvc) | harmful | Microsoft: do not disable. Starts on demand anyway. |
| Connected User Experiences (DiagTrack) | small | Microsoft: no guidance. |
| Print Spooler | small | Microsoft: OK to disable if not a print server/DC. Breaks printing and PDF printers. |
| Fax, Retail Demo | small | Not in fetched rows; both are demand-start and rarely running. |
| Downloaded Maps Manager (MapsBroker) | small | Microsoft: OK to disable if no apps rely on it. |
| Geolocation (lfsvc), Phone Service | small | Microsoft: OK to disable if unused. |
| Windows Insider Service | small | Microsoft: OK to disable. |
| Xbox Live Auth Manager etc. | harmful on this device | Microsoft says disable on Server, but Xbox app, Xbox mode and Game Pass need them on a gaming handheld. |
| Windows Search | small | Microsoft lists it as already disabled on Server; on client it powers Start/Outlook search. |
| Font Cache | none | Microsoft: no guidance; caches speed font load. |
| WSL / VM Platform / Hyper-V | small | vmmem only uses RAM while a VM runs; WSL2 returns idle memory. Enabling VMP puts Windows under the hypervisor; Microsoft lists VMP as a possible gaming cost ([Puget Systems](https://www.pugetsystems.com/labs/hpc/does-enabling-wsl2-affect-performance-of-windows-10-applications-1832/), [Microsoft WSL blog, 2019-10-30](https://devblogs.microsoft.com/commandline/memory-reclaim-in-the-windows-subsystem-for-linux-2/)). |
| Office ClickToRun service | harmful | Stops Office updates; use the Office "Enable Automatic Updates" policy instead ([MUO via mirror](https://365.telkomuniversity.ac.id/?p=39163), [Microsoft archive blog](https://learn.microsoft.com/it-it/archive/blogs/ouc1too/patching-office-365-with-configuration-manager-2012-or-how-i-learned-to-stop-worrying-and-love-the-click2rclient)). |
| AMD Software (AMDRSServ, AMDRSSrcExt, overlay) | depends | AMD offers Full / Minimal / Driver Only installs; Minimal drops tuning and recording, Driver Only has no UI ([AMD RSX2-INSTALL](https://www.amd.com/en/resources/support-articles/faqs/RSX2-INSTALL.html)). No memory figures found. On the Ally, Armoury Crate covers TDP, so Minimal is a reasonable option. |
| Realtek audio service | none found | No evidence found; the Ally audio stack is OEM-specific, leave alone. |
| Scheduled tasks volume | none found | No measurement found; tasks run briefly, not resident. |
| Edge WebView2 background | small-REAL | Count follows apps that host it (Widgets, Teams, Weather). |
| Store auto-updates / BITS | small | BITS is demand-start; turning off Store auto-update leaves Gaming Services and Xbox app stale. Keep on. |
| Prefetch (Superfetch-related) | none | Same as SysMain. |
| Windows Backup app | none found | No measurement found. |
| Tiny11-style stripped image | harmful | Forum consensus about 1% FPS difference; Tiny11 idles near 2 GB but strips Store/Xbox components ([TechPowerUp forum](https://techpowerup.com/forums/goto/post?id=4966616), [MuyComputer, 2026-07-13](https://www.muycomputer.com/2026/07/13/tiny11-25h2-reduce-windows-11-a-solo-8-gb-de-espacio-y-2-gb-de-ram-manteniendo-la-interfaz-intacta/)). |

## Gaming latency tweaks

### Game Mode
- Verdict: **small**, keep on.
- Evidence: pauses Windows Update and deprioritises background work; tests show under 1 FPS average change, slight wins or losses in 1% lows by title.
- Sources: [windowsnews.ai test](https://windowsnews.ai/article/windows-11-game-mode-can-hurt-performance-in-cpu-heavy-games-tests-show-try-this-alternative.441277), [allthings.how](https://allthings.how/windows-11-game-mode-on-or-off-in/).

### HAGS
- Verdict: **depends**.
- Evidence: Microsoft says most users won't notice; GamersNexus measured little FPS change; required for NVIDIA DLSS frame generation, not AMD AFMF.
- Sources: [Microsoft DirectX blog, 2020-06-30](https://devblogs.microsoft.com/directx/hardware-accelerated-gpu-scheduling/), [GamersNexus](https://gamersnexus.net/guides/3599-windows-10-hardware-accelerated-gpu-scheduling-benchmarks).

### VBS / Memory Integrity (HVCI)
- Verdict: **small** perf gain, but **conflicts with anti-cheat direction**.
- Evidence: Microsoft (2022) says Memory Integrity and VMP can cost performance in some configurations and lets gamers turn them off while playing. Tom's Hardware (RTX 4090) measured ~5% average, up to ~10% in some runs and bigger hits on 1% lows; MBEC (Zen 2+) shrinks the HVCI cost. No Ally-specific test found. Riot on-demand, FACEIT and Fortnite tournaments now want VBS/HVCI or IOMMU.
- Sources: [Microsoft support via TechRadar, 2022-10](https://www.techradar.com/news/are-you-a-gamer-running-windows-11-heres-microsofts-advice-to-speed-up-games), [Tom's Hardware benchmark, 2023](https://www.tomshardware.com/news/windows-11-gaming-benchmarks-performance-vbs-hvci-security/).

### Virtual Machine Platform
- Verdict: **small**. Off unless WSL2 is used; note VBS needs the hypervisor anyway.
- Source: same Microsoft 2022 article ([Thurrott](https://www.thurrott.com/windows/274452/windows-11-security-visualization-features-gaming-performance)).

### Fullscreen and windowed optimizations
- Verdict: **keep on**.
- Evidence: Microsoft moved DX10/11 windowed games to flip model for lower latency; flip model can bypass composition like exclusive fullscreen.
- Sources: [DirectX blog, 2022-02-16](https://devblogs.microsoft.com/directx/updates-in-graphics-and-gaming/), [DXGI flip model](https://devblogs.microsoft.com/directx/dxgi-flip-model/).

### MPO
- Verdict: **none** as performance tweak; troubleshooting only (OverlayTestMode=5). Reports say it no longer works on recent Windows 11.
- Source: [Guru3D 2025 thread](https://forums.guru3d.com/goto/post?id=6305178).

### Power plans, Ultimate Performance, core parking (AMD handheld)
- Verdict: Ultimate Performance **harmful** on handheld; core parking tweaks **none-small**.
- Evidence: Microsoft shipped Ultimate Performance as unavailable on battery systems because it cuts micro-latency by drawing more power. ASUS controls TDP via Armoury Crate modes (Silent/Performance/Turbo/Manual) and Windows power mode; no benchmark compares plans on the Ally. Core parking evidence is anecdotal.
- Sources: [Laptop Mag, 2018](https://www.laptopmag.com/articles/windows-10-ultimate-performance-mode), [ASUS Ally performance guide](https://rog.asus.com/articles/guides/how-to-boost-gaming-performance-on-the-rog-ally), [Bitsum ParkControl](https://bitsum.com/parkcontrol/).

### HPET, useplatformclock, disabledynamictick
- Verdict: **harmful-none**.
- Evidence: forum benchmarker found both reduce performance; advice is to delete the values. No controlled gain.
- Source: [Guru3D HPET thread](https://forums.guru3d.com/goto/post?id=5827684).

### Timer resolution
- Verdict: **none**.
- Evidence: Microsoft: since Windows 10 2004 timeBeginPeriod is per-process, and on Windows 11 a minimized or hidden window gets no guaranteed higher resolution. GlobalTimerResolutionRequests is undocumented, described by its own tool author as for debugging.
- Sources: [Microsoft Learn timeBeginPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod), [valleyofdoom/TimerResolution](https://github.com/valleyofdoom/TimerResolution).

### Nagle / TcpAckFrequency / TCPNoDelay
- Verdict: **none** for modern shooters (UDP).
- Source: [Wikipedia Nagle's algorithm](https://en.wikipedia.org/wiki/Nagle%27s_algorithm), [connected.app](https://www.connected.app/library/nagles-algorithm-tcp-nodelay-53dx322).

### NetworkThrottlingIndex / SystemResponsiveness
- Verdict: **none**.
- Evidence: MMCSS settings for multimedia playback (default 10 packets/ms and 20% reserve); Bitsum advises leaving defaults.
- Source: [Bitsum docs](https://bitsum.com/docs/pl/Advanced%20Tools/advanced_tools.htm), [SpeedGuide](https://www.speedguide.net/articles/-5812/p-1).

### Mouse acceleration
- Verdict: **depends**. Raw-input games ignore it; matters only for games using Windows cursor input. On a handheld with gamepad, irrelevant.
- Source: [Upcomer](https://upcomer.com/should-you-turn-on-enhance-pointer-precision-for-gaming-explained/).

### Win32PrioritySeparation
- Verdict: **none**. Default (2 / 0x26) already gives 3:1 foreground boost; claims are feel-based.
- Source: [Blur Busters thread](https://forums.blurbusters.com/viewtopic.php?p=98581).

### MSI mode
- Verdict: **none-small**. No Microsoft or benchmark evidence found; modern GPU/NVMe drivers typically already use MSI/MSI-X.
- Source: [Wikipedia MSI](https://www.Wikipedia.org/wiki/MSI-X).

### Spectre/Meltdown mitigations off
- Verdict: **harmful**. Only anecdotal gains; AMD parts need fewer mitigations; registry keys may not disable all (STIBP).
- Source: [NTLite thread](https://ntlite.com/community/threads/how-to-disable-windows-11-spectre-mitigations.6064/).

## Anti-cheat requirements (Windows 11)

| Game / anti-cheat | TPM 2.0 | Secure Boot | VBS / HVCI | IOMMU | Other | Stripped Windows |
|---|---|---|---|---|---|---|
| Valorant / LoL (Riot Vanguard) | Yes | Yes | May be required (Pre-Check); required for On-Demand mode | May be required; required for On-Demand | On-Demand (2026-06-24) needs Windows 11 25H2 + Runtime Driver Attestation | Kernel driver at boot; needs intact boot chain. No Defender/service requirement stated. |
| Fortnite (Easy Anti-Cheat) | Tournaments only | Tournaments only | Not stated | Tournaments only (from Feb 19, 2026) | Casual play has no such requirement | No official statement found. |
| Call of Duty BO7 / Warzone (Ricochet) | Yes | Yes | Not stated | Not stated | Remote attestation via Azure; kernel driver | No Defender/Windows Update requirement found. |
| Battlefield 6 (Javelin) | Press says yes; EA page names only Secure Boot | Yes (mandatory) | Not stated | Not stated | Targets VMs and emulation | Not addressed. |
| FACEIT (CS2) | Yes (all players from 2025-11-25) | Yes | VBS required where IOMMU enforced | Rolling out since 2025-04; enforced for 3000+ Elo since 2025-08 | Windows 11 required from 2026-10-14 | Not addressed. |
| BattlEye | Not stated | Not stated | Test-signing mode blocked; older driver failed with Hardware-enforced Stack Protection | Not stated | Root certificates and system file integrity checked | Not addressed. |

Sources: [Riot Vanguard security requirements, 2026-05-31](https://support.riotgames.com/riot/performance/vanguard-security-requirements); [Tom's Hardware on Vanguard On-Demand, 2026-06](https://www.tomshardware.com/video-games/pc-gaming/riot-vanguard-adds-an-on-demand-mode-that-stops-anti-cheat-loading-at-boot-on-secured-windows-11-pcs); [MobileSyrup, 2026-06-26](https://mobilesyrup.com/2026/06/26/you-can-now-use-riots-anti-cheat-on-demand/); [Fortnite anti-cheat requirements](https://www.fortnite.com/news/new-fortnite-anti-cheat-pc-requirements-and-latest-legal-action) (403 on fetch; via [Windows Report](https://windowsreport.com/?p=1487707)); [Ricochet BO7 beta blog, 2025-09](https://www.callofduty.com/blog/2025/09/call-of-duty-black-ops-7-beta-ricochet-anti-cheat-update); [Ricochet day one, 2025-11](https://www.callofduty.com/blog/2025/11/call-of-duty-black-ops-7-ricochet-anticheat-day-one); [EA Battlefield 6 Secure Boot, 2025-08-04](https://www.ea.com/games/battlefield/battlefield-6/news/secure-boot-information); [FACEIT Windows Security Requirements FAQ](https://support.faceit.com/hc/en-us/articles/23117181142556) (403 on fetch; via search snippets and [Dust2.us](https://www.dust2.us/news/66964/faceit-announces-major-anti-cheat-update)); [Ubisoft BattlEye FAQ](https://www.ubisoft.com/en-us/game/rainbow-six/siege/news-updates/2999gdyIXLbeiZP78R7545/battleye-faq).

Implications for NitrOS:
- Never disable TPM, Secure Boot, VBS/HVCI, Hyper-V or the hypervisor platform by default; at most offer an off-by-default "Memory Integrity off" with an anti-cheat warning.
- Keep Windows Update functional: Vanguard On-Demand depends on 25H2 features.
- No vendor states a hard Defender or Xbox-service dependency, but none supports modified images. No source confirms EAC breaking on tiny11; risk is unverified, not disproven.
- Never enable test-signing (BattlEye blocks it).

## ROG Ally / Ally X / Xbox Ally specifics

### Armoury Crate SE and ASUS services
- What it does: ASUS calls it the single control platform: control modes and button mapping, operating modes (TDP), fan, Command Center, firmware, BIOS, MCU and driver updates. MCU firmware is delivered through the Armoury Crate SE Update Center.
- Memory cost: no handheld measurement found. Desktop Armoury Crate forum reports describe asus_framework leaks and a 15% CPU helper; not proven on the Ally.
- Without it: back-button remapping, TDP modes, fan curves and MCU/BIOS updates lose their supported UI. ASUS support's own fix for broken Command Center is to remove and reinstall ASUS System Control Interface v3 plus Armoury Crate SE, implying those are the core pieces.
- Smart App Control has flagged Armoury Crate on Ally devices after a Windows update.
- Sources: [ASUS Armoury Crate SE intro](https://www.asus.com/support/FAQ/1050220), [ASUS ROG forum MCU thread](https://rog-forum.asus.com/t5/handheld-gaming/mcu-firmware-312-not-updating/td-p/1064713), [RetroHandhelds](https://retrohandhelds.gg/tag/asus-rog-xbox-ally/), [ASUS forum on asus_framework](https://rog-forum.asus.com/t5/previous-forum/is-this-normal-armoury-crate-activity/td-p/888754).

### Community alternatives
- Handheld Companion lists ROG Ally, Ally X, Xbox ROG Ally and Xbox ROG Ally X; offers TDP/Auto TDP, gyro, remapping, per-game profiles; relies on HidHide (bundled). Back buttons and fan control not stated on the README. G-Helper is another ASUS-control alternative. Guidance: pick one owner for TDP, fans and buttons; do not run two.
- Sources: [Handheld Companion GitHub](https://github.com/Valkirie/HandheldCompanion), [TechRadar on Armoury Crate issues](https://www.techradar.com/computing/gaming-pcs/having-problems-with-your-asus-rog-ally-a-new-patch-for-armoury-crate-might-have-fixed-some-issues-but-not-all-of-them).

### Xbox mode (full screen experience)
- Windows 11 24H2+; Settings > Gaming > Xbox mode, choose home app. On handhelds "Enter Xbox mode on startup" limits background processes, startup apps launch only when the desktop is first opened, Win+F11 toggles. Requires the Xbox app (Microsoft Insider blog); Game Bar is an entry point; Gaming Services named only by third parties but Store games need it anyway.
- Measured: Microsoft (Potvin) said ~2 GB memory returned to games and one third the idle power. Notebookcheck saw 38 vs 29 FPS in Shadow of the Tomb Raider, but the same result was reached on the desktop by closing apps manually; HotHardware's 15-game test found little gain and no regressions.
- Sources: [Microsoft KB5070297, 2025-10-20](https://support.microsoft.com/en-US/servicing/os/windows/docs/2025/10/windows-gaming-full-screen-experience), [Windows Insider blog, 2025-11-21](https://blogs.windows.com/windows-insider/2025/11/21/full-screen-experience-expands-to-more-windows-11-pc-form-factors-for-windows-insiders/), [Thurrott](https://www.thurrott.com/games/xbox/xbox-ally-gaming-handheld/321970/xbox-allys-full-screen-xbox-experience-is-coming-to-other-windows-handhelds), [Notebookcheck, 2025-09-18](https://www.notebookcheck.net/Xbox-full-screen-mode-on-original-Asus-ROG-Ally-boosts-gaming-handheld-performance-but-with-caveats.1117352.0.html), [HotHardware](https://hothardware.com/news/xboxs-new-full-screen-gaming-mode).
- NitrOS implication: the Xbox mode gain is basically "close background stuff", which is what NitrOS does. Keep the Xbox app, Gaming Services and Game Bar so Xbox mode keeps working.

### iGPU VRAM (UMA frame buffer)
- ASUS: Armoury Crate > Settings > Operating Mode > GPU Settings > Memory Assigned to GPU; default 4 GB, max 8 GB (original Ally guide, 2023-06-09), reboot required. The reserve comes out of system RAM: raising it helps games that refuse to launch or stutter on VRAM, lowering it gives Windows more memory. A third-party page claims Ally X defaults differ (Auto or 8 GB); not verified.
- Sources: [ASUS VRAM guide](https://rog.asus.com/articles/guides/how-to-increase-the-rog-allys-vram-allocation).
- NitrOS implication: report the current reservation and explain the trade-off; do not change it silently.

### Ally power guidance
- ASUS: Silent / Performance / Turbo / Manual (SPL/SPPT, fan curve); on Xbox Ally the SPPT value is the stable wattage. Windows power mode set separately. Ultimate Performance is not intended for battery devices.
- Source: [ASUS performance guide](https://rog.asus.com/articles/guides/how-to-boost-gaming-performance-on-the-rog-ally), [Windows Central Xbox Ally tweaks](https://www.windowscentral.com/microsoft/windows-11/12-essential-windows-11-tweaks-to-supercharge-your-asus-rog-xbox-ally/).

## Gaps (not verified this session)
- No primary BattlEye or Easy Anti-Cheat requirements page could be read; FACEIT and Fortnite pages returned 403 (data from search snippets and press).
- No measured idle RAM for Armoury Crate SE, AMD Software, Realtek, Studio Effects, Hello, Windows Backup.
- No Ally-specific VBS/HVCI or power-plan benchmark found.
- LetAppsRunInBackground value meanings need checking against the ADMX before shipping.
