# NitrOS Plan 4: Measurement Virtual Machine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `measure\` part: scripts that create and drive a Hyper-V test machine on the owner's PC, a resumable loop that measures every switch and every card exactly as spec 7 says, and a tested tool that writes the numbers into the catalog's `measured` fields and `cards.json`. Use the same machine to run plan 3's VM-lane tests, prove Tune mode's "Undo everything" and health check, settle every Plan 4 item in the notes, prove the "Add Xbox back" method, and decide "Fewer service processes" for Gamer and Streamer from the numbers.

**Architecture:** Hyper-V work is PowerShell 5.1 (the Hyper-V module is PowerShell-only) in `measure\scripts\`. Every decision those scripts make is in C#, so it can be unit-tested: a library `NitrOS.Measure` (statistics, campaign plan, resume state, results calculation, catalog writer, report, guest sampler and state fingerprint), a host console tool `nitros-measure` that the scripts call, and a windowless guest tool `nitros-probe` that samples inside the test machine without leaving a console process behind. Files go into the test machine and results come out over PowerShell Direct. While the machine idles and samples, the host talks to it only through Hyper-V's key-value exchange, which starts no process in the guest. Each run starts from the same clean checkpoint, so an interrupted run is simply run again.

**Tech Stack:** .NET 10 (SDK 10.0.402), C#, xUnit v3 as before. Windows PowerShell 5.1 with the in-box Hyper-V module on the host. PowerShell Direct (`New-PSSession -VMName`, `Copy-Item -ToSession/-FromSession`). IMAPI2 (`IMAPI2FS.MsftFileSystemImage`, built into Windows, present on this PC) to write the small answer-file ISO. No Pester: the only Pester on this PC is the in-box 3.4.0 (checked read-only 2026-10-28), and installing a newer one would change the owner's PC, so all testable logic is C#.

**Spec:** `docs/superpowers/specs/2026-10-24-nitros-design.md` (sections 3, 5, 6, 7, 8, 11). Also `docs/research/notes-for-later-plans.md` (Plan 4 section), `docs/research/dism-access-denied.md` and `docs/research/reinstall-block.md`.

**This is plan 4 of 7.** Plans 1 (Core), 2 (catalog) and 3 (PC tuner) are merged. Later: 5 app screens (shows the numbers and `MemoryFacts` from this plan, and calls the Add Xbox back method), 6 ISO builder (reuses `New-MeasureVm.ps1` and `Invoke-MeasureRun.ps1`, see "Interface for plan 6"), 7 device updates.

## Global Constraints

- Plans 1 to 3 constraints still hold: SDK pin, Nullable, TreatWarningsAsErrors, xUnit v3 test projects as Exe, no `using Xunit;`, namespaces mirror folders, `NitrOS.Core` free of Windows APIs, atomic writes as `<name>.tmp` then move.
- **Commits carry no Claude attribution** (owner rule, 2026-10-24): no `Co-Authored-By` trailer, no "Generated with" line. Before committing, `git config user.email` must print `225994527+musika08@users.noreply.github.com`.
- **Nothing in tests or tasks changes the owner's real Windows** (the ASUS ROG Xbox Ally X, Windows 11 Pro, build 26300, which is also the Hyper-V host). Tests on the owner's PC only read, or write to sandbox targets they create and remove themselves (temp folders, `HKCU\Software\NitrOS.Tests`). Anything that changes a system carries `[Trait("Lane","Vm")]` and `Assert.SkipUnless(Environment.GetEnvironmentVariable("NITROS_VM") == "1", ...)`, and runs only inside the test machine.
- **OWNER CHECKPOINT** marks every step that needs admin on the owner's PC: turning on Hyper-V, creating the test machine, every script in `measure\scripts\` that drives Hyper-V, and anything on the Ally itself. The controller runs it through a UAC-elevated `.ps1` that writes its output to `%USERPROFILE%\NitrOS-<step>.txt`, and only after the owner says yes in chat. A restart of the owner's PC needs the owner's explicit OK first.
- Never change Defender settings on the owner's PC. Never change permissions (ACLs) on the owner's PC. Never change Shadow Defender's settings, services or drivers.
- The owner supplies the Windows 11 ISO path. No ISO path is ever hardcoded, in code, scripts, tests or docs.
- The test account's password is generated, stored only in the git-ignored `measure\measure.local.json` (protected with the owner's Windows data protection), and never printed, logged, put on a command line, written to chat or committed. It reaches child processes only through the `NITROS_MEASURE_PASSWORD` environment variable of that child.
- `measured` fields and `cards.json` are written only by `nitros-measure results --write`. Never by hand. After every write the strict loader (`CatalogLoader.Load`) must still load the catalog with no errors.
- Plain English, short sentences, no em-dashes, no emojis, in code comments and docs too.
- Facts marked "verify first" in a task are checked in that task before code depends on them, and the check (source URL and date, or the command and its output) goes in the task's commit message or research note.

## Review Focus

1. The middle value really is the middle: five samples, sorted, index 2, memory and processes each on their own. (Task 1, `Median_OfFiveIsTheThirdSorted`, `Summary_TakesMediansIndependently`)
2. A measurement inside the noise is never shown as a saving. (Task 1, `Savings_InsideNoiseBandIsZero`)
3. A run interrupted at any point is run again from the clean checkpoint, never counted twice and never lost. (Task 2, `Next_ReturnsInterruptedRunAgain`, `Complete_IsIdempotent`)
4. The writer changes only `measured` fields and `cards.json`, all or nothing, and the strict loader still passes. (Task 3, `Writer_LeavesEveryOtherByteUnchanged`, `Writer_RollsBackWhenLoaderFails`)
5. A switch that did nothing in the test machine (every action Skipped), failed, or changed since it was measured gets no number. (Task 3, `Compute_NoNumberForSkippedFailedOrChanged`)
6. The sampler never counts itself, and a run where the guest slept is thrown away. (Task 4, `Sampler_ExcludesOwnProcessAndIdle`, `Sampler_FlagsSleep`)
7. Undo everything leaves every reversible target exactly as in the clean checkpoint. (Task 9, `Compare_ReportsOnlyReversibleDifferences` and the VM run)

## Definitions pinned by this plan

- **Memory in use** = `ullTotalPhys - ullAvailPhys` from `GlobalMemoryStatusEx`, in MiB (bytes / 1,048,576, rounded down). Why: Microsoft defines `ullAvailPhys` as the standby, free and zero lists, so this is exactly the memory Windows cannot hand to a game without writing something to disk first, and the instantly reusable file cache is left out.
- **Process count** = every process `Process.GetProcesses()` returns, except the Idle entry (PID 0) and the sampler itself. `System`, `Registry`, `Secure System` and `Memory Compression` count. The sampler also records the `svchost` count (for the "Fewer service processes" decision).
- **Memory available to Windows** = `ullTotalPhys`. **Installed memory** = `GetPhysicallyInstalledSystemMemory` (from the firmware tables). **Reserved for hardware** = installed minus available. On the owner's Ally X (24 GB installed) Windows sees about 16 GB, because the rest is reserved, mostly for the GPU. Task 4 adds these to `nitros detect`; plan 5 shows them on the Done screen and cards.
- **One run** (spec 7): restore the clean checkpoint, start, apply the run's switch ids with the CLI (nothing for a baseline run), restart, wait for sign-in, idle 10 minutes, take 5 samples 30 seconds apart, keep the middle value of each measure.
- **Baseline** = the median of three baseline runs taken before any switch run. **Noise band** = the largest minus the smallest of those three, per measure. A fourth baseline run at the end only checks for drift.
- **Saving** = baseline minus the run (positive means less memory or fewer processes). If its size is inside the noise band, the saving is written as 0. A negative saving outside the band is written as measured and flagged in the report.
- **Test machine device profile:** Desktop (no battery, no touchscreen). Card runs use `Selection.FromCard(..., TargetMode.TunePc, DeviceProfile.Desktop)`. So card numbers include `high-performance-plan`, which a handheld skips; that plan has no memory effect, which the report states.

## Run time estimate

Runs: 3 baselines + 109 switches with a live mode (110 minus the image-only `recovery-remove`) + 4 cards + 2 variants (Gamer and Streamer with `svchost-grouping-on`) + 1 final baseline = **119 runs**.

Per run: restore 0.5 min, boot and sign-in 2, copy and apply 1 to 6 (most switches about 1.5; feature removals and the Edge uninstall the longest), restart 2, idle 10, sample 2, collect and shut down 1. That is 18.5 to 23.5 minutes, about **20 minutes** on average.

Total about **40 hours** (range 37 to 47), plus about 1 hour to install the test machine, 3 hours for the Undo everything runs, and about 8 hours of investigations. The Ally must stay on the charger with about 5 GB of memory free on the host for the whole time. The loop resumes after any interruption (Task 2, Task 14).

## Interface for plan 6

Plan 6 builds an ISO per card and installs it in a test machine. It calls these two scripts, which stay stable:

- `measure\scripts\New-MeasureVm.ps1 -IsoPath <iso> -VmName <name> [-CheckpointName clean] [-ConfigPath measure\measure.local.json] [-ImageIndex <n>] [-SettleMinutes 30]`. Creates a Generation 2 machine (4 GB static memory, 4 cores, virtual TPM, Secure Boot, 64 GB disk, network adapter present and disconnected), installs Windows unattended with the test account, prepares it, shuts it down and takes the checkpoint. It refuses an ISO older than 24H2 or without a Pro edition. It writes `measure\work\<VmName>.vm.json`: `{ "vmName", "checkpoint", "windowsBuild", "edition", "imageIndex", "createdAt" }`, and prints that path. Exit 0 on success, 1 on any failure (with the reason on the last line).
- `measure\scripts\Invoke-MeasureRun.ps1 -VmName <name> -Checkpoint <name> -RunId <id> -OutDir <dir> [-Ids <id,...>] [-Network Off|On] [-SkipApply]`. One run as defined above. Writes `<OutDir>\<RunId>.result.json` (the `RunResult` record from Task 1) and `<OutDir>\<RunId>.journal.json` when switches were applied, and prints the result path. Exit 0 measured, 1 failed (reason on the last line), 3 the guest slept or the host was not ready (try again later).

Plan 6 measures an installed slim ISO by calling `Invoke-MeasureRun.ps1 -SkipApply` on its own checkpoint, and subtracts this plan's baseline with `nitros-measure`.

---

### Task 1: Measure projects, run records and statistics

**Files:**
- Create: `src/NitrOS.Measure/NitrOS.Measure.csproj` (class library, `net10.0-windows10.0.19041.0`, references Core and Live), `src/NitrOS.Measure/Runs.cs`, `src/NitrOS.Measure/Stats.cs`, `src/NitrOS.Measure/BaselineCalc.cs`, `src/NitrOS.Measure/Savings.cs`, `tests/NitrOS.Measure.Tests/NitrOS.Measure.Tests.csproj`, `tests/NitrOS.Measure.Tests/StatsTests.cs`, `tests/NitrOS.Measure.Tests/SavingsTests.cs`
- Modify: `NitrOS.slnx`, `.gitignore` (add `measure/measure.local.json` and `measure/work/`)

**Interfaces:**
- Consumes: Core's `Measurement(int MemoryMb, int Processes, string WindowsBuild, DateOnly Date)`.
- Produces, namespace `NitrOS.Measure`:
  - `record MemorySample(DateTimeOffset At, int InUseMb, int Processes, int Svchost)`
  - `record RunSummary(int InUseMb, int Processes, int Svchost)`
  - `enum RunKind { Baseline, Switch, Card, Variant }`
  - `record RunSpec(string RunId, RunKind Kind, IReadOnlyList<string> Ids, string? Card, string DefinitionHash)`
  - `record RunResult(string RunId, string WindowsBuild, int VisibleMb, IReadOnlyList<MemorySample> Samples, RunSummary Median, int? ApplyExitCode, IReadOnlyDictionary<string, string> SwitchResults, bool Slept, DateTimeOffset Started, DateTimeOffset Finished)`. `SwitchResults` maps switch id to the `SwitchResult` name from the run's journal.
  - `static class Stats`: `int Median(IReadOnlyList<int> values)` (odd count only, else `ArgumentException`); `RunSummary Summarize(IReadOnlyList<MemorySample> samples)` (exactly 5, else `ArgumentException`).
  - `record Baseline(RunSummary Value, int MemoryNoiseMb, int ProcessNoise)`; `static class BaselineCalc { Baseline From(IReadOnlyList<RunResult> baselineRuns); }`, needing at least 3 runs, all with the same `WindowsBuild` and none `Slept`, else `InvalidOperationException` naming the problem.
  - `static class Savings { Measurement For(RunResult run, Baseline baseline, DateOnly date); bool IsFlagged(RunResult run, Baseline baseline); }`, with the rules in "Definitions pinned by this plan".
- JSON for all records uses `CatalogJson.Options`.

- [ ] **Step 1:** Write the failing tests:
  - `Median_OfFiveIsTheThirdSorted` (`[5,1,4,2,3]` gives 3)
  - `Median_EvenCountThrows`
  - `Summary_TakesMediansIndependently` (memory, processes and svchost medians come from different samples)
  - `Summary_NeedsExactlyFiveSamples`
  - `Baseline_IsMedianOfThreeWithSpreadAsNoise` (in-use 2000, 2010, 2030 gives value 2010, noise 30)
  - `Baseline_RefusesFewerThanThreeMixedBuildsOrSleptRuns`
  - `Savings_IsBaselineMinusRun` (baseline 2010, run 1890 gives 120)
  - `Savings_InsideNoiseBandIsZero` (noise 30, difference 25 gives 0; difference -25 gives 0)
  - `Savings_NegativeOutsideBandIsKeptAndFlagged`
  - `Savings_CarriesBuildAndDate`
  - `RunResult_RoundTripsThroughCatalogJson`
- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~NitrOS.Measure.Tests"`. Expected: build fails.
- [ ] **Step 3:** Create the projects, add them to `NitrOS.slnx`, implement.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass, including every earlier test.
- [ ] **Step 5:** Commit `src/NitrOS.Measure`, `tests/NitrOS.Measure.Tests`, `NitrOS.slnx`, `.gitignore` with the message `Add measurement run records and statistics`.

