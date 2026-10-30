# NitrOS UI refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Restyle the NitrOS WinUI app as a compact dark cockpit without changing logic.

**Architecture:** One shared `Styles/Theme.xaml` (tokens plus control styles) merged in `App.xaml`; shell and two key pages hand-built against it; remaining pages swap hard-coded values for shared styles.

**Tech Stack:** WinUI 3, .NET 10, xUnit v3. Build: `dotnet build src/NitrOS.App`.

**Spec:** `docs/superpowers/specs/2026-10-30-nitros-ui-refresh-design.md`

## Global Constraints
- Look only: do not edit `NitrOS.App.Logic`, Core, tuner, CLI, or any `x:Bind`/`Command` target names.
- Body text at least 12 px; touch targets at least 40 px; accent gradient cyan `#22D3EE` to violet `#8B5CF6`.
- `CardsPage.OnPickClicked` stays `sender is Button { Tag: CardId }`.
- Motion off when `UISettings.AnimationsEnabled` is false.
- No emojis or em-dashes in any text. Commits: no Claude attribution, owner email only.

## Review Focus
- Long switch names / descriptions wrap or trim without breaking row height.
- Zero results in search shows an empty state, not a blank page.
- High-contrast Windows theme: text stays readable (use theme brushes as fallback).
- Window narrower than 900 px: rail stays collapsed, no clipping.
- Controller focus lands on first card / first switch on page entry.

---

### Task 1: Theme tokens and shared styles
**Files:** Create `src/NitrOS.App/Styles/Theme.xaml`; Modify `App.xaml`, `Styles/Sizes.xaml`.
**Interfaces:** Produces brushes `CockpitWindowBrush`, `CockpitCardBrush`, `CockpitRaisedBrush`, `CockpitStrokeBrush`, `CockpitAccentBrush` (gradient), `CockpitTextPrimary/Secondary/Tertiary`; styles `CockpitCard`, `CockpitCardButton`, `CockpitRow`, `CockpitChip` (Border), `PrimaryButton`, `GhostButton`, `SectionHeader` (TextBlock), `RailButton`.
- [ ] Build App; confirm it fails only if a key is missing (add, then build passes).
- [ ] Launch via smoke runner, screenshot Start page; verify near-black surfaces.
- [ ] Commit.

### Task 2: Shell (rail, header strip, saved readout)
**Files:** Modify `ShellWindow.xaml`, `ShellWindow.xaml.cs`.
**Interfaces:** Consumes Task 1 styles. Produces collapsible rail (64 to 200 px, button toggle, collapsed under 900 px), 40 px header strip.
- [ ] Replace six colored tiles with `RailButton` icons; selected item uses accent pill via `UpdateActiveNav`.
- [ ] Add count-up helper for the readout only if a bound saved-RAM value exists on current page view-models; otherwise omit readout.
- [ ] Build, screenshot collapsed and expanded rail. Commit.

### Task 3: Profiles page
**Files:** Modify `Pages/CardsPage.xaml`.
- [ ] Two-column `ItemsRepeater`/`GridView` of `CockpitCardButton` cards (name, chips, large saving, one detail line); `Tag="{x:Bind Id}"`, `Click="OnPickClicked"`.
- [ ] Screenshot: all five cards visible at 1280x750 without scrolling. Commit.

### Task 4: Switches page
**Files:** Modify `Pages/SwitchesPage.xaml` (and code-behind only for expander toggle).
- [ ] 44 px `CockpitRow` with name, action chip, saving chip, toggle; details in an `Expander` or collapsible panel; "Can't undo" amber chip stays visible.
- [ ] Slim header and bottom bar. Empty-state text when `Groups` is empty.
- [ ] Count visible rows at 1280x750 versus before (target 2x). Commit.

### Task 5: Remaining pages (parallelizable, one helper per group)
**Files:** Modify Start, Review, Done, Working, Health, Drift, Undo, DeviceUpdates, IsoSource, Myths, Blocked, Settings `.xaml`.
- [ ] Replace hard-coded brushes, font sizes and paddings with shared styles; no 11 px text. Build and screenshot each. Commit per group.

### Task 6: Motion and verification
**Files:** Create `src/NitrOS.App/Motion.cs` (static helpers: `CountUp(TextBlock,double)`, `SpringToggle`, guarded by `UISettings.AnimationsEnabled`).
- [ ] Wire count-up and page fade; verify off when animations disabled.
- [ ] Run `dotnet test` for `NitrOS.App.Tests` and full solution; all pass.
- [ ] Contrast check of text on card surfaces (4.5:1). Run EGM `-Diff` audit, then critic loop on screenshots (3 rounds max).
