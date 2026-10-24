# NitrOS Plan 3: PC Tuner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `NitrOS.Live`, the engine that applies a selection of switches to the running Windows, records every old value, undoes per switch or all, spots switches that Windows turned back on, and checks health after restart. Add a small command-line runner so plan 4's test machine can drive it.

**Architecture:** Every Windows touch point sits behind a small interface (`NitrOS.Live.Systems`). Each action type has one handler that reads the old state, applies the action, and undoes it from the saved old state. `TuneEngine` runs the handlers in catalog order, writes a journal after every action, and rolls back a switch whose actions fail partway. The handlers and engine are tested against in-memory fakes. The real Windows adapters are tested read-only on the owner's PC, or on sandbox targets NitrOS creates and deletes itself. Anything that changes real Windows parts is tested only in the virtual machine (plan 4).

**Tech Stack:** .NET 10 (SDK 10.0.402), C#, `net10.0-windows10.0.19041.0` for the Windows parts (gives `Windows.Management.Deployment.PackageManager`), `Microsoft.Win32.Registry`, P/Invoke for services, restore points and signature checks, `schtasks.exe`, `powercfg.exe` and `dism.exe` for the rest. xUnit v3 as before.

**Spec:** `docs/superpowers/specs/2026-10-24-nitros-design.md` (sections 5, 5.2, 6, 11). Also `docs/research/notes-for-later-plans.md` (Plan 3 section) and `docs/research/identifier-checks.md`.