---

### Task 2: Campaign plan and resumable state

**Files:**
- Create: `src/NitrOS.Measure/Campaign/CampaignPlanner.cs`, `src/NitrOS.Measure/Campaign/DefinitionHash.cs`, `src/NitrOS.Measure/Campaign/CampaignState.cs`, `src/NitrOS.Measure/Campaign/CampaignStore.cs`, `tests/NitrOS.Measure.Tests/Campaign/CampaignPlannerTests.cs`, `tests/NitrOS.Measure.Tests/Campaign/CampaignStoreTests.cs`

**Interfaces:**
- Consumes: `Catalog`, `Selection.FromCard`, `Selection.FromIds`, `DeviceProfile.Desktop`, `TargetMode.TunePc`, `CardDefault`.
- Produces, namespace `NitrOS.Measure.Campaign`:
  - `static class DefinitionHash { string Of(SwitchDef s); string Of(IEnumerable<SwitchDef> switches); }`. SHA-256 hex of `CatalogJson.Write(s with { Measured = null })`, so writing a measurement never changes the hash, but any change to actions, modes or cards does.
  - `static class CampaignPlanner { IReadOnlyList<RunSpec> Build(Catalog catalog, DeviceProfile device); }`. Order and ids, exact:
    - `baseline-1`, `baseline-2`, `baseline-3` (no ids)
    - `switch-<id>` for every switch whose `Modes` contains `live` and that `Selection.FromIds` keeps for the device, in catalog order
    - `card-recommended`, `card-gamer`, `card-streamer`, `card-max-lean`, with the ids of `Selection.FromCard(catalog, card, TunePc, device).On`, sorted ordinally
    - `variant-gamer-svchost`, `variant-streamer-svchost`: the card's ids plus `svchost-grouping-on`
    - `baseline-final`
  - `enum RunStatus { Pending, Running, Done, Failed }`
  - `record RunState(string RunId, RunStatus Status, int Attempts, string? ResultFile, string? LastError)`
  - `record CampaignState(string CampaignId, string WindowsBuild, string CliCommit, DateTimeOffset Created, IReadOnlyList<RunSpec> Runs, IReadOnlyList<RunState> States)`
  - `sealed class CampaignStore(string directory)`, files `campaign.json` and `runs\<RunId>.result.json`:
    - `CampaignState Create(string campaignId, string windowsBuild, string cliCommit, IReadOnlyList<RunSpec> runs)`: refuses if `campaign.json` exists
    - `CampaignState Load()`
    - `RunSpec? Next()`: the first `Running` run (an interrupted one), else the first `Pending`; marks it `Running`, adds 1 to `Attempts`, saves, returns it. Null when nothing is left. A run reaching `MaxAttempts` = 3 without completing becomes `Failed` and is skipped.
    - `void Complete(string runId, RunResult result)`: saves the result file first, then the state. Completing a `Done` run again is a no-op.
    - `void Fail(string runId, string error)`: back to `Pending` while `Attempts < 3`, else `Failed`.
    - `void Reset(string runId)`: back to `Pending` with `Attempts` 0 (for the owner to retry a failed run).
    - Every save is atomic.

- [ ] **Step 1:** Write the failing tests:
  - `Plan_HasBaselinesSwitchesCardsVariantsAndFinalBaselineInOrder` (on the real catalog: 119 runs, first three and last are baselines)
  - `Plan_LeavesOutImageOnlySwitches` (`recovery-remove` has no run)
  - `Plan_CardIdsMatchFromCard` (and `svchost-grouping-on` is not in `card-gamer`, because Pending counts as Off)
  - `Plan_VariantsAddSvchostToTheCard`
  - `Hash_IgnoresMeasuredButNotActions`
  - `Next_ReturnsInterruptedRunAgain` (a run left `Running` by a "crash" is returned next, attempts 2)
  - `Next_SkipsDoneAndMarksThirdFailureFailed`
  - `Complete_IsIdempotent`
  - `Complete_WritesResultBeforeState` (a fake crash between the two leaves the run `Running`, so it is rerun, never `Done` without a result)
  - `Create_RefusesExistingCampaign`
  - `Store_IgnoresLeftoverTmpFile`
