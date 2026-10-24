# NitrOS Plan 2: Catalog Content Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the researched switch inventory into the real catalog: the five extra action types the content needs, every identifier checked, every group written as JSON, and tests that pin the cards to the design's cards table.

**Architecture:** Core (`src/NitrOS.Core`) gets five new action records and their checker rules. The catalog lives in `catalog/*.json`, one file per group. The source of truth for each switch's content is `docs/research/switch-inventory.md`. Task 2 checks every identifier in it and marks each row VERIFIED or DROPPED; Tasks 3 to 7 copy the VERIFIED rows into JSON; Task 8 pins the cards table and the safety promises with tests.

**Tech Stack:** As plan 1: .NET 10 (SDK 10.0.402), C#, System.Text.Json, xUnit v3. Read-only PowerShell for the identifier checks.

**Spec:** `docs/superpowers/specs/2026-10-24-nitros-design.md` (sections 2, 3, 5.1; owner decisions dated 2026-10-24 in the cards table). Read it with the inventory before starting.

**This is plan 2 of 7.** Plan 1 (Core) is merged. Later: 3 PC tuner, 4 measurement VM, 5 app screens, 6 ISO builder, 7 device updates.

## Global Constraints

- Everything from plan 1's Global Constraints still holds (SDK pin, net10.0, Nullable, TreatWarningsAsErrors, no NuGet runtime packages or Windows APIs in Core, camelCase JSON with kebab-case enums, descriptions one line ≤120 characters, xUnit v3 test project as Exe, commit trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).
- Namespaces: `NitrOS.Core.Model`, `NitrOS.Core.Json`, `NitrOS.Core.Loading`, `NitrOS.Core.Selecting`; tests mirror them under `NitrOS.Core.Tests.*`. Never add `using Xunit;`.
- No switch ships with an identifier that was not checked (spec 5.1). An unchecked row is DROPPED for v1, not guessed.
- Never a switch for TPM, Secure Boot, IOMMU, test-signing, touch keyboard service, sensors and rotation, battery and sleep, WebView2, GameInput, or the ASUS System Control Interface driver. Nothing from the spec's "Not shipped" list.
- `requires` means: if switch S lists T, S's feature depends on T's feature (the Xbox app switch lists the Store switch). The inventory's own note on `requires` (its "How to read" section) is wrong and must not be followed.
- Card defaults follow the spec's cards table exactly, including the owner's 2026-10-24 decisions: Virtual Machine Platform and Hyper-V are unchanged on Recommended, Gamer and Streamer; Max Lean turns off Windows Update's automatic downloads and installs, but the service stays available.
- Bluetooth and printing stay switchable (owner): Bluetooth's switch is off on every card; printing's is on only in Max Lean.
- Identifier checks are read-only. Nothing in this plan changes the PC.
- Credit: entries taken from winutil keep the `NOTICE.md` credit (already present). Nothing is copied from Winhance or Tiny11 code; only facts about Windows settings.

## Review Focus

1. A card turning off something the spec says that card keeps (for example Recommended removing Edge, OneDrive, the Store, Xbox, Defender, Windows Update, printing or Bluetooth). Test: `Recommended_KeepsWhatTheSpecKeeps` (Task 8).
2. Gamer or Streamer touching what anti-cheat needs (Memory Integrity, Virtual Machine Platform, Hyper-V). Test: `GamerAndStreamer_LeaveAntiCheatPartsAlone` (Task 8).
3. A kept feature losing its dependency with no warning: Gamer plus the Store switch turned on must warn about the Xbox app. Test: `RemovingStoreWhileKeepingXbox_Warns` (Task 8).
4. Battery and touchscreen devices: the power-plan switch is skipped on a battery device on every card. Test: `PowerPlan_SkippedOnBattery` (Task 8).
5. A group file whose switches drift from the verified inventory rows (missing or extra ids). Test: each content task's `<Group>_HasExactlyTheVerifiedIds`.

---

### Task 1: Five new action types

**Files:**
- Modify: `src/NitrOS.Core/Model/Actions.cs`, `src/NitrOS.Core/Model/Enums.cs`, `src/NitrOS.Core/Loading/CatalogValidator.cs`, `src/NitrOS.Core/Loading/CatalogLoader.cs` (null-item check covers any new list fields)
- Test: `tests/NitrOS.Core.Tests/Json/CatalogJsonTests.cs`, `tests/NitrOS.Core.Tests/Loading/CatalogValidatorTests.cs`

**Interfaces:**
- Produces (namespace `NitrOS.Core.Model`):
  - `enum StartupSource { Run, StartupFolder, AppxTask, Task }`, written in JSON as `run`, `startup-folder`, `appx-task`, `task`.
  - `ProgramUninstall(string DisplayName, string Publisher, string? Args = null)`, discriminator `"program.uninstall"`. `DisplayName` may contain `*` wildcards. `IsDestructive` true.
  - `StartupDisable(StartupSource Source, string Name, string? Hive = null)`, discriminator `"startup.disable"`. Not destructive.
  - `RegistryDelete(string Hive, string Key, string? Name = null)`, discriminator `"registry.delete"`. A null `Name` means the whole key. Not destructive (the PC tuner exports first).
  - `PowerScheme(string Scheme)`, discriminator `"power.scheme"`. Not destructive.
  - `LaunchAtLogon(string Program, string? Args = null)`, discriminator `"launch.at-logon"`. Not destructive.
