# ASUS ROG Xbox Ally X Verification Checklist (Host Execution)

This checklist verifies NitrOS on the actual hardware (ASUS ROG Xbox Ally X, RC73XA / RC72LA, Windows 11 build 26300).
It is executed only when the owner chooses. The assistant never applies changes to the host hardware autonomously.

---

## 1. Registry Permissions Pre-Check (Elevated, Read-Only)

Run the non-modifying registry write probe:
```powershell
& "X:\Projects\NitrOS\measure\scripts\Test-CatalogKeysWritable.ps1"
```
Review `%USERPROFILE%\NitrOS-catalog-keys.txt`.
Any HKLM keys that return `DENIED` must be examined before applying switches that write them.

---

## 2. Progressive Card Testing (Tune Mode)

Execute in order with manual verification:
1. **Recommended Card:**
   - Create system restore point.
   - Run: `nitros apply --card recommended --yes`
   - Restart: `shutdown /r /t 0`
   - Run health probe: `nitros health` (Confirm exit 0).
2. **Gamer Card:**
   - Run: `nitros apply --card gamer --yes`
   - Restart and verify: `nitros health`.
3. **Streamer Card:**
   - Run: `nitros apply --card streamer --yes`
   - Restart and verify: `nitros health`.

---

## 3. Hardware Behavior Without Armoury Crate

Apply `armoury-crate-remove` in a test run and check:
- **Game Controller & Back Buttons (M1/M2):** Do standard gamepad inputs and macro remappings function in Steam Input / Windows Gaming API?
- **Command Center & Armoury Crate Buttons:** What happens when pressed? (Does Overdrive or Decky catch them, or does `ArmouryCrateKeyControl.exe` need to be retained?)
- **TDP and Fan Modes:** Can power profiles be switched without AC? (e.g. via G-Helper, Handheld Companion, or ACPI power schemes).
- **VRAM Reserve Allocation:** How to adjust GPU memory allocation outside Armoury Crate (verify if accessible directly in UEFI BIOS under Advanced -> UMA Frame Buffer Size).

---

## 4. UEFI BIOS Armoury Crate Setting

1. Reboot to UEFI BIOS (Hold Volume Down + Power during boot).
2. Check `Advanced` / `Tool` menus for an option named `Download & Install ARMOURY CRATE app` or similar ASUS ASUS9001 WPBT injection setting.
3. Record presence and default state.

---

## 5. Armoury Crate Reinstallation Check

1. After `armoury-crate-remove`, leave the Ally connected to the internet with Windows Update and Microsoft Store enabled for 24 hours.
2. Monitor `C:\ProgramData\ASUS\ALL` and `C:\Windows\System32\ASUSACCI`.
3. Verify whether `ArmouryCrateControlInterface` or `ASUSSoftwareManager` triggered a download or silent re-provisioning of Armoury Crate SE.

---

## 6. Audio Driver Reinstallation & Companion App Block

1. Apply `dolby-access-remove` and `realtek-audio-console-remove`.
2. Restart the Ally.
3. Install an updated or reinstalled Realtek/Dolby audio driver package from ASUS.
4. Test with:
   - `PreventDeviceMetadataFromNetwork = 0` (default)
   - `PreventDeviceMetadataFromNetwork = 1` (`device-companion-apps-off`)
5. Inspect `C:\Windows\INF\setupapi.dev.log` and Event Viewer `Applications and Services Logs > Microsoft > Windows > AppXDeployment-Server > Operational`.
6. Record whether `dax3_swc_hsa.inf` or `realtekhsa.inf` triggered reinstallation of the Store apps.

---

## 7. Dolby Atmos Tuning State

1. With Dolby Access app uninstalled:
2. Open Windows Sound Settings -> Speakers -> Audio enhancements.
3. Verify if Dolby processing is still active in the audio driver processing object (APO) stack or if sound profile reverted to generic stereo.

---

## 8. Background Apps Off & Xbox Full-Screen Experience

1. Apply `background-apps-off` and restart.
2. Launch Xbox app and test Xbox full-screen / compact mode.
3. Verify if game invitations, party chat notifications, and controller navigation operate properly.

---

## 9. Uninstaller Window Behavior

1. Observe `armoury-crate-remove` and `myasus-remove` uninstaller execution.
2. Record whether any uninstaller dialog prompted for user interaction or displayed a progress window.

---

## 10. Owner Host System Decisions

1. **Windows Update Driver Exclusion:**
   - Current state: `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\ExcludeWUDriversInQualityUpdate = 1` (configured by Winhance).
   - Decision: Clear to restore Windows Update driver delivery, or leave enabled and rely on NitrOS Device Updates (Plan 7).
2. **Transient `D.sys` Service:**
   - Investigate which local process generates `C:\WINDOWS\TEMP\D.sys` (service `D`).

---

## 11. Real PC RAM & Telemetry Benchmark

1. Run `nitros detect` and record:
   - Memory installed, memory available to Windows, and reserved for GPU.
2. Compare live RAM usage before and after applying the Gamer card:
   - Initial Windows 11 idle RAM usage.
   - Post-tuning Windows 11 idle RAM usage.
   - Compare with Hyper-V measurement estimates.