- [ ] **Step 2:** Run the filter `Campaign`. Expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit `src/NitrOS.Measure/Campaign`, `tests/NitrOS.Measure.Tests/Campaign` with the message `Add measurement campaign plan and resumable state`.

---

### Task 3: Results calculation, catalog writer and report

**Files:**
- Create: `src/NitrOS.Measure/Results/ResultsCalculator.cs`, `src/NitrOS.Measure/Results/CatalogMeasurementWriter.cs`, `src/NitrOS.Measure/Results/ReportWriter.cs`, `tests/NitrOS.Measure.Tests/Results/*.cs`, `tests/NitrOS.Measure.Tests/Results/Fixtures/` (a two-file catalog copy with mixed formatting)

**Interfaces:**
- Consumes: `CampaignState`, `RunResult`, `BaselineCalc`, `Savings`, `DefinitionHash`, `CatalogLoader.Load`, `CatalogJson`, `CardMeasurements`, `Measurement`.
- Produces, namespace `NitrOS.Measure.Results`:
  - `record SvchostChoice(bool Gamer, bool Streamer)`; `SvchostChoice.FromCatalog(Catalog c)` reads `svchost-grouping-on`'s Gamer and Streamer defaults (On means true; Off and Pending mean false).
  - `record CampaignOutcome(Baseline Baseline, IReadOnlyDictionary<string, Measurement> Switches, CardMeasurements Cards, IReadOnlyList<(string Id, string Reason)> NotMeasured, IReadOnlyList<string> Flagged, int FinalBaselineDriftMb)`
  - `static class ResultsCalculator { CampaignOutcome Compute(CampaignState state, IReadOnlyDictionary<string, RunResult> results, Catalog current, DateOnly date, SvchostChoice svchost); }` Rules:
    - Every result must carry the campaign's `WindowsBuild`; otherwise throw.
    - A switch gets no number, with the reason, when: its run is missing or `Failed` (`not measured: the run failed`), its result is `Slept`, every action was Skipped in the test machine (`SwitchResults[id] == "Skipped"`: `not present in the test machine`), its switch result is `Failed` (`failed in the test machine`), or its `DefinitionHash` differs from the current catalog (`changed since it was measured`).
    - Card numbers come from `card-<card>`, except Gamer and Streamer, which come from their `variant-...-svchost` run when the matching `SvchostChoice` flag is true. A card whose chosen run's ids no longer equal the current `FromCard` selection gets null and a NotMeasured line.
    - `FinalBaselineDriftMb` = `baseline-final` in-use minus the baseline value; the report flags it when it is larger than twice the noise band.
  - `static class CatalogMeasurementWriter { void Write(string catalogDir, IReadOnlyDictionary<string, Measurement> switches, CardMeasurements cards); }` Rules:
    - Text splice, so the hand-written layout stays: locate each switch object by its `id` with `Utf8JsonReader` byte offsets; replace the value of an existing `measured` property, or insert `, "measured": { "memoryMb": N, "processes": N, "windowsBuild": "...", "date": "yyyy-MM-dd" }` before the object's closing brace. Switches not in the dictionary are left alone (an old number stays until a new one replaces it).
    - `cards.json` is written whole with `CatalogJson.Write(cards)`.
    - All or nothing: write every changed file to a staging copy of the folder, run `CatalogLoader.Load` on the staging copy, and require zero errors and that every switch equals the original except `Measured`. Only then move the files over the originals. Otherwise throw and change nothing.
    - An unknown id throws before anything is written.
  - `static class ReportWriter { string Markdown(CampaignOutcome outcome, CampaignState state, IReadOnlyDictionary<string, RunResult> results); }`: a table per switch (memory, processes, svchost, flagged), the cards, the baseline and noise band, the drift check, every NotMeasured reason, and the inputs for Task 15.

- [ ] **Step 1:** Write the failing tests:
  - `Compute_UsesMedianBaselineAndNoise`
  - `Compute_NoNumberForSkippedFailedOrChanged`
  - `Compute_ThrowsOnMixedBuilds`
  - `Compute_CardsUseVariantWhenSvchostChosen`
  - `Compute_FlagsFinalBaselineDrift`
  - `Writer_InsertsMeasuredIntoSwitchWithout`
  - `Writer_ReplacesExistingMeasured`
  - `Writer_LeavesEveryOtherByteUnchanged` (fixture bytes outside each `measured` span are identical)
  - `Writer_WritesCardsJsonThatLoads`
  - `Writer_RollsBackWhenLoaderFails` (a measurement with an empty `windowsBuild` makes the loader fail; every file is unchanged)
  - `Writer_UnknownIdThrowsBeforeWriting`
  - `Writer_RealCatalogCopyStillLoads` (copies `catalog/` to a temp folder, writes a measurement for every switch, loads it strictly)
  - `Report_ListsNotMeasuredReasons`
- [ ] **Step 2:** Run the filter `Results`. Expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit `src/NitrOS.Measure/Results`, `tests/NitrOS.Measure.Tests/Results` with the message `Add measurement results calculator, catalog writer and report`.

---

### Task 4: Guest sampler, state fingerprint, memory facts and the `nitros-probe` tool

**Files:**
- Create: `src/NitrOS.Measure/Guest/IGuestFacts.cs`, `src/NitrOS.Measure/Guest/WinGuestFacts.cs`, `src/NitrOS.Measure/Guest/Sampler.cs`, `src/NitrOS.Measure/Guest/Kvp.cs`, `src/NitrOS.Measure/Guest/Fingerprint.cs`, `src/NitrOS.Measure.Probe/NitrOS.Measure.Probe.csproj` (`OutputType` `WinExe`, assembly `nitros-probe`, `net10.0-windows10.0.19041.0`), `src/NitrOS.Measure.Probe/Program.cs`, `src/NitrOS.Measure.Probe/ProbeApp.cs`, `src/NitrOS.Live/MemoryFacts.cs`, `src/NitrOS.Live/Windows/WinMemoryFacts.cs`, tests in `tests/NitrOS.Measure.Tests/Guest/` and `tests/NitrOS.Live.Tests/MemoryFactsTests.cs`
- Modify: `src/NitrOS.Cli/CliEnvironment.cs`, `src/NitrOS.Cli/CliApp.cs` (detect prints memory lines), `tests/NitrOS.Live.Tests/CliTests.cs`, `tests/NitrOS.Measure.Tests/NitrOS.Measure.Tests.csproj` (links `..\NitrOS.Live.Tests\Fakes\*.cs` as compiled files, so the fingerprint is tested with the existing fakes), `NitrOS.slnx`

