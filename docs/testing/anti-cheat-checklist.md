# NitrOS Anti-Cheat Compatibility Checklist (ROG Xbox Ally X)

Anti-cheat systems (Vanguard, Easy Anti-Cheat, BattlEye, Ricochet, FACEIT) frequently refuse to operate in virtual machines. This checklist is run directly on the host hardware (ASUS ROG Xbox Ally X) by the owner at their discretion.

NitrOS Gamer and Streamer cards are designed to keep anti-cheat games completely functional. NitrOS never modifies Secure Boot, TPM 2.0, or IOMMU, and preserves Memory Integrity (HVCI) and Virtualization-Based Security (VBS) states.

---

## 1. Baseline Pre-Flight Check

Before making changes:
1. Verify device security features in Windows Settings (`Settings > Privacy & security > Windows Security > Device security > Core isolation`):
   - Memory Integrity (HVCI): Note state (On/Off).
   - Virtualization-based security (VBS): Note state (Running).
   - Secure Boot: State (On).
   - Security Processor (TPM 2.0): State (Ready).
2. Create a manual System Restore point:
   - System Properties -> System Protection -> Create -> Name `Pre-NitrOS-AntiCheat`.

---

## 2. Test Run: Gamer Card

1. Apply Gamer card with NitrOS CLI:
   ```powershell
   nitros apply --card gamer --yes
   ```
2. Restart Windows:
   ```powershell
   shutdown /r /t 0
   ```
3. Test each installed game:
   - **Valorant (Riot Vanguard):** Launch, reach main menu, enter Practice Range or Deathmatch.
     - Result: `[PASS / FAIL]` (Record any error dialog word-for-word).
   - **Fortnite (Easy Anti-Cheat / BattleEye):** Launch, reach lobby, drop into match.
     - Result: `[PASS / FAIL]`
   - **Call of Duty (Ricochet):** Launch, connect to servers, enter match.
     - Result: `[PASS / FAIL]`
   - **Battlefield (EA Anti-Cheat / Javelin):** Launch, enter practice or multiplayer.
     - Result: `[PASS / FAIL]`
   - **FACEIT (FACEIT AC):** Launch client, queue match or warm up.
     - Result: `[PASS / FAIL]`

---

## 3. Test Run: Streamer Card

1. Apply Streamer card:
   ```powershell
   nitros apply --card streamer --yes
   ```
2. Restart Windows.
3. Rerun game launch checks above and record pass/fail.

---

## 4. Optional: Max Lean Card (Expected Failure Verification)

Max Lean turns Memory Integrity off and strips Xbox components.
1. Apply Max Lean:
   ```powershell
   nitros apply --card max-lean --yes
   ```
2. Restart Windows.
3. Confirm anti-cheat displays expected incompatibility or error message.

---

## 5. Troubleshooting / Root-Cause Isolation

If any anti-cheat game fails on Gamer or Streamer:
1. Identify newest journal:
   ```powershell
   nitros journals
   ```
2. Undo suspect switches individually:
   ```powershell
   nitros undo <journalId> --ids xbox-services-off --yes
   nitros undo <journalId> --ids svchost-grouping-on --yes
   ```
3. Retry game to identify exact conflicting switch. Report switch ID so catalog `breaks` metadata can be updated.

---

## 6. Official Vendor Anti-Cheat Requirements (Reference Pages)

Open in browser and record system requirement constraints:
- **BattlEye FAQ & Requirements:** https://www.battleye.com/support/faq/
- **Easy Anti-Cheat Support:** https://www.easy.ac/en-us/support/
- **FACEIT Anti-Cheat Requirements:** https://support.faceit.com/hc/en-us/articles/360014237259-FACEIT-Anti-Cheat-Information