**This is plan 3 of 7.** Plans 1 (Core) and 2 (catalog) are merged. Later: 4 measurement VM (runs this plan's VM-lane tests), 5 app screens, 6 ISO builder, 7 device updates.

## Global Constraints

- Plan 1 and 2 constraints still hold (SDK pin, Nullable, TreatWarningsAsErrors, xUnit v3 Exe test projects, no `using Xunit;`, namespaces mirror folders).
- **Commits carry no Claude attribution** (owner rule, 2026-10-24): no `Co-Authored-By` trailer, no "Generated with" line. Before committing, `git config user.email` must print `225994527+musika08@users.noreply.github.com`.
- `NitrOS.Core` stays free of Windows APIs. All Windows code lives in `src/NitrOS.Live` (target `net10.0-windows10.0.19041.0`).
- **Nothing in this plan changes the owner's real Windows.** Tests on the owner's PC either only read, or write to sandbox targets NitrOS creates and removes in the same test:
  - registry key `HKCU\Software\NitrOS.Tests`
  - scheduled task folder `\NitrOS.Tests\`
  - files under the test's own temp folder
  - the Run value `NitrOS.Tests.Dummy`

  Tests that would change services, apps, features, capabilities, programs, power plans or system files carry `[Trait("Lane", "Vm")]` and are skipped unless the environment variable `NITROS_VM` is `1`. The CLI's apply and undo refuse to run unless `--yes` is passed. No task may run them on the owner's PC.
- Backups and journals live under `%ProgramData%\NitrOS\backups\`, one JSON file per run, named `yyyyMMdd-HHmmss.json`. Writes are atomic: write `<name>.tmp`, then move it over the target.
- A missing target is a no-op with outcome `Skipped`, never a failure: an app not installed, a service or task that doesn't exist on this build.
- Wildcards in names (`*` only) match case-insensitively. Publisher comparison is case-insensitive (notes-for-later-plans: ASUS spells it two ways).
- `program.uninstall` never runs an uninstaller unless its file's Authenticode signature is valid and its signer subject contains the switch's publisher (case-insensitive).
- Undo restores the saved old value even if Windows has since changed it. Undo of an irreversible action reports it and changes nothing.

## Review Focus

1. A switch whose second action fails: the first action is rolled back, the switch is marked Failed, the engine goes on to the next switch. (Task 6, `Apply_RollsBackSwitchWhenAnActionFails`)
2. Power loss partway: the journal on disk has every action applied so far, so undo works for them. (Task 6, `Apply_PersistsJournalAfterEveryAction`)
3. A target missing on this build: the action is Skipped, the switch counts as applied, and undo of it is a no-op. (Task 6, `Apply_SkipsMissingTargets`)
4. Undo after Windows already changed the value: undo writes the saved old value, no error. (Task 6, `Undo_RestoresOldValueEvenAfterDrift`)
5. An elevated run as a different user than the signed-in one: refused with a reason, because HKCU would be the wrong person's. (Task 9, `SessionGuard_RefusesWhenProcessUserIsNotConsoleUser`)

---

### Task 1: Projects and `Selection.FromIds`

**Files:**
- Create: `src/NitrOS.Live/NitrOS.Live.csproj`, `tests/NitrOS.Live.Tests/NitrOS.Live.Tests.csproj`, `tests/NitrOS.Live.Tests/SmokeTests.cs`
- Modify: `NitrOS.slnx`, `src/NitrOS.Core/Selecting/Selection.cs`, `tests/NitrOS.Core.Tests/Selecting/SelectionTests.cs`

**Interfaces:**
- Produces:
  - Project `NitrOS.Live` with `<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>` (overrides the props default), referencing Core.
  - `NitrOS.Live.Tests`, same framework, referencing Live and Core.
  - In Core: `static Selection FromIds(Catalog catalog, IEnumerable<string> ids, TargetMode mode, DeviceProfile device)`. Unknown ids throw `KeyNotFoundException`; unavailable ids are left off. It also gets `IReadOnlyList<(string Id, string Reason)> Dropped`, listing each unavailable id with the reason `AvailabilityOf` gives.

- [ ] **Step 1:** Write the failing tests in `SelectionTests`:
  - `FromIds_TurnsOnGivenIds`
  - `FromIds_DropsUnavailableWithReason` (an image-only id in TunePc lands in `Dropped` with the existing reason text)
  - `FromIds_UnknownIdThrows`

  Also `LiveSmoke_ReferencesCore` in the Live tests.
- [ ] **Step 2:** Run `dotnet test`. Expected: build fails.
- [ ] **Step 3:** Create the projects, add them to the solution, and implement `FromIds`.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass (199 old tests plus the new ones).
- [ ] **Step 5:** Commit with the message `Add NitrOS.Live projects and Selection.FromIds`.

---

### Task 2: System interfaces, fakes, old-state records and the journal

**Files:**
- Create: `src/NitrOS.Live/Systems/*.cs` (one interface per file), `src/NitrOS.Live/Journal/PriorState.cs`, `src/NitrOS.Live/Journal/RunJournal.cs`, `src/NitrOS.Live/Journal/JournalStore.cs`, `src/NitrOS.Live/Glob.cs`, `tests/NitrOS.Live.Tests/Fakes/*.cs` (one fake per interface), `tests/NitrOS.Live.Tests/Journal/JournalStoreTests.cs`, `tests/NitrOS.Live.Tests/GlobTests.cs`

**Interfaces:**
- Produces, in namespace `NitrOS.Live.Systems`. All names are exact; later tasks call them.
  - `enum LiveHive { LocalMachine, CurrentUser }`, `enum RegView { Default, Registry32 }`
  - `record RegValue(RegistryKind Kind, string Data)`. `Data` uses the catalog's text format; multi-string is newline-separated.
  - `record RegTree(string Key, IReadOnlyList<(string Name, RegValue Value)> Values, IReadOnlyList<RegTree> SubKeys)`
  - `interface IRegistry`:
    - `RegValue? GetValue(LiveHive h, string key, string name, RegView v)`
    - `void SetValue(LiveHive h, string key, string name, RegValue value, RegView v)`
    - `void DeleteValue(LiveHive h, string key, string name, RegView v)`
    - `IReadOnlyList<string> GetValueNames(LiveHive h, string key, RegView v)`
    - `RegTree? ExportKey(LiveHive h, string key, RegView v)`
    - `void ImportKey(LiveHive h, RegTree tree, RegView v)`
    - `void DeleteKey(LiveHive h, string key, RegView v)`
    - `bool KeyExists(LiveHive h, string key, RegView v)`
  - `record ServiceStartInfo(ServiceStartType Start)`; `interface IServiceConfig`:
    - `ServiceStartInfo? Get(string name)`
    - `void Set(string name, ServiceStartType start)`
    - `IReadOnlyList<string> PerUserInstances(string template)`
  - `record TaskInfo(bool Enabled)`; `interface ITaskScheduler`:
    - `TaskInfo? Get(string path)`
    - `void SetEnabled(string path, bool enabled)`
  - `interface IPowerConfig`:
    - `string ActiveScheme()`
    - `IReadOnlyList<string> Schemes()`
    - `(int Ac, int Dc)? Get(string scheme, string subgroup, string setting)`
    - `void SetAc(string scheme, string subgroup, string setting, int value)`
    - `void SetDc(string scheme, string subgroup, string setting, int value)`
    - `void SetActive(string scheme)`
  - `interface IAppx`:
    - `bool IsInstalled(string name)`
    - `bool IsProvisioned(string name)`
    - `void RemoveForAllUsers(string name)`
    - `void Deprovision(string name)`

    Names may carry `*`.
  - `enum DismOutcome { Done, RebootRequired, Deferred, NotPresent, Failed }`; `interface IDism`:
    - `string? FeatureState(string name)`
    - `DismOutcome DisableFeature(string name)`
    - `DismOutcome EnableFeature(string name)`
    - `string? CapabilityState(string name)`
    - `DismOutcome RemoveCapability(string name)`
  - `record UninstallEntry(string KeyPath, string DisplayName, string Publisher, string? Version, string? QuietUninstallString, string? UninstallString)`; `interface IUninstaller`:
    - `IReadOnlyList<UninstallEntry> Find(string displayNamePattern, string publisher)`
    - `bool SignerMatches(UninstallEntry entry, string publisher)`
    - `int Run(UninstallEntry entry, string? args)`, which returns the exit code
  - `interface IFileSystem`:
    - `bool Exists(string path)`
    - `void TakeOwnershipAndDelete(string path)`
  - `interface IScriptRunner { int Run(string body); }`
  - `enum RestorePointOutcome { Created, SkippedRecent, Unavailable }`; `interface IRestorePoint { RestorePointOutcome Create(string description); }`
  - `record LiveSystems(IRegistry Registry, IServiceConfig Services, ITaskScheduler Tasks, IPowerConfig Power, IAppx Appx, IDism Dism, IUninstaller Uninstaller, IFileSystem Files, IScriptRunner Scripts, IRestorePoint RestorePoint)`
- Produces, in namespace `NitrOS.Live.Journal`:
  - `abstract record PriorState`, polymorphic on `kind`, with:
    - `RegistryPrior(RegValue? Value)`
    - `RegistryTreePrior(RegTree? Tree, RegValue? Value)`
    - `ServicePrior(IReadOnlyList<(string Name, ServiceStartType Start)> Services)`
    - `TaskPrior(bool Enabled)`
    - `StartupPrior(IReadOnlyList<(string Location, string Data)> Entries)`
    - `PowerPrior(string Scheme, int? Ac, int? Dc)`
    - `SchemePrior(string PreviousScheme)`
    - `LaunchPrior(RegValue? Value)`
    - `MissingPrior()`: the target was absent, so the action was skipped
    - `IrreversiblePrior(string What, string? Version, string ReinstallHint)`
    - `FeaturePrior(string? State)`
  - `enum ActionOutcome { Applied, Skipped, Failed, RolledBack }`
  - `record JournalEntry(string SwitchId, int ActionIndex, SwitchAction Action, PriorState Prior, ActionOutcome Outcome, string? Detail, DateTimeOffset At)`
  - `record RunJournal(string RunId, string UserSid, string WindowsBuild, IReadOnlyList<string> SwitchIds, IReadOnlyList<JournalEntry> Entries)`
  - `sealed class JournalStore(string directory)`:
    - `string Save(RunJournal journal)`: atomic, as in Global Constraints; returns the path
    - `RunJournal Load(string path)`
    - `IReadOnlyList<string> List()`: newest first
  - `static class Glob { bool IsMatch(string pattern, string text); }`: `*` only, case-insensitive.
- Fakes: in-memory implementations with public state so tests can assert, for example `FakeRegistry.Values` and `FakeServices.Starts`. They throw `InvalidOperationException` when a test sets `FailOn` to a target name.

- [ ] **Step 1:** Write the failing tests:
  - `JournalStore_RoundTripsEveryPriorKind`
  - `JournalStore_SaveIsAtomic`: a `.tmp` file left by a simulated crash is ignored by `List`, and `Save` overwrites it.
  - `JournalStore_ListNewestFirst`
  - `Glob_MatchesStarAndIgnoresCase`, with cases `MicrosoftEdgeAutoLaunch_*` against `MicrosoftEdgeAutoLaunch_96AC5A...` (true), `Foo*` against `foo` (true) and `a*b` against `ac` (false).
- [ ] **Step 2:** Run `dotnet test --filter "JournalStore|Glob"`. Expected: build fails.
- [ ] **Step 3:** Implement. Journal JSON uses Core's `CatalogJson.Options` settings plus the `PriorState` polymorphism.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit with the message `Add tuner system interfaces, fakes and run journal`.

---

### Tasks 3 to 5: action handlers

Every handler implements one interface. Handlers live in `src/NitrOS.Live/Handlers/`; tests in `tests/NitrOS.Live.Tests/Handlers/`. All tests use the fakes.

```csharp
public interface IActionHandler
{
    PriorState Read(SwitchAction action, LiveSystems s);           // capture before change; MissingPrior if absent
    ActionOutcome Apply(SwitchAction action, LiveSystems s);       // Skipped when the target is absent
    void Undo(SwitchAction action, PriorState prior, LiveSystems s); // restore exactly prior; no-op for MissingPrior
}
```

Also `static class Handlers { IActionHandler For(SwitchAction action); }`, a type switch over all 15 action types; unknown types throw.

For every handler, write and pass these tests first: `<Type>_ReadsPrior`, `<Type>_Applies`, `<Type>_UndoRestoresPrior`, and `<Type>_MissingTargetIsSkipped` wherever a target can be absent. Each task commits once.

| Task | Handlers | Rules beyond the four tests |
|---|---|---|
| 3 | `registry.set`, `registry.delete`, `startup.disable`, `launch.at-logon` | **Registry hives.** `registry.set` maps HKLM/HKCU to `LiveHive`; undo of a value that didn't exist deletes it. `registry.delete` with a name stores the value; without one, it stores the `ExportKey` tree, and undo calls `ImportKey`. **`startup.disable`**, using the formats observed on the owner's PC on 2026-10-25: `run` sets the first byte of `...\Explorer\StartupApproved\Run` (or `Run32` when the hive is HKLM and the Run value is in the 32-bit view) to `03`, enabled being `02`, and writes the full value as `03 00 00 00 00 00 00 00 00 00 00 00` when absent. It never touches entries whose first byte is anything else, such as `06`. `startup-folder` uses the `StartupApproved\StartupFolder` key the same way. `appx-task` sets `State` (dword) to `1` (DisabledByUser) under `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\<PFN>\<TaskId>`, where the action's name is `<PFN>!<TaskId>`. `task` delegates to the scheduler. Names may carry `*`; every match is changed and every old value saved in `StartupPrior`. **`launch.at-logon`** writes the HKCU Run value `NitrOS.LaunchAtLogon` = `"<program>" <args>` (program path expanded), and only when the program file exists; otherwise it is Skipped. |
| 4 | `service.start`, `task.disable`, `power.set`, `power.scheme` | **`service.start` on a per-user template** (`CDPUserSvc`, `BluetoothUserService`): it also sets every `PerUserInstances` match, and `ServicePrior` stores all of them. **`power.set`** writes to the scheme that is active at Apply time, and `PowerPrior` records which scheme. **`power.scheme`** is Skipped with detail `not available on this PC` when the GUID isn't in `Schemes()`. It never creates a scheme. **The engine (Task 6) orders `power.scheme` before `power.set` within a run.** |
| 5 | `appx.remove`, `capability.remove`, `feature.disable`, `file.remove`, `program.uninstall`, `script` | **`appx.remove`** calls `RemoveForAllUsers`, then `Deprovision`; `IrreversiblePrior` holds the name and the hint `Reinstall from the Microsoft Store`. **`feature.disable`** stores the state; undo re-enables it with `IDism.EnableFeature`. `Deferred` and `RebootRequired` count as Applied, with the detail saying a restart is needed. **`capability.remove` and `file.remove`** are irreversible; `file.remove` expands environment variables. **`program.uninstall`:** `Find` gives zero entries → Skipped. It runs every match, and refuses any whose `SignerMatches` is false (Failed with detail `signature check failed`). The uninstall string is `QuietUninstallString`, falling back to `UninstallString`, with `args` appended after a space. Exit code 0 or 3010 → Applied; anything else → Failed with the code. **`script`** runs the body; undo runs `UndoBody` if present, otherwise it is irreversible. |

---

### Task 6: Tune engine, rollback, undo and drift

**Files:**
- Create: `src/NitrOS.Live/TuneEngine.cs`, `src/NitrOS.Live/DriftChecker.cs`, `tests/NitrOS.Live.Tests/TuneEngineTests.cs`, `tests/NitrOS.Live.Tests/DriftCheckerTests.cs`

**Interfaces:**
- Consumes: `Handlers.For`, `JournalStore`, `LiveSystems`, Core's `Selection` and `Catalog`.
- Produces:
  - `record TuneOptions(bool AllowWithoutRestorePoint, string UserSid, string WindowsBuild)`
  - `enum SwitchResult { Applied, PartlyApplied, Failed, Skipped }` (Skipped means every action was skipped)
  - `record TuneProgress(string SwitchId, int Done, int Total)`
  - `record TuneResult(string JournalPath, RestorePointOutcome RestorePoint, IReadOnlyDictionary<string, SwitchResult> Switches, bool Refused, string? RefusedReason)`
  - `sealed class TuneEngine(LiveSystems systems, JournalStore store)` with:
    - `TuneResult Apply(Selection selection, TuneOptions options, IProgress<TuneProgress>? progress, CancellationToken ct)`
    - `UndoResult Undo(string journalPath, IReadOnlyCollection<string>? switchIds)`: null means every switch
  - `record UndoResult(IReadOnlyList<string> Undone, IReadOnlyList<(string SwitchId, string Reason)> CannotUndo)`
  - `sealed class DriftChecker(LiveSystems systems)` with `IReadOnlyList<string> Check(RunJournal journal)`. It returns the switch ids where any Applied, reversible action no longer holds its applied value. For each action type, "holds" means the handler's `Read` would now differ from the state the action sets. Implement `bool IsInEffect(SwitchAction action, LiveSystems s)` on each handler (extend the interface).

Engine rules:
- Create the restore point first. `Unavailable` and `!AllowWithoutRestorePoint` → Refused, with the reason `System Restore is off on this PC.` Nothing else changes. `SkippedRecent` → proceed.
- Order: catalog switch order, but every switch that has `power.scheme` runs before every switch that has `power.set`. Within a switch, action order.
- Per action: `Read`, append a `JournalEntry` and **save the journal**, then `Apply`, then update the entry's outcome and save again.
- If an action throws or returns Failed, undo that switch's already-applied actions in reverse order, mark their entries RolledBack, set the switch to Failed, and continue with the next switch.
- Cancellation stops between actions; the journal stays valid.
- Undo: reverse journal order. Applied reversible entries are undone; Skipped entries are a no-op. Irreversible entries go into `CannotUndo` with their hint, for example `Reinstall from the Microsoft Store`. The journal is saved with the undone entries marked.

- [ ] **Step 1:** Write the failing tests. The five Review Focus items:
  - `Apply_RollsBackSwitchWhenAnActionFails`
  - `Apply_PersistsJournalAfterEveryAction` (a fake store counting saves, so a crash after action N leaves N entries)
  - `Apply_SkipsMissingTargets`
  - `Undo_RestoresOldValueEvenAfterDrift`
  - `Undo_ReportsIrreversible`

  Plus:
  - `Apply_RefusesWithoutRestorePointUnlessAllowed`
  - `Apply_OrdersPowerSchemeBeforePowerSet`
  - `Apply_CancellationLeavesValidJournal`
  - `Drift_FindsSwitchWindowsTurnedBack`
  - `Drift_IgnoresSkippedAndIrreversible`
- [ ] **Step 2:** Run `dotnet test --filter "TuneEngine|Drift"`. Expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit with the message `Add tune engine with rollback, undo and drift check`.

---

### Task 7: Real adapters A (registry, start-up, tasks, power)

**Files:**
- Create: `src/NitrOS.Live/Windows/WinRegistry.cs`, `WinTaskScheduler.cs`, `WinPowerConfig.cs`, `ProcessRunner.cs`, `tests/NitrOS.Live.Tests/Windows/*Tests.cs`

**Interfaces:** `WinRegistry : IRegistry`, using `Microsoft.Win32.RegistryKey.OpenBaseKey` with `RegistryView.Registry64` or `Registry32`. `WinTaskScheduler : ITaskScheduler` runs `schtasks.exe /Query /TN <path> /FO CSV /NH` and `schtasks.exe /Change /TN <path> /DISABLE|/ENABLE`. `WinPowerConfig : IPowerConfig` parses `powercfg /getactivescheme`, `/list` and `/q <scheme> <sub> <setting>` (the "Current AC/DC Power Setting Index" lines, hex), and runs `/setacvalueindex`, `/setdcvalueindex` and `/setactive`. `ProcessRunner.Run(string file, string args, TimeSpan timeout) -> (int ExitCode, string StdOut, string StdErr)`; it kills the process at the timeout.

Tests on the owner's PC follow the Global Constraints sandbox rules exactly:
- `WinRegistry_RoundTripsEveryKindUnderSandboxKey`, `WinRegistry_ExportImportTree` and `WinRegistry_Registry32View`. Each works under `HKCU\Software\NitrOS.Tests\<guid>` and deletes it in `Dispose`.
- `WinTaskScheduler_TogglesSandboxTask`: create `\NitrOS.Tests\Dummy` with `schtasks /Create /TN "\NitrOS.Tests\Dummy" /TR "cmd /c exit" /SC ONCE /ST 00:00 /F`, toggle it, then delete it in `Dispose`.
- `WinPowerConfig_ReadsActiveSchemeAndOneSetting`: read-only, against the active scheme and the USB selective suspend GUIDs from `catalog/gaming-settings.json`.
- `[Trait("Lane","Vm")]` tests for SetAc/SetDc/SetActive.

- [ ] Steps: tests first, then implement, run `dotnet test` (VM lane skipped), and commit with the message `Add Windows adapters for registry, tasks and power`.

---

### Task 8: Real adapters B (services, apps, DISM, uninstall, files, restore point, script)

**Files:**
- Create: `src/NitrOS.Live/Windows/WinServiceConfig.cs`, `WinAppx.cs`, `WinDism.cs`, `WinUninstaller.cs`, `WinFileSystem.cs`, `WinRestorePoint.cs`, `WinScriptRunner.cs`, `NativeMethods.cs`, `WinSystems.cs` (`static LiveSystems Create()`), plus tests.

**Decisions:**
- **Services:** read the start type from `HKLM\SYSTEM\CurrentControlSet\Services\<name>` (`Start`, `DelayedAutostart`). Set it with `ChangeServiceConfigW`, using `SERVICE_NO_CHANGE` for every field except the start type, plus `ChangeServiceConfig2W(SERVICE_CONFIG_DELAYED_AUTO_START_INFO)` for automatic-delayed. Per-user instances are the services named `<template>_<suffix>`, enumerated from `HKLM\SYSTEM\CurrentControlSet\Services`.
- **Apps:** `Windows.Management.Deployment.PackageManager`, with `FindPackages()` filtered by name glob; `RemovePackageAsync(fullName, RemovalOptions.RemoveForAllUsers)`; `DeprovisionPackageForAllUsersAsync(familyName)`. Wait for each with a 5-minute timeout.
- **DISM:** `dism.exe /English /Online /Get-FeatureInfo|/Disable-Feature|/Enable-Feature /FeatureName:<n> /NoRestart` and `/Get-CapabilityInfo|/Remove-Capability /CapabilityName:<n>`. Exit 3010 → RebootRequired. Output containing "Disable Pending" or "Uninstall Pending" → Deferred. "not found" → NotPresent. Access denied → Failed with the detail `DISM access denied (see notes-for-later-plans)`.
- **Uninstall:** read the three Uninstall roots, both views. `SignerMatches` extracts the executable path from the uninstall string, which may be quoted or `MsiExec.exe /X{...}`. MsiExec counts as a Microsoft signer, checked through the MSI product's publisher. It verifies with `WinVerifyTrust` (`WINTRUST_ACTION_GENERIC_VERIFY_V2`) and compares the signer certificate's subject to the publisher.
- **Files:** `takeown.exe /F <path> /R /D Y` and `icacls.exe <path> /grant *S-1-5-32-544:F /T /C`, then delete.
- **Restore point:** `SRSetRestorePointW` with `APPLICATION_INSTALL`/`BEGIN_SYSTEM_CHANGE`, then `END_SYSTEM_CHANGE`. `ERROR_SERVICE_DISABLED` → Unavailable. A point created within 24 hours (Windows' own limit) shows as success without a new point; treat that as SkippedRecent by comparing the newest restore point time, read through WMI `root\default:SystemRestore`. Never change Windows' frequency setting.
- **Script:** write the body to a temp `.ps1` and run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`, with a 10-minute timeout.

Tests:
- On the owner's PC, read-only only:
  - `WinServiceConfig_ReadsKnownService` (`Audiosrv`)
  - `WinServiceConfig_FindsPerUserInstances` (`CDPUserSvc` has at least one instance)
  - `WinAppx_FindsInstalledPackage` (`Microsoft.WindowsCalculator` or any installed package)
  - `WinUninstaller_FindsSteamEntry`, skipped when Steam isn't installed
  - `WinUninstaller_SignerMatchesSteam`
  - `WinDism_ReportsFeatureState` (`Printing-PrintToPDFServices-Features`, or report access denied)
- Everything that changes something: `[Trait("Lane","Vm")]`.

- [ ] Steps: tests first, then implement, run `dotnet test` (VM lane skipped), and commit with the message `Add Windows adapters for services, apps, DISM, uninstall, files and restore points`.

---

### Task 9: Device detection, session guard, health check

**Files:**
- Create: `src/NitrOS.Live/DeviceDetector.cs`, `SessionGuard.cs`, `HealthCheck.cs`, `Windows/WinHealthProbe.cs`, plus tests.

**Interfaces:**
- `static DeviceProfile DeviceDetector.Detect(IDeviceFacts facts)` with `interface IDeviceFacts { bool HasBattery { get; } int MaxTouches { get; } string Manufacturer { get; } string Model { get; } }`:
  - battery = `HasBattery`
  - touchscreen = `MaxTouches > 0`
  - handheld = `HasBattery`, `MaxTouches > 0` and the model is in the known list `RC71L`, `RC72LA`, `RC73XA`, `RC73YA`, matched by prefix, case-insensitive.

  `WinDeviceFacts`: `GetSystemPowerStatus` (`BatteryFlag` 128 means no battery), `GetSystemMetrics(SM_MAXIMUMTOUCHES = 95)`, and `Win32_ComputerSystem` Manufacturer/Model.
- `static SessionCheck SessionGuard.Check(ISessionFacts facts)` with `ISessionFacts { string ProcessUserSid { get; } string? ConsoleUserSid { get; } bool IsElevated { get; } int WindowsBuild { get; } }`, returning `record SessionCheck(bool Ok, string? Reason)`. Reasons, checked in this order:
  - build below 26100 (spec 6: Windows 11 24H2 and newer only) → `NitrOS needs Windows 11 24H2 or newer.`
  - not elevated → `NitrOS needs administrator rights.`
  - no console user → `No one is signed in at this PC.`
  - SIDs differ → `NitrOS is running as a different user than the one signed in, so it would change the wrong account's settings.`

  `WinSessionFacts`: the console user comes from the owner of `explorer.exe` in `WTSGetActiveConsoleSessionId()`.
- `HealthCheck.Run(IHealthProbe probe, IReadOnlySet<string> keptFeatures) -> HealthReport` with `record HealthItem(string Name, bool Ok, string? LikelySwitchId)`. The checks:
  - Start menu: `StartMenuExperienceHost` running after sign-in
  - internet: HTTP GET `http://www.msftconnecttest.com/connecttest.txt` returns `Microsoft Connect Test`
  - sound: `Audiosrv` running
  - Bluetooth: `bthserv` running, unless `bluetooth-off` is on
  - Store: `Microsoft.WindowsStore` installed, if kept
  - Game Pass: `Microsoft.GamingApp` and the `GamingServices` service, if kept

  Each failing item names the most likely switch from a fixed map: Start → `windows-ads-off`, Store → `store-remove`, sound → none, and so on.

Tests use fake facts and probes:
- `Detect_AllyXIsHandheldBatteryTouch`
- `Detect_DesktopHasNoTraits`
- `SessionGuard_RefusesWhenProcessUserIsNotConsoleUser`
- `SessionGuard_RefusesWhenNotElevated`
- `SessionGuard_RefusesOldWindows` (build 22631 is refused; 26100 passes)
- `Health_SkipsKeptChecksForRemovedFeatures`
- `Health_NamesLikelySwitch`

One read-only real test: `WinDeviceFacts_OnThisPcIsAllyX`. It asserts handheld when the model starts with `RC73XA`; otherwise it only checks no exception is thrown.

- [ ] Steps: tests first, then implement, run `dotnet test`, and commit with the message `Add device detection, session guard and health check`.

---

### Task 10: Command-line runner

**Files:**
- Create: `src/NitrOS.Cli/NitrOS.Cli.csproj` (console app, `net10.0-windows10.0.19041.0`, `app.manifest` with `requireAdministrator`), `src/NitrOS.Cli/Program.cs`, `tests/NitrOS.Live.Tests/CliTests.cs` (calls the testable entry point `CliApp.Run(string[] args, TextWriter output, LiveSystems systems, ...)` against fakes)

Commands (exact):
- `nitros detect`: prints the device profile.
- `nitros plan --card <recommended|gamer|streamer|max-lean> [--ids a,b]`: a dry run. It prints every switch and action that would run, plus the dropped ids and their reasons. It changes nothing.
- `nitros apply --card <...> [--ids ...] --yes [--allow-without-restore-point]`: refuses without `--yes`. Runs the session guard first.
- `nitros undo <journal> [--ids a,b] --yes`
- `nitros drift <journal>`
- `nitros health`
- `nitros journals`

Exit codes: 0 OK, 1 refused or partly failed, 2 bad arguments. The catalog folder is found next to the executable (copied at build), or given with `--catalog <dir>`.

Tests:
- `Plan_PrintsActionsAndChangesNothing`: fake systems record no writes.
- `Apply_RefusesWithoutYes`
- `Apply_RefusesWhenSessionGuardFails`
- `Undo_RefusesWithoutYes`
- `BadArguments_Exit2`

One real, read-only test, `Plan_OnThisPcAgainstRealCatalog`, runs `plan --card gamer` with the real adapters, which only read in `plan`, and asserts exit code 0.

- [ ] Steps: tests first, then implement, run `dotnet test`, and commit with the message `Add nitros command-line runner`.

---

### Task 11: Open questions from spec section 11 (research only, no code)

**Files:**
- Create: `docs/research/reinstall-block.md`, `docs/research/dism-access-denied.md`
- Modify: `docs/superpowers/specs/2026-10-24-nitros-design.md` (section 11: mark what was answered and link the notes), `docs/research/notes-for-later-plans.md`

- [ ] **Step 1: Reinstall block.** Find, from Microsoft Learn and vendor pages, how maker apps such as Armoury Crate come back after removal: Windows Update driver packages with companion apps (the "software component" in driver packages), the Windows Platform Binary Table, ASUS's BIOS setting for Armoury Crate download, the device metadata and Store auto-install. Check what controls each one while still letting drivers and firmware update, and what is documented. Read-only checks on this PC are allowed: installed driver packages via `pnputil /enum-drivers`, and the PnP device list. Write the findings with sources and dates. If a documented, reversible block exists that fits an existing action type, propose its catalog entry in the doc; do not add it to the catalog.
- [ ] **Step 2: DISM access denied.** Read-only. Explain why `dism /Online /Get-ProvisionedAppxPackages` and `Get-AppxProvisionedPackage -Online` are denied even when elevated on this PC, while features and capabilities work. Look at the DISM log (`C:\Windows\Logs\DISM\dism.log`; reading it may need the controller's elevated run, so ask, as plan 2 did) and the services and filter drivers of third-party software (Shadow Defender). State the likely cause and what NitrOS must do about it (detect it and tell the user, for example), with evidence.
- [ ] **Step 3:** Update spec section 11 and the notes file. Commit with the message `Research reinstall block and DISM access denied`.