- New validator rules, one test each (names below):
  - `ProgramUninstall_LiveOnlyAndNamed`: not allowed in `image`; `displayName` and `publisher` are non-empty.
  - `StartupDisable_HiveRules`: `source: run` requires `hive` HKLM or HKCU; other sources must not set `hive`; in `image` only `source: run` is allowed.
  - `RegistryDelete_HiveAndKey`: hive HKLM or HKCU; `key` non-empty.
  - `PowerScheme_LiveOnlyGuid`: `scheme` parses as a GUID; not allowed in `image`.
  - `LaunchAtLogon_LiveOnly`: `program` non-empty; not allowed in `image`.

- [ ] **Step 1:** Write the failing tests: `RoundTrip_PreservesEveryActionType` is extended to all 15 types, plus the five validator tests above.
- [ ] **Step 2:** Run `dotnet test --filter "CatalogJsonTests|CatalogValidatorTests"`. Expected: fail (types not defined).
- [ ] **Step 3:** Add the records, the enum, `[JsonDerivedType]` registrations and the validator rules. Give the new enum the same undefined-value backstop as the others.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass, with the 94 existing tests still green.
- [ ] **Step 5:** Commit with the message `Add uninstall, startup, registry delete, power scheme and logon actions`.

---

### Task 2: Check every identifier and freeze the inventory

**Files:**
- Create: `tools/verify/collect-identifiers.ps1`, `docs/research/identifier-checks.md`
- Modify: `docs/research/switch-inventory.md`

No code tests. The deliverable is a frozen inventory in which every row is marked `VERIFIED` or `DROPPED: <reason>`, with corrected values.

- [ ] **Step 1: Write `tools/verify/collect-identifiers.ps1`.** It must be read-only, with no `Set-`, `Remove-`, `Disable-`, `Stop-` or `New-` cmdlets except `New-Item`/`Out-File` for its own output. It writes one JSON file to `$env:USERPROFILE\NitrOS-identifiers.json` containing:
  - `Get-AppxPackage -AllUsers` (Name, PackageFamilyName)
  - `Get-AppxProvisionedPackage -Online` (DisplayName, PackageName)
  - `Get-Service` (Name, DisplayName, StartType)
  - `Get-ScheduledTask` (TaskPath + TaskName, State)
  - `Get-WindowsOptionalFeature -Online` (FeatureName, State)
  - `Get-WindowsCapability -Online` (Name, State)
  - every uninstall entry under `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`, `HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall` and `HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` (key name, DisplayName, Publisher, QuietUninstallString, UninstallString)
  - the Run keys under HKLM and HKCU, and the files in both Startup folders
  - `powercfg /list` and `powercfg /qh` output (raw text)
  - `[Environment]::OSVersion` and the DisplayVersion registry value

  Wrap each section in try/catch, so a failure records the error text and the script carries on.