**Interfaces:**
- Produces, namespace `NitrOS.Live`:
  - `record MemoryInfo(int InstalledMb, int VisibleMb, int InUseMb) { int ReservedMb => InstalledMb - VisibleMb; }`
  - `interface IMemoryFacts { MemoryInfo Read(); }`, `WinMemoryFacts` (`GetPhysicallyInstalledSystemMemory` in KB, `GlobalMemoryStatusEx`).
  - `CliEnvironment` gets `IMemoryFacts? Memory { get; init; }` (null in tests that don't set it; `Real()` sets `new WinMemoryFacts()`). `nitros detect` adds `Memory installed: N MB`, `Memory available to Windows: N MB`, `Reserved for hardware (mostly the GPU): N MB` when `Memory` is set.
- Produces, namespace `NitrOS.Measure.Guest`:
  - `interface IGuestFacts { (long TotalPhys, long AvailPhys) Memory(); IReadOnlyList<(int Pid, string Name)> Processes(); TimeSpan UnbiasedTime(); DateTimeOffset Now(); string WindowsBuild(); int OwnPid { get; } }`. `WinGuestFacts`: `GlobalMemoryStatusEx`, `Process.GetProcesses()`, `QueryUnbiasedInterruptTime` (excludes time asleep), and `CurrentBuild` + `.` + `UBR` from `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`.
  - `record SamplerOptions(TimeSpan Idle, int Count, TimeSpan Interval)` with `Default` = 10 minutes, 5, 30 seconds.
  - `record SampleRun(IReadOnlyList<MemorySample> Samples, int VisibleMb, string WindowsBuild, bool Slept, DateTimeOffset Started, DateTimeOffset Finished)`
  - `sealed class Sampler(IGuestFacts facts, Action<TimeSpan> sleep) { SampleRun Run(SamplerOptions options); }`. `Slept` is true when wall-clock time minus unbiased time over the run exceeds 30 seconds.
  - `interface IKvpWriter { void Set(string key, string value); }`; `WinKvpWriter` writes string values under `HKLM\SOFTWARE\Microsoft\Virtual Machine\Guest` (the documented guest side of Hyper-V data exchange). Keys used: `NitrOS.Measure.State` = `idle`, `sampling`, `done` or `error`.
  - `record Fingerprint(IReadOnlyDictionary<string, string> Entries)`: key `<switchId>#<actionIndex>`, value the `PriorState` JSON (`CatalogJson.Options`).
  - `static class FingerprintTool`: `Fingerprint Capture(Catalog catalog, LiveSystems systems)` calls `Handlers.For(action).Read(action, systems)` for every live action of every switch; a throwing `Read` stores `error: <message>`. `IReadOnlyList<FingerprintDiff> Compare(Fingerprint before, Fingerprint after, Catalog catalog)` with `record FingerprintDiff(string Key, string Before, string After, bool Expected)`; `Expected` is true for actions of irreversible switches, `appx.remove`, `capability.remove`, `file.remove`, `program.uninstall` and `script`.
- `nitros-probe` commands (exit 0 OK, 1 failed, 2 bad arguments; errors go to `<out>.error.txt` because a WinExe has no console):
  - `sample --out <file> [--idle-minutes 10] [--count 5] [--interval-seconds 30] [--kvp]`: writes `SampleRun` JSON; with `--kvp` sets the state key as it goes.
  - `fingerprint --out <file> [--catalog <dir>]`: needs elevation (reads services, tasks and all-user apps through `WinSystems.Create`); refuses otherwise.

- [ ] **Step 1:** Write the failing tests:
  - `Sampler_TakesCountSamplesAfterIdle` (fake sleep records 10 minutes, then 4 waits of 30 seconds)
  - `Sampler_ExcludesOwnProcessAndIdle`
  - `Sampler_CountsSvchost` (case-insensitive name `svchost`)
  - `Sampler_InUseIsTotalMinusAvailableInMib`
  - `Sampler_FlagsSleep` (fake unbiased time falls 60 seconds behind wall time)
  - `Fingerprint_CapturesEveryLiveAction` (fake systems, real catalog: one entry per live action)
  - `Compare_ReportsOnlyReversibleDifferences` (a changed registry value is unexpected, a removed app is expected)
  - `Probe_SampleWritesJsonAndKvpStates` (fake facts and a fake `IKvpWriter`: `sampling` then `done`)
  - `Probe_BadArgumentsExit2`
  - `MemoryInfo_ReservedIsInstalledMinusVisible`
  - `Detect_PrintsMemoryLinesWhenSet`
  - Read-only on the owner's PC: `WinMemoryFacts_ReadsThisPc` (installed at least visible, visible above 0); `WinGuestFacts_ReadsThisPc` (process count above 50, build at least 26100).
- [ ] **Step 2:** Run the filters `Guest|MemoryFacts|Detect`. Expected: fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit the files above with the message `Add guest sampler, state fingerprint and memory facts`.

---

### Task 5: Host tool `nitros-measure` and the answer file

**Files:**
- Create: `src/NitrOS.Measure.Host/NitrOS.Measure.Host.csproj` (console, assembly `nitros-measure`, `net10.0-windows10.0.19041.0`), `src/NitrOS.Measure.Host/Program.cs`, `src/NitrOS.Measure.Host/HostApp.cs`, `src/NitrOS.Measure/Unattend/AnswerFile.cs`, `src/NitrOS.Measure/Unattend/AnswerIso.cs`, `measure/unattend/autounattend.template.xml`, `measure/measure.example.json`, `measure/README.md`, tests in `tests/NitrOS.Measure.Tests/Host/` and `tests/NitrOS.Measure.Tests/Unattend/`
- Modify: `NitrOS.slnx`

**Interfaces:**
- `static int HostApp.Run(string[] args, TextWriter output, IReadOnlyDictionary<string, string?> environment)`. Exit codes: 0 OK, 1 failed or refused, 2 bad arguments, 4 nothing left (`next` only). Commands, exact:
  - `plan --campaign <dir> --catalog <dir> --build <windowsBuild> --cli-commit <sha>`
  - `next --campaign <dir>`: prints the `RunSpec` as one JSON line
  - `complete --campaign <dir> --run <id> --result <file>`
  - `fail --campaign <dir> --run <id> --error <text>`
  - `reset --campaign <dir> --run <id>`
  - `status --campaign <dir>`: counts per status and an estimate of the time left at 20 minutes a run
  - `summarize-run --run <id> --samples <SampleRun file> [--journal <file>] [--apply-exit <n>] --out <file>`: builds a `RunResult`; `SwitchResults` from the journal (a switch is `Skipped` when every entry for it is Skipped, `Failed` when any is Failed or RolledBack, else `Applied`)
  - `results --campaign <dir> --catalog <dir> [--write]`: computes with `SvchostChoice.FromCatalog`, always writes `<campaign>\report.md`, and writes the catalog only with `--write`
  - `compare-fingerprints --before <file> --after <file> --catalog <dir>`: prints every diff; exit 1 when any is unexpected
  - `answer-iso --template <xml> --out <iso> --image-index <n> --user <name> [--computer-name NITROS-MEASURE]`: the password comes only from the environment variable `NITROS_MEASURE_PASSWORD`; it refuses when that is missing.
- `static class AnswerFile { string Render(string template, int imageIndex, string user, string password, string computerName); }`: replaces `{{IMAGE_INDEX}}`, `{{USER}}`, `{{PASSWORD}}`, `{{COMPUTER_NAME}}` (XML-escaped); throws if any `{{` remains or the result is not well-formed XML.
- `static class AnswerIso { void Write(string isoPath, string fileName, string content); }`: IMAPI2 `IMAPI2FS.MsftFileSystemImage` through COM, ISO 9660 plus Joliet, the file at the root, `CreateResultImage().ImageStream` copied to the file.
- `measure/unattend/autounattend.template.xml`, settings (verify first: every element name and pass against the Unattended Windows Setup Reference, https://learn.microsoft.com/windows-hardware/customize/desktop/unattend/, and note the date checked):
  - windowsPE: en-US language; wipe disk 0, GPT with EFI (300 MB), MSR (16 MB) and Windows partitions; install image `/IMAGE/INDEX` = `{{IMAGE_INDEX}}` to disk 0 partition 3; accept the EULA. Product key: first try none. If Setup stops at the key page (verify first in Task 7), use the Windows 11 Pro generic key Microsoft publishes for this purpose on https://learn.microsoft.com/windows-server/get-started/kms-client-activation-keys (checked 2026-10-28). The test machine is never activated; that is fine for measuring.
  - specialize: computer name `{{COMPUTER_NAME}}`.
  - oobeSystem: hide the EULA, online-account and wireless pages; local account `{{USER}}` in Administrators with password `{{PASSWORD}}`; automatic sign-in for that account, kept on after setup; time zone UTC.
- `measure/measure.example.json` (committed, no secrets): `{ "isoPath": "<owner supplies>", "vmRoot": "X:\\NitrOS-VM", "vmName": "nitros-measure", "checkpoint": "clean", "user": "nitros-test", "passwordProtected": "<written by New-MeasureConfig.ps1>" }`.

- [ ] **Step 1:** Write the failing tests:
  - `Host_PlanThenNextThenCompleteFlow` (temp campaign folder, real catalog)
  - `Host_NextExit4WhenDone`
  - `Host_SummarizeRunMapsJournalOutcomes`
  - `Host_ResultsWithoutWriteLeavesCatalogUntouched`
  - `Host_AnswerIsoRefusesWithoutPasswordVariable`
  - `Host_BadArgumentsExit2`
  - `AnswerFile_ReplacesEveryPlaceholderAndEscapes` (a password with `<&"` stays valid XML)
  - `AnswerFile_TemplateInRepoRendersToValidXml`
  - `AnswerIso_WritesIsoWithFileAtRoot` (writes in a temp folder; checks `CD001` at offset 0x8001 and the file name in the image; deletes it)
  - `Password_NeverInOutput` (runs `answer-iso` with a known test password and asserts the output text does not contain it)
- [ ] **Step 2:** Run the filters `Host|AnswerFile|AnswerIso`. Expected: fail.
- [ ] **Step 3:** Implement. `measure/README.md` explains the folder, the scripts' order, the config file and what is git-ignored, in plain words.
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass.
- [ ] **Step 5:** Commit the files above with the message `Add nitros-measure host tool and unattended answer file`.

---

### Task 6: Host pre-check and turning on Hyper-V (OWNER CHECKPOINT)

**Files:**
- Create: `measure/scripts/Test-HostReady.ps1`, `measure/scripts/Enable-HyperV.ps1`, `docs/research/hyperv-host.md`

**Background (checked read-only on 2026-10-28):** Windows 11 Pro 26300, firmware virtualization on, a hypervisor already running (Virtualization-based security reports running with System Guard services, Memory Integrity off). `Win32_OptionalFeature` shows `Microsoft-Hyper-V-All`, `Microsoft-Hyper-V`, its tools and `VirtualMachinePlatform` as disabled (InstallState 2). No `vmms` service. Shadow Defender's service is running and its boot driver `diskpt` is installed. Drive X: has about 955 GB free. The host sees about 16 GB of memory; about 0.6 GB was free at the time.

`Test-HostReady.ps1` is read-only. Its non-admin part runs any time; with `-Elevated` it adds the read-only checks that need admin. It writes `%USERPROFILE%\NitrOS-host-ready.txt` with one `PASS`, `WARN` or `FAIL` line per check:
- Windows edition is Pro, Enterprise or Education (Hyper-V needs one of them).
- Firmware virtualization is on (`Win32_Processor.VirtualizationFirmwareEnabled`).
- Hyper-V feature states (`Win32_OptionalFeature`), and whether the `vmms` service and the Hyper-V module exist.
- A restart already pending: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending`, `...\WindowsUpdate\Auto Update\RebootRequired`, and `PendingFileRenameOperations`. A pending one is WARN: finish it first.
- Shadow Defender: the service under `C:\Program Files\Shadow Defender\` and the `diskpt` driver. If present, WARN with the text: "Shadow Defender is installed. If drive C: or the test machine's drive is in Shadow Mode, turning on Hyper-V and every test machine file are thrown away at restart. Check in Shadow Defender's window that no drive is in Shadow Mode." NitrOS cannot read Shadow Mode reliably (`dism-access-denied.md`), so the owner confirms it by looking.
- Free space on the drive of `vmRoot` at least 150 GB (one 64 GB disk, checkpoints, ISOs).
- With `-Elevated`: `dism /English /Online /Get-FeatureInfo /FeatureName:Microsoft-Hyper-V` exits 0. This proves DISM's feature path works here, unlike its provisioned-app path.
- With `-Elevated`: BitLocker state of C: (`Get-BitLockerVolume`). If on, WARN: "Have your BitLocker recovery key ready before the restart."
- On AC power (`Win32_Battery`), WARN when on battery.

`Enable-HyperV.ps1` runs `Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All -NoRestart` (the feature name and `-All` are from https://learn.microsoft.com/windows-server/virtualization/hyper-v/get-started/install-hyper-v, checked 2026-10-28; verify first that `-NoRestart` is listed on https://learn.microsoft.com/powershell/module/dism/enable-windowsoptionalfeature). It writes the result and `RestartNeeded` to `%USERPROFILE%\NitrOS-hyperv-enable.txt`. It never restarts.

- [ ] **Step 1:** Write both scripts. Run `Test-HostReady.ps1` without `-Elevated` (read-only, allowed) and save its output in `docs/research/hyperv-host.md`.
- [ ] **Step 2: OWNER CHECKPOINT.** Show the owner the pre-check result and this text: "Turning on Hyper-V needs administrator rights and a restart. It adds Hyper-V's services and lets Windows run the test machine. NitrOS's Gamer and Streamer cards leave Hyper-V as it is; turning it off later is one command. Before saying yes: check in Shadow Defender's window that no drive is in Shadow Mode, have your BitLocker key ready if BitLocker is on, and save your work." On a yes, run `Test-HostReady.ps1 -Elevated`, then `Enable-HyperV.ps1`, each through a UAC-elevated `.ps1` writing to `%USERPROFILE%`.
- [ ] **Step 3: OWNER CHECKPOINT.** Ask for the restart. Restart only after an explicit OK.
- [ ] **Step 4:** After the restart, run `Test-HostReady.ps1` again. Expected: Hyper-V enabled, `vmms` running, the Hyper-V module present, and a `Default Switch` (`Get-VMSwitch`, elevated). Verify first, with `Get-Command -Module Hyper-V`, that every cmdlet Task 7 names exists: `New-VM`, `Set-VMMemory`, `Set-VMProcessor`, `Set-VMKeyProtector`, `Enable-VMTPM`, `Set-VMFirmware`, `Add-VMDvdDrive`, `Set-VM`, `Checkpoint-VM`, `Restore-VMSnapshot`, `Get-VMSnapshot`, `Connect-VMNetworkAdapter`, `Disconnect-VMNetworkAdapter`, `Get-VMIntegrationService`. Record the result in `docs/research/hyperv-host.md`.
- [ ] **Step 5:** Run `dotnet test`. Expected: all pass. Commit `measure/scripts/Test-HostReady.ps1`, `measure/scripts/Enable-HyperV.ps1`, `docs/research/hyperv-host.md` with the message `Add host pre-check and Hyper-V enable script`.

---

### Task 7: Test machine creation, PowerShell Direct and one measured run

**Files:**
- Create: `measure/scripts/NitrOS.Measure.psm1`, `measure/scripts/New-MeasureConfig.ps1`, `measure/scripts/Publish-GuestTools.ps1`, `measure/scripts/New-MeasureVm.ps1`, `measure/scripts/Invoke-MeasureRun.ps1`, `docs/research/vm-setup.md`

**Interfaces:**
- `NitrOS.Measure.psm1` exports:
  - `Get-MeasureConfig [-Path]` (returns the config with a `PSCredential`; the password is decrypted only into the credential)
  - `Get-GuestSession -VmName -Credential [-TimeoutMinutes 15]` (retries `New-PSSession -VMName` until it works)
  - `Invoke-GuestTask -Session -Name -CommandLine -TimeoutMinutes`: inside the guest, registers `\NitrOS.Measure\<Name>` for the test user with `-LogonType Interactive -RunLevel Highest`, action `cmd.exe /c <CommandLine> > C:\NitrOS.Measure\out\<Name>.log 2>&1`, starts it, waits until it is no longer running, and returns `{ ExitCode, Log }`. Running through a task puts the CLI in the signed-in user's session, elevated, so `SessionGuard` and HKCU see the right account.
  - `Wait-GuestKvp -VmName -Key -Value -TimeoutMinutes`: reads the guest's data exchange items on the host through WMI (`root\virtualization\v2`); starts no guest process.
  - `Assert-HostReadyForRun`: AC power and at least 4,608 MB of host memory available (`\Memory\Available MBytes`); it waits up to 30 minutes, checking every minute, then returns not-ready.
  - `Send-VmKeys -VmName -VirtualKey 13 -Seconds 20`: `Msvm_Keyboard.TypeKey` (documented on https://learn.microsoft.com/windows/win32/hyperv_v2/typekey-msvm-keyboard, checked 2026-10-28), to answer the "Press any key to boot from CD or DVD" prompt.
- `New-MeasureConfig.ps1 -IsoPath <path> [-VmRoot] [-VmName] [-User nitros-test]`: not elevated. Generates a 24-character password with `System.Security.Cryptography.RandomNumberGenerator` (upper, lower, digits, symbols), stores it with `ConvertFrom-SecureString` (verify first that, with no key, this uses Windows data protection for the current user: https://learn.microsoft.com/powershell/module/microsoft.powershell.security/convertfrom-securestring) in `measure\measure.local.json`, and refuses to overwrite an existing file.
- `Publish-GuestTools.ps1`: `dotnet publish src/NitrOS.Cli -c Release -r win-x64 --self-contained true -o measure/work/guest/nitros` and the same for `src/NitrOS.Measure.Probe` into `measure/work/guest/probe`. Self-contained, so nothing has to be installed in the test machine. Writes `measure/work/guest/commit.txt` with `git rev-parse HEAD`.
- `New-MeasureVm.ps1` and `Invoke-MeasureRun.ps1`: parameters and outputs exactly as in "Interface for plan 6".

`New-MeasureVm.ps1` steps:
1. Mount the ISO read-only (`Mount-DiskImage -Access ReadOnly`), read `sources\install.wim` or `install.esd` with `Get-WindowsImage`, and dismount it in a `finally`. Refuse when the image version is below 10.0.26100 ("NitrOS needs a Windows 11 24H2 or newer ISO.") or no image has EditionId `Professional`. That image's index is used.
2. With `NITROS_MEASURE_PASSWORD` set only for the child, run `nitros-measure answer-iso` into `<vmRoot>\<VmName>\answer.iso`.
3. `New-VM -Generation 2 -MemoryStartupBytes 4GB -NewVHDPath <vmRoot>\<VmName>\disk.vhdx -NewVHDSizeBytes 64GB`, `Set-VMMemory -DynamicMemoryEnabled $false`, `Set-VMProcessor -Count 4`, `Set-VMKeyProtector -NewLocalKeyProtector`, `Enable-VMTPM`, `Set-VMFirmware -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows`, two DVD drives (Windows ISO first, answer ISO second), DVD as first boot device, `Set-VM -CheckpointType Standard -AutomaticCheckpointsEnabled $false`, network adapter disconnected.
4. Start the machine, `Send-VmKeys`, then wait up to 90 minutes until `Get-GuestSession` works with the test credential.
5. Prepare, through PowerShell Direct: check `CurrentBuild` is at least 26100; create `C:\NitrOS.Measure\bin` and `\out`; set sleep and hibernate timeouts on AC to 0 in every scheme `powercfg /list` shows, so the guest never sleeps during the 10-minute idle (sleep still gets caught by the sampler's check); set the home location to the United States (`Set-WinHomeLocation`, GeoId verify first on https://learn.microsoft.com/windows/win32/intl/table-of-geographical-locations), because the Edge test in Task 10 needs a known region.
6. Wait `-SettleMinutes` (default 30) signed in, so first-sign-in work finishes. Shut down cleanly, remove the answer DVD, delete `answer.iso`, make the disk the first boot device, `Checkpoint-VM -SnapshotName <CheckpointName>`.
7. Write the `.vm.json` file.

`Invoke-MeasureRun.ps1` steps:
1. `Assert-HostReadyForRun`; if not ready, exit 3.
2. Turn the machine off if it runs, `Restore-VMSnapshot -VMName -Name -Confirm:$false`, set the network adapter per `-Network` (Off for measurement: no Windows Update or Store downloads, so runs repeat).
3. Start, open a guest session, copy `measure\work\guest\*` to `C:\NitrOS.Measure\bin`, set the KVP state to `idle`, and register `\NitrOS.Measure\Sample` (trigger: sign-in of the test user; action `nitros-probe sample --out C:\NitrOS.Measure\out\samples.json --kvp`; elevated, because writing the data exchange key needs it). The same steps run for baseline runs, so they cancel out.
4. Unless `-SkipApply` or no ids: `Invoke-GuestTask -Name Apply -CommandLine "C:\NitrOS.Measure\bin\nitros\nitros.exe apply --ids <ids> --yes --allow-without-restore-point"`, then copy the newest journal from `%ProgramData%\NitrOS\backups` out as `<RunId>.journal.json`.
5. Restart the guest with `shutdown.exe /r /t 0`, close the session, and from then on touch the guest only through the host's KVP reads until `done` (timeout 30 minutes) or `error`.
6. Open a session, copy `samples.json` out, shut down cleanly, and run `nitros-measure summarize-run` into `<RunId>.result.json`. Exit 3 when the result says `Slept`.

- [ ] **Step 1:** Write the module and scripts. Run `Publish-GuestTools.ps1` (builds only; allowed).
- [ ] **Step 2: OWNER CHECKPOINT.** Ask the owner for the path of their Windows 11 ISO (24H2 or newer). Run `New-MeasureConfig.ps1 -IsoPath <path>` (not elevated). Never echo the password.
- [ ] **Step 3: OWNER CHECKPOINT.** With the owner's OK, run `New-MeasureVm.ps1` elevated through UAC. Verify first, and write each answer with its evidence in `docs/research/vm-setup.md`:
  - Setup finds `Autounattend.xml` on the second DVD (Microsoft lists removable read-only media in the implicit search order: https://learn.microsoft.com/windows-hardware/manufacture/desktop/windows-setup-automation-overview, checked 2026-10-28). Fallback if not: put the answer file on a FAT32 virtual disk and, if still not found, report BLOCKED.
  - Setup gets past the product key page with no key. If not, use the published Pro generic key (Task 5).
  - Out-of-box setup finishes offline with the local account. If it asks for a network, add the documented setting that allows offline setup and record its source; if none is documented, report NEEDS_CONTEXT.
  - Automatic sign-in survives restarts.
  - The PowerShell Direct session is elevated (`IsInRole(Administrator)` is true). If it is not, every guest step that needs admin goes through `Invoke-GuestTask`.
  - The host reads a KVP value the guest wrote. Fallback if not: the host waits a fixed 15 minutes after the restart, and the sampler records any PowerShell Direct host process seen during sampling so such a run is marked bad and rerun.
- [ ] **Step 4: OWNER CHECKPOINT.** Run `Invoke-MeasureRun.ps1 -RunId trial-baseline` and `-RunId trial-switch -Ids fax-off -Network Off`. Expected: two result files, five samples each, `Slept` false, the fax run's journal shows `fax-off` Applied. Record both and the run time in `docs/research/vm-setup.md`.
- [ ] **Step 5:** Run `dotnet test`. Expected: all pass. Commit `measure/scripts`, `docs/research/vm-setup.md` with the message `Add test machine creation and single measured run`.

---

### Task 8: Plan 3's VM-lane tests inside the test machine

**Files:**
- Create: `measure/scripts/Invoke-GuestTests.ps1`, `docs/research/vm-lane-results.md`
- Modify: only files under `src/NitrOS.Live` and `tests/NitrOS.Live.Tests` that a failing VM-lane test proves wrong, each fix with a test.

`Invoke-GuestTests.ps1`: restore the clean checkpoint, start with the network connected (the health probe needs the internet), publish `tests/NitrOS.Live.Tests` self-contained for win-x64 and copy it in, then `Invoke-GuestTask -Name Tests` running `NitrOS.Live.Tests.exe` with `NITROS_VM=1` set in the task's command line (`cmd /c set NITROS_VM=1&& ...`). Copy the log and report out. Restore the checkpoint afterwards.

- [ ] **Step 1:** Verify first, on the host with `NitrOS.Live.Tests.exe --help` (read-only), the xUnit v3 runner's options to filter by trait and to write a report. Record them in `docs/research/vm-lane-results.md`.
- [ ] **Step 2: OWNER CHECKPOINT.** Run `Invoke-GuestTests.ps1` twice: once for the whole suite with `NITROS_VM=1`, once filtered to `Lane=Vm`. Expected: every VM-lane test runs (none skipped) and passes. Read-only tests that assume the Ally may skip; list them.
- [ ] **Step 3:** For each failure: write down the cause, fix it with a test that fails first on the fakes where possible, and rerun the suite in the test machine. Stop and report BLOCKED after three rounds on the same failure.
- [ ] **Step 4:** Write `docs/research/vm-lane-results.md`: tests run, passed, skipped (with reason), the fixes, the build of the test machine.
- [ ] **Step 5:** Run `dotnet test` on the host. Expected: all pass. Commit `measure/scripts/Invoke-GuestTests.ps1`, `docs/research/vm-lane-results.md` and any fixes with the message `Run tuner VM-lane tests in the test machine`.

---

### Task 9: Undo everything and the health check, per card (spec 8.2)

**Files:**
- Create: `measure/scripts/Test-UndoEverything.ps1`, `docs/research/undo-everything.md`

For each card (`recommended`, `gamer`, `streamer`, `max-lean`), `Test-UndoEverything.ps1` runs:
1. Restore the clean checkpoint, start with the network connected.
2. `nitros-probe fingerprint --out before.json` (through `Invoke-GuestTask`, elevated).
3. `nitros apply --card <card> --yes` (with `--allow-without-restore-point` only when System Restore is off in the guest; record which).
4. Restart, wait for sign-in, wait 2 minutes, then `nitros health`. Expected: exit 0. Record every line.
5. `nitros undo <newest journal> --yes`, restart, wait for sign-in.
6. `nitros-probe fingerprint --out after.json`, copy both out, `nitros-measure compare-fingerprints`.
7. Also `nitros health` again after undo.

Pass means: health passes after the card and after undo, and `compare-fingerprints` exits 0 (every difference is in an irreversible action, which the undo listed under "Cannot undo").

- [ ] **Step 1:** Write the script.
- [ ] **Step 2: OWNER CHECKPOINT.** Run it for all four cards.
- [ ] **Step 3:** For any unexpected difference or health failure, find the cause. Fix engine bugs in `src/NitrOS.Live` with a failing fake test first. A catalog switch whose undo cannot work as written is fixed in the catalog (for example marked `reversible: false` with a breaks line), with its group test updated. Rerun the card.
- [ ] **Step 4:** Write `docs/research/undo-everything.md`: per card, health before and after undo, the diff list, the cannot-undo list, the fixes.
- [ ] **Step 5:** Run `dotnet test`. Expected: all pass. Commit the script, the doc and any fixes with the message `Check undo everything and health per card in the test machine`.

---

### Task 10: Investigations A (switch behaviour in the test machine)

**Files:**
- Create: `measure/investigations/*.ps1` (one per item), `docs/research/vm-findings.md`
- Modify (only where the evidence demands it, each with its group test updated): `catalog/*.json`, `tests/NitrOS.Core.Tests/CatalogGroups/*.cs`, `docs/research/notes-for-later-plans.md`

Each item starts from the clean checkpoint, uses `Invoke-MeasureRun.ps1` or `Invoke-GuestTask`, and writes a section in `vm-findings.md` with: what was done, raw evidence (command output saved under `docs/research/evidence/vm-*.txt`), the finding, and the catalog change or "no change" with the reason.

- [ ] **Step 1: Service host threshold value** (notes: "Whether the svchost data value 4294967295 behaves like installed RAM"). Three runs: no change, `SvcHostSplitThresholdInKB` = 4294967295 (the catalog's value), and = the guest's installed memory in KB. Compare the `svchost` and process counts. If 4294967295 does not group like the installed-memory value, change the catalog data to a value that does, with evidence.
- [ ] **Step 2: Search's web engine flag 1694661260** (notes). Apply `search-web-engine-off`, restart, open Search once through a guest task that presses the Windows key with `Send-VmKeys`, then list `msedgewebview2.exe` processes and their parent. Compare with a baseline run. Finding: does the flag stop Search's web view? If not, fix or drop the switch.
- [ ] **Step 3: App Installer is NonRemovable** (notes). Apply `app-installer-remove`. Record the journal outcome, `Get-AppxPackage Microsoft.DesktopAppInstaller -AllUsers` after, and whether `winget` still starts. If the removal does nothing or fails, change the switch (for example image-only) or drop it.
- [ ] **Step 4: Defender removal with Tamper Protection on** (notes). Confirm Tamper Protection is on in the clean guest (`Get-MpComputerStatus`, read in the guest). Apply `defender-remove`, restart. Record the journal, whether `MsMpEng` still runs, and `Get-MpComputerStatus` after. If refused, the switch's breaks text and description tell the user to turn off Tamper Protection first, and the notes get a line for plan 5 to show it before applying.
- [ ] **Step 5: Edge `--force-uninstall`, outside and inside the EEA** (notes). Twice: home location United States, then Germany (GeoIds verify first). Apply `edge-remove`, restart, network on, wait 30 minutes. Record: `msedge.exe` on disk, the uninstall entry, Start menu shortcut, and what came back. Also check the WebView2 runtime still works: its folder under `Program Files (x86)\Microsoft\EdgeWebView\Application` and its registration (registry location verify first on https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution) are intact, and a WebView2 host (the Search panel from step 2) still opens. The image-mode WebView2 check stays with plan 6 (see report).
- [ ] **Step 6: Uninstallers that show a window** (notes). From the Edge and OneDrive runs: did `program.uninstall` finish within its timeout with no input? Record any window seen in the guest (`Get-Process` with a `MainWindowTitle` during the apply). Armoury Crate and MyASUS uninstallers are not in the test machine; they go to Task 17.
- [ ] **Step 7: Protected services and tasks** (notes). Scan every journal from Tasks 8 to 10 for actions that Failed with access denied (Task 14 Step 4 repeats this over all campaign journals). For each, confirm the switch shows `Failed` with every earlier action `RolledBack`. List them. A switch that can never work for an elevated admin is changed or dropped.
- [ ] **Step 8:** Run `dotnet test`. Expected: all pass. Commit `measure/investigations`, `docs/research/vm-findings.md`, `docs/research/evidence/vm-*.txt` and any catalog fixes with the message `Investigate switch behaviour in the test machine`.

---

### Task 11: Investigations B (location, background apps, built-in apps, DISM)

**Files:**
- Create: more `measure/investigations/*.ps1`
- Modify: `docs/research/vm-findings.md`, `docs/research/dism-access-denied.md`, and, where the evidence demands, `catalog/*.json` with group tests

- [ ] **Step 1: Location off versus automatic time zone** (notes). Network on. Set the guest's time zone to a wrong one with "Set time zone automatically" on (`tzautoupdate` service and its setting; verify first how Windows 11 stores that setting). Wait 15 minutes; record whether it corrected. Repeat after `geolocation-off`. If the time zone stops updating, add that sentence to `geolocation-off`'s breaks.
- [ ] **Step 2: Background apps force-deny versus the Xbox app** (notes). Apply the Gamer card ids plus `background-apps-off`. Health check (Game Pass item), then start the Xbox app through a guest task and record whether `GamingServices` and the Xbox app's background task run. The full-screen experience is handheld-only and goes to Task 17. Update `background-apps-off`'s breaks with what is found.
- [ ] **Step 3: Built-in app permissions on a clean Windows** (notes, Task 11 item). In the clean guest, read the permissions (SDDL) and owner of `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx`, `...\AppModel` and `...\Appx\AppxAllUserStore\InboxApplications`, and run `tools/verify/collect-dism-evidence.ps1` elevated through a guest task. Compare with the owner's PC evidence. Record whether `dism /Online /Get-ProvisionedAppxPackages` works and whether `HKLM\SOFTWARE` opens for writing in a clean Windows.
- [ ] **Step 4: Deprovisioning under those permissions** (notes, plan 3 final review). Apply `clipchamp-remove` and record whether `DeprovisionPackageForAllUsersAsync` succeeded (journal detail) on the clean guest.
- [ ] **Step 5: OWNER CHECKPOINT, optional: Shadow Defender in the test machine** (notes, Task 11 item; `dism-access-denied.md` "Ways to confirm (a)"). Only if the owner agrees and copies their own Shadow Defender 1.5.0.726 installer to the host (NitrOS downloads nothing from third parties). From a separate checkpoint, install it in the guest, restart, rerun Steps 3 and 4. Record whether the refusals appear. Delete that checkpoint afterwards so the clean one stays clean. If the owner declines, write that the cause stays unproven.
- [ ] **Step 6:** Update `dism-access-denied.md`'s "Not proven" list with what is now proven. Run `dotnet test`. Expected: all pass. Commit with the message `Investigate location, background apps and built-in app permissions`.

---

### Task 12: The `device-companion-apps-off` reinstall block

**Files:**
- Modify: `catalog/manufacturer-addons.json`, `tests/NitrOS.Core.Tests/CatalogGroups/ManufacturerAddonsTests.cs`, `tests/NitrOS.Core.Tests/CatalogGroups/CardsTableTests.cs`, `docs/research/reinstall-block.md`, `docs/research/vm-findings.md`

The proposal in `reinstall-block.md` uses `HKLM\SOFTWARE\Policies\Microsoft\Windows\Device Metadata\PreventDeviceMetadataFromNetwork` = 1. The real route on the Ally (Dolby and Realtek driver packages naming Store apps) needs that hardware, which a Hyper-V machine does not have, so the reinstall part goes to Task 17.

- [ ] **Step 1:** Copy `catalog/` to `measure/work/catalog-proposal/` and add the proposed entry there. In the test machine, run `nitros apply --ids device-companion-apps-off --catalog <that copy> --yes --allow-without-restore-point` through `Invoke-GuestTask`, restart, and record that Windows reads the value: the "Device installation settings" dialog shows its choice as set by the organisation (read its state through a guest task; verify first how to read it), and the policy name and value match Microsoft's documentation of the DeviceSetup policy (verify first; record the URL and date). Then `nitros undo` it and confirm the value is gone.
- [ ] **Step 2:** If Windows reads it: add the entry exactly as proposed in `reinstall-block.md` (id, name, feature, description, `safe`, breaks, both modes, reversible, On for every card) to `manufacturer-addons.json`. The description must not promise more than the policy does. If Windows does not read it, do not add it; record why.
- [ ] **Step 3:** Update the tests: `ManufacturerAddons_HasExactlyTheVerifiedIds` gains the id; `CardsTableTests` gains its row (On, On, On, On). In `reinstall-block.md`, mark the proposal as added, with the date and "plan 4".
- [ ] **Step 4:** Run `dotnet test`. Expected: all pass, loader strict. Commit with the message `Add device companion apps block after test machine check`.

This task runs before the campaign (Task 14), so the new switch is measured with the rest.

---

### Task 13: Prove and build "Add Xbox back" (spec 3)

**Files:**
- Create: `docs/research/add-xbox-back.md`, `src/NitrOS.Live/XboxRestore.cs`, `src/NitrOS.Live/Windows/WinXboxRestore.cs`, `tests/NitrOS.Live.Tests/XboxRestoreTests.cs`
- Modify: `src/NitrOS.Cli/CliApp.cs` (new command), `tests/NitrOS.Live.Tests/CliTests.cs`

Max Lean removes `store-remove`, `xbox-app-remove`, `game-bar-remove`, `xbox-sign-in-remove`, `gaming-services-remove` and turns `xbox-services-off`. The button must bring back the Store and the Xbox parts together.

- [ ] **Step 1: Find candidates** from Microsoft sources only, each with its URL and the date checked: for example `wsreset.exe -i` for the Store, re-registering packages still on disk, `winget` from the `msstore` source, the official Xbox app installer from xbox.com, and turning the Xbox services back on through the engine's own undo. Write them in `add-xbox-back.md`. Downloads only from Microsoft servers.
- [ ] **Step 2: Test each in the test machine:** restore, apply Max Lean (`nitros apply --card max-lean --yes`), restart, run the candidate with the network on, restart, then check: Store installed and opens, `Microsoft.GamingApp` installed, `GamingServices` running, Game Bar present, Xbox sign-in present, and `nitros health` Game Pass and Store items pass. Record each result. Also run `nitros undo` of the Max Lean journal and record that it does not bring back the removed apps (they are irreversible), which is why the button is needed.
- [ ] **Step 3:** Pick the method that passes every check with the fewest steps. If none passes, stop here: record it, tell the owner the button cannot ship yet, and report NEEDS_CONTEXT. Do not build Steps 4 and 5.
- [ ] **Step 4:** Build it behind an interface:
  - `interface IXboxRestore { XboxRestoreResult Run(IProgress<string>? progress, CancellationToken ct); }`
  - `record XboxRestoreResult(bool Ok, IReadOnlyList<(string Part, bool Ok, string? Detail)> Parts)`
  - `sealed class XboxRestore(LiveSystems systems, IXboxRestoreSteps steps)`, where `IXboxRestoreSteps` is the proven method's steps and `WinXboxRestore` is the real one. It first turns `xbox-services-off`'s services back to their Windows defaults (the values the catalog's actions replaced, read from the catalog, not the journal), then runs the steps, then checks the parts.
  - CLI: `nitros restore-xbox --yes`, refused without `--yes` and when the session guard fails, exit 0 only when every part is back.
  - Tests with fakes: `XboxRestore_RestoresServicesThenApps`, `XboxRestore_ReportsEachPart`, `RestoreXbox_RefusesWithoutYes`, and a `[Trait("Lane","Vm")]` test `XboxRestore_AfterMaxLean_BringsBackStoreAndXbox`.
- [ ] **Step 5:** Run the VM-lane test in the test machine with `Invoke-GuestTests.ps1` after applying Max Lean. Expected: passes.
- [ ] **Step 6:** Run `dotnet test`. Expected: all pass. Commit with the message `Prove and add Add Xbox back`.

---

### Task 14: The measurement campaign (OWNER CHECKPOINT)

**Files:**
- Create: `measure/scripts/Start-Measurement.ps1`, `measure/results/<campaignId>/` (campaign state, run results, journals, `report.md`, `log.txt`)
- Modify: `catalog/*.json` (`measured` only), `catalog/cards.json`, both written by `nitros-measure results --write`

`Start-Measurement.ps1 -Campaign <dir>`:
- Keeps the host awake while it runs with `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` (https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate, checked 2026-10-28). This changes no power setting and ends with the script. It does not stop the power button.
- Loops: `nitros-measure next` (exit 4 ends the loop), `Invoke-MeasureRun.ps1` with the run's ids, then `complete` on exit 0, `fail` on exit 1, and on exit 3 waits 10 minutes and puts the run back with `fail` (it counts as an attempt only when the guest slept).
- Appends one line per run to `log.txt` (run id, result, minutes taken). Ctrl+C stops after the current step; the run stays `Running` and is rerun on the next start.
- **Resume** after anything (Ctrl+C, a host restart, Windows Update restarting the host, a crash): run the same command again. The campaign state on disk says where it was, and every run begins from the clean checkpoint.

- [ ] **Step 1:** Write the script. Run `Publish-GuestTools.ps1`, then `nitros-measure plan --campaign measure/results/<yyyyMMdd>-<build> --catalog catalog --build <guest build from the .vm.json> --cli-commit <commit.txt>`. Expected: 119 runs, or 120 if Task 12 added its switch.
- [ ] **Step 2: OWNER CHECKPOINT.** Tell the owner: "The measurement takes about 40 hours (37 to 47). Keep the Ally on the charger and leave about 5 GB of memory free, so close games and heavy apps. You can use the PC lightly. If it restarts or you stop it, it carries on from where it was." On a yes, start `Start-Measurement.ps1` elevated through UAC in the foreground of its own window. The controller checks `nitros-measure status` when asked, never by touching the test machine.
- [ ] **Step 3:** When it ends, run `nitros-measure status`. Every run must be `Done`. For `Failed` runs, read their errors, fix the cause, `reset` them and run the loop again. A run that cannot pass stays Failed and its switch is listed as not measured.
- [ ] **Step 4:** Run `nitros-measure results --campaign <dir> --catalog catalog` (no `--write`) and read `report.md`. Check: baseline noise band (expected under 50 MB memory and under 3 processes; larger means the runs are not repeatable, so report BLOCKED with the numbers rather than write them), final-baseline drift, every flagged and not-measured switch. Rescan all campaign journals for access-denied failures and add them to Task 10 Step 7's list in `vm-findings.md`.
- [ ] **Step 5:** Run `nitros-measure results --campaign <dir> --catalog catalog --write`. Then run `dotnet test`. Expected: all pass, including `RepoCatalogTests` (the strict loader). `git diff catalog` shows only `measured` objects and `cards.json`.
- [ ] **Step 6:** Commit `measure/scripts/Start-Measurement.ps1`, `measure/results/<campaignId>`, `catalog` with the message `Measure every switch and card in the test machine`.

---

### Task 15: "Fewer service processes" for Gamer and Streamer, by measurement (spec 3)

**Files:**
- Modify: `catalog/background-services.json` (Gamer and Streamer defaults of `svchost-grouping-on` only), `tests/NitrOS.Core.Tests/CatalogGroups/BackgroundServicesTests.cs`, `tests/NitrOS.Core.Tests/CatalogGroups/CardsTableTests.cs`, `docs/superpowers/specs/2026-10-24-nitros-design.md` (the cards table row), `catalog/cards.json` and `measured` fields through the tool, `measure/results/<campaignId>/report.md`

Decision rule, per card (Gamer, then Streamer), using that card's run and its `variant-...-svchost` run:
- **On** when all hold: the variant saves memory beyond the noise band compared with the card run; it has fewer processes; Task 11's background-apps and Task 9's health checks pass on the variant (run `nitros health` on a variant run with the network on, Game Pass item included).
- **Off** otherwise. The source that says it can upset Xbox services counts against On if the Game Pass check fails or `GamingServices` is not running on the variant.

- [ ] **Step 1: OWNER CHECKPOINT.** Run the two health checks on the variants (`Invoke-MeasureRun.ps1 -Network On` plus `nitros health` through a guest task). Record them in `report.md`.
- [ ] **Step 2:** Apply the rule. Write the decision and its numbers in `report.md` and the spec's cards table row ("decided by measurement" becomes on or off with the date).
- [ ] **Step 3:** Change the two defaults in `background-services.json` from `pending` to `on` or `off`. Update `BackgroundServices` and `CardsTableTests` expectations.
- [ ] **Step 4:** Rerun `nitros-measure results --campaign <dir> --catalog catalog --write`, so each card number comes from the run that matches its new selection (the variant when On). Changing the defaults changes the cards' `FromCard` ids; `Compute` checks them against the run it chose, and a matching run exists for both choices.
- [ ] **Step 5:** Run `dotnet test`. Expected: all pass. Commit with the message `Decide fewer service processes for Gamer and Streamer by measurement`.

---

### Task 16: Anti-cheat check (spec 8.3) (OWNER CHECKPOINT, checklist only)

**Files:**
- Create: `docs/testing/anti-cheat-checklist.md`

Anti-cheat often refuses to run in a virtual machine, so this needs the owner's real hardware. The controller writes the checklist and does nothing on the Ally.

- [ ] **Step 1:** Write the checklist, for the owner to run on the Ally when they choose:
  - Before: a restore point; note Memory Integrity, Secure Boot, TPM and VBS state (Settings, Device security), which NitrOS never changes on Gamer and Streamer.
  - Apply Gamer in Tune mode (`nitros apply --card gamer --yes`), restart.
  - For each game the owner has (Valorant with Vanguard, Fortnite with Easy Anti-Cheat, Call of Duty with Ricochet, Battlefield 6 with Javelin, a FACEIT match): launch, reach the main menu, join one match or practice. Record pass or fail and any message, word for word.
  - Repeat with Streamer.
  - Optional, only if the owner wants: Max Lean, expecting failures, to confirm its warning text.
  - On any fail: `nitros undo <journal> --ids <one switch> --yes` one switch at a time from the most likely (Xbox services, background services) and retry, to find what the game needs.
  - Primary requirement pages for BattlEye, Easy Anti-Cheat and FACEIT (spec 11): the owner opens them in their browser and pastes the relevant lines, since the research could not read them.
- [ ] **Step 2:** Commit `docs/testing/anti-cheat-checklist.md` with the message `Add anti-cheat checklist for the owner`. Results come back from the owner and are added later; a game a card breaks becomes a `breaks` line on the switch that caused it, through a normal catalog change with tests.

---

### Task 17: The owner's Ally X, last (spec 8.4) (OWNER CHECKPOINT, checklist only)

**Files:**
- Create: `docs/testing/ally-checklist.md`, `measure/scripts/Test-CatalogKeysWritable.ps1`

This runs only after Tasks 8, 9 and 14 pass, and only when the owner asks. The controller never applies anything on the Ally on its own.

- [ ] **Step 1:** Write `Test-CatalogKeysWritable.ps1`: read-only. For every HKLM key that a live `registry.set`, `registry.delete` or HKLM `startup.disable` action in the catalog writes, outside `SOFTWARE\Policies`, open the nearest existing key with write rights through `RegOpenKeyExW` and close it without writing (the same probe plan 3's engine uses). Output to `%USERPROFILE%\NitrOS-catalog-keys.txt`: key, opened or the Win32 error (notes, Task 11 item: `HKLM\SOFTWARE` refused on the Ally).
- [ ] **Step 2:** Write `docs/testing/ally-checklist.md`, for the owner, in order:
  1. Run `Test-CatalogKeysWritable.ps1` elevated (OWNER CHECKPOINT; it changes nothing). Switches whose keys are refused are listed before anything is applied.
  2. Recommended in Tune mode with a restore point (`nitros apply --card recommended --yes`), restart, `nitros health`. Then Gamer, then Streamer, each from the previous state or after undo, as the owner prefers.
  3. Without Armoury Crate: what the controller, back buttons, Library and Command Center buttons, power button and TDP or fan modes do; how to change the GPU memory reserve without it (spec 11).
  4. Look in the Ally X BIOS for an Armoury Crate download option (notes).
  5. After `armoury-crate-remove`: leave the Ally a day with Windows Update and the Store on; log what returns, and what `ArmouryCrateControlInterface` and `ASUSSoftwareManager` do (`C:\ProgramData\ASUS\ALL` logs) (notes, `reinstall-block.md`).
  6. Dolby and Realtek: remove `dolby-access-remove` and `realtek-audio-console-remove`, restart, then install a newer audio driver from ASUS. Does the app come back with and without `PreventDeviceMetadataFromNetwork` = 1 (`device-companion-apps-off`)? Record `setupapi.dev.log` lines and AppX deployment events (notes, `reinstall-block.md`).
  7. Dolby tuning without the Dolby app: is the Dolby sound profile still applied (listen and check the Sound settings enhancements) (notes)?
  8. Background apps off with the Xbox full-screen experience (Xbox app notifications and the full-screen home) (notes).
  9. Uninstaller windows: did any Armoury Crate or MyASUS uninstaller show a window or wait for input (notes)?
  10. Owner decisions: clear `ExcludeWUDriversInQualityUpdate` (set by Winhance) so Windows Update offers drivers again, or rely on Device updates (plan 7)? Which program installs `C:\WINDOWS\TEMP\D.sys` (service `D`) over and over? (notes)
  11. Measure the real PC: the Tune Done screen numbers, before and after, for plan 5 to compare with the test machine's estimates.
- [ ] **Step 3:** Commit both files with the message `Add Ally X checklist for the owner`. Update `docs/research/notes-for-later-plans.md` and spec section 11: mark every Plan 4 item answered (with the doc that answers it) or moved to the owner checklist.
