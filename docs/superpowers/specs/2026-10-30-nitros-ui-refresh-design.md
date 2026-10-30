# NitrOS UI refresh: dark cockpit

Date: 2026-10-30. Status: draft, waiting for the owner's review.

## Goal

Make the app compact, ergonomic, sleek, Apple-like, futuristic and rewarding. Owner picked the **dark cockpit** look (2026-10-30). Look only: no logic, view-model, copy or tuner changes.

## Problems today

- No shared design system: `Styles/Sizes.xaml` has four size tweaks; every page hard-codes font sizes, paddings and brushes.
- Switches page rows are about 90 px (3 to 4 text lines plus chips), so about 110 switches need long scrolling.
- Sidebar is a fixed 230 px column of six differently coloured tiles (#0A84FF, #007AFF, #AF52DE, #34C759, #FF9500, #FF2D55); the rainbow competes with content.
- Profile cards stack full width with chips, notes and a Pick button; five cards need scrolling at 1280x750.
- 11 px grey text is common (low legibility on a handheld).

## Design

### Tokens (new `Styles/Theme.xaml`, merged in `App.xaml` before `Sizes.xaml`)
- Surfaces, three layers: Window `#0B0D12`, Card `#141821`, Raised `#1B2030`. 1 px top highlight `#FFFFFF14`, stroke `#FFFFFF0F`. No heavy borders.
- Accent: one gradient cyan `#22D3EE` to violet `#8B5CF6`; used only for the primary action, the ON toggle, the saved-RAM figure and focus glow.
- Status: caution `#F5A524`, critical `#F31260`, success `#17C964`. Used only for warnings and states.
- Text: primary `#F2F4F8`, secondary `#A3ACBD`, tertiary `#6F7A90`. Nothing below 12 px; body 13 px, row title 14 px, page title 22 px, hero number 34 px.
- Spacing scale 4/8/12/16/24; radius 10 card, 8 control, 999 chip. Minimum touch target 40 px.
- Shared styles: `CockpitCard`, `CockpitRow`, `CockpitChip`, `PrimaryButton`, `GhostButton`, `CockpitToggle`, `SectionHeader`, plus a clear focus ring (2 px accent) for controller and keyboard.

### Shell (`ShellWindow.xaml`, `ShellWindow.xaml.cs`)
- Sidebar becomes a 64 px icon rail that expands to about 200 px on hover or by a toggle button. All six tiles become one monochrome icon style; the selected item gets the accent pill.
- Title strip 40 px (was 44) holds back button, page title and a persistent **saved so far** readout that counts up when switches change. The readout binds to values the view-models already expose (verify during planning; if none exists, show it only on pages that have totals).
- Keep Mica backdrop; window default size stays 1280x750.

### Profiles page (`CardsPage.xaml`)
- 2-column grid of compact cards: name, in-use/recommended chip, big saving figure ("-780 MB"), one detail line. The whole card is a `Button` (style `CockpitCardButton`, `Tag` = card id), so `CardsPage.xaml.cs` `OnPickClicked` (`sender is Button { Tag: CardId }`) stays unchanged and still works for mouse, touch, keyboard and controller. Do not use a Border/Grid tap target (EGM audit 2026-10-30: the handler matches `Button` only). Fits five cards without scrolling at 1280x750.

### Switches page (`SwitchesPage.xaml`)
- Row 44 px: title, small action chip, saving chip, toggle. Description, action detail, risk and unavailable reason move into an expander opened by click or controller A. "Can't undo" stays visible as an amber chip.
- Sticky group headers; header strip slimmed to search plus profile chip; bottom bar slimmed to live total plus Review button.
- Target: at least double the switches visible per screen versus today.

### Other pages
Start, Review, Done, Working, Health, Drift, Undo, DeviceUpdates, IsoSource, Myths, Blocked, Settings: restyled to the shared styles only (cards, buttons, text sizes, spacing). Layout changes only where a page has the same density problem.

### Motion
- Toggle: spring about 150 ms. Saved-RAM number counts up about 400 ms. Selected profile card glow pulse once. Page change: 120 ms fade plus 8 px rise.
- All motion off when Windows animations are off (`UISettings.AnimationsEnabled`).
- No blur beyond Mica; no per-frame work on idle pages (handheld battery).

## Files touched
New: `src/NitrOS.App/Styles/Theme.xaml`.
Edited: `App.xaml`, `Styles/Sizes.xaml`, `ShellWindow.xaml`, `ShellWindow.xaml.cs` (rail expand, readout only), `Pages/CardsPage.xaml`, `Pages/SwitchesPage.xaml`, and the other 12 page `.xaml` files for styles. Possibly small code-behind additions for expander and count-up animation helper. Not touched: `NitrOS.App.Logic`, Core, tuner, CLI.

## How we prove it works
1. `dotnet build` of the App project with no warnings added; `dotnet test` for `NitrOS.App.Tests` and the full suite still passes (no logic changed, so counts must not drop).
2. `SmokeRunner` already renders a PNG per screen; capture before and after for every page and compare. Close-up of the changed elements (rail, a card, a switch row, bottom bar) per page, not only the page top.
3. Measured density: count switch rows visible at 1280x750 before and after (target at least 2x).
4. Contrast check: primary and secondary text against Card and Window at 4.5:1 or better.
5. Controller/keyboard: tab/D-pad through Profiles and Switches, focus ring always visible, no 40 px target missed.
6. Reduced motion: turn Windows animations off, confirm no animation runs.

## Process
Per owner rules: EGM audit of this spec by the other company's model before coding (`~/.egm/audit.ps1 -Reviewer gemini`), `-Diff` audit before commit, then the Gauntlet loop on page screenshots (3 rounds max, stop early if the score stalls). Nothing is committed without the owner's OK.

## Open items for the owner
- Rail expand: hover or button? Default proposed: button toggle (hover is not controller-friendly).
- Keep or drop the "Myths we tested" shortcut button on the Switches header (proposed: move it into the sidebar only).