- [ ] **Step 2: Hand the elevated run to the controller.** Some sections need admin rights. Report status NEEDS_CONTEXT with the script path and stop. The controller runs it elevated and tells you the output path. Do not try to elevate yourself.
- [ ] **Step 3: Check every row of the inventory** against the output and against primary sources (Microsoft Learn, the vendor's own pages). A row is `VERIFIED` when each identifier it uses is either present on this PC with the exact name, or stated by a primary source, or agreed by both winutil and Winhance in the research files. Otherwise mark it `DROPPED: <which identifier could not be checked>`. Something absent from this PC's build but confirmed by a source stays VERIFIED; the PC tuner treats a missing item as a no-op.
- [ ] **Step 4: Apply these content decisions while freezing** (from the spec and the owner's 2026-10-24 answers):
  - `virtualization-off` cards: off/off/off/on.
  - `windows-update-off` becomes `windows-update-auto-off`: Max Lean only. It stops automatic downloads and installs through policy, and leaves the update service startable. Its description says so.
  - Add `bluetooth-off`: all cards off, risk breaks, breaks sentence `Wireless controllers, headsets and earbuds stop working.`
  - Drop `windows-ai-core-remove` (it needs winutil's EndOfLife method, which v1 does not ship).
  - Drop `oem-reinstall-block` and `wpbt-off` for now (waiting on plan 3, per spec section 11).
  - Drop any row whose only action was a "computed data" token. For `svchost-grouping-on`, use the fixed data `4294967295` (maximum dword, larger than any installed memory) and mark it pending on the cards that say "decided by measurement".
  - Replace every `NEEDS ACTION TYPE:` with the matching Task 1 type and its exact fields, or DROP the row if no Task 1 type fits.
  - Rewrite every `requires` cell to the spec's meaning: the Xbox app switch lists the Store switch and Gaming Services; Game Bar recording lists nothing.
  - Accept the inventory's proposed defaults in its "Card defaults not derived from the spec" table as written, except `virtualization-off` above.
- [ ] **Step 5:** Write `docs/research/identifier-checks.md`: the date, the Windows build, the counts VERIFIED and DROPPED per group, and every DROPPED row with its reason.
- [ ] **Step 6:** Commit with the message `Check catalog identifiers and freeze the switch inventory`.

---

### Tasks 3 to 7: one task per batch of group files

Every content task follows the same steps. Only the groups differ.

| Task | Group files (`catalog/<group>.json`) |
|---|---|
| 3 | `bundled-apps` |
| 4 | `manufacturer-addons`, `edge-onedrive`, `startup` |
| 5 | `windows-ai`, `xbox`, `security-updates` |
| 6 | `big-parts`, `background-services` |
| 7 | `privacy-ads` (extend the existing file; keep its three entries unchanged), `gaming-settings` |

**Files per task:** Create or modify the listed `catalog/<group>.json` files. Create `tests/NitrOS.Core.Tests/Catalog/<Group>Tests.cs`, one per group, with namespace `NitrOS.Core.Tests.Catalog`.

**Interfaces:**
- Consumes: `CatalogLoader.Load(RepoPaths.CatalogDir())`, `Catalog.Get(id)`, and the frozen inventory from Task 2.
- Produces: group files whose `group` value equals the file name and whose switches are exactly the VERIFIED rows of that group's inventory section, with each row's id, name, feature, description, risk, breaks, requires, modes, reversible, skipOn, cards and actions copied exactly.

- [ ] **Step 1:** For each group, write the failing test `<Group>_HasExactlyTheVerifiedIds`. It asserts the set of ids in the loaded group equals the VERIFIED ids, typed out in the test from the inventory section. Also write one test per group, `<Group>_SpotCheck`, asserting the full action list of the group's first VERIFIED row.
- [ ] **Step 2:** Run `dotnet test --filter Catalog`. Expected: fail (file or ids missing).
- [ ] **Step 3:** Write the JSON. Double every backslash. Use kebab-case enum values. Use `HKLM`/`HKCU`.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass, including `RepoCatalog_LoadsWithoutErrors` (any checker error lists file and switch).
- [ ] **Step 5:** Commit with the message `Add <groups> catalog groups`.

---

### Task 8: Cards table and safety promises

**Files:**
- Create: `tests/NitrOS.Core.Tests/Catalog/CardsTableTests.cs`
- Modify: `tests/NitrOS.Core.Tests/RepoCatalogTests.cs` (the Recommended starter test now covers more than the three starter ids; change it to assert those three ids are a subset)

**Interfaces:**
- Consumes: the full catalog; `Selection.FromCard`, `WarningEngine.For`, `DeviceProfile`.

Tests, all against the real catalog:
- `CardsTable_MatchesSpec`: a `[Theory]` with one row per spec cards-table line. Each row gives the inventory switch id that represents it and the expected `CardDefault` for Recommended, Gamer, Streamer and Max Lean, copied from the spec table: on = removed or off, off = kept or unchanged, pending = decided by measurement. Cover at least: one bundled app, a manufacturer add-on, the Edge removal, the OneDrive removal, search indexing, Game Bar background recording, the camera removal, the Store removal, the Xbox app removal, `windows-update-auto-off`, the Defender removal, `memory-integrity-off`, `svchost-grouping-on`, the vendor GPU panel, `virtualization-off`, recovery tools, `printing-off` and `bluetooth-off`.
- `Recommended_KeepsWhatTheSpecKeeps`: in TunePc on `DeviceProfile.Desktop`, Recommended has none of the Edge, OneDrive, search indexing, Store, Xbox app, Windows Update, Defender, printing or Bluetooth switches on.
- `GamerAndStreamer_LeaveAntiCheatPartsAlone`: `memory-integrity-off` and `virtualization-off` are off for Gamer and Streamer.
- `RemovingStoreWhileKeepingXbox_Warns`: Gamer, then toggle the Store removal on. There is a `Dependency` warning whose `RelatedId` is the Xbox app switch.
- `PowerPlan_SkippedOnBattery`: for every card, `new DeviceProfile(true, false, false)` leaves the power-plan switch off.
- `MaxLean_SaysWhatStopsWorking`: in TunePc on a desktop profile, Max Lean's Breaks warnings include the Defender and anti-cheat sentences from the inventory.
- `EveryCard_TurnsSomethingOn`: Recommended, Gamer, Streamer and Max Lean each have a non-empty `On` in TunePc on a desktop profile.

- [ ] **Step 1:** Write the tests.
- [ ] **Step 2:** Run `dotnet test --filter "CardsTableTests|RepoCatalogTests"`. A failure here means a catalog value is wrong. Fix the JSON to match the spec, never the test to match the JSON. If the spec and the frozen inventory disagree, report DONE_WITH_CONCERNS naming the row.
- [ ] **Step 3:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 4:** Commit with the message `Pin cards table and safety promises with tests`.
