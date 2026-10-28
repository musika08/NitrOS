# ASUS Updaters, Signers, and Controller Firmware Research

Date: 2026-10-28 / 2026-10-29.

## 1. Updaters and Silent Switches

ASUS support site listings provide an `ExeModule` field for updates specifying execution parameters:
- **Format:** `<executable_name>%%<arguments>`
- **Typical Arguments:** `/SUPPRESSMSGBOXES /VERYSILENT /NORESTART`
- **Installer Framework:** ASUS packages their BIOS flashers, firmware updates (PD, Touch Panel), and driver bundles primarily using Inno Setup wrappers.
- **Power Requirements:**
  - BIOS and device firmware updates enforce AC mains power (`Ac_power: true` in metadata). Flashing on battery is blocked by the tool to prevent bricking.
  - ASUS BIOS updates stage the payload into UEFI capsule or NVRAM and require an immediate system reboot to execute the firmware flash during POST.

## 2. Signer Verification

All downloads originating from `dlcdnets.asus.com`:
- **Leaf Signer:** `ASUSTeK COMPUTER INC.` (or `ASUSTeK Computer Inc.`)
- **Root / CA:** Digicert or Microsoft trusted root.
- **Revocation:** Validated via online CRL / OCSP checks using WinVerifyTrust (`WTD_REVOKE_WHOLECHAIN`, `WTD_REVOCATION_CHECK_CHAIN`).
- **Policy:** Files whose signer does not match `ASUSTeK COMPUTER INC.` via `Authenticode.NamesMatch` are rejected.

## 3. Controller (MCU) Firmware Reading

Investigation on the ROG Ally X hardware:
- The controller hardware identity is `USB\VID_0B05&PID_1B4C`.
- An "ASUS Component Firmware Update" device exists at `HID\VID_0B05&PID_1B4C&MI_04`.
- Standard read-only PnP device properties (`DEVPKEY_Device_FirmwareVersion`) and registry entries under `HKLM\SYSTEM\CurrentControlSet\Enum` do not expose the MCU firmware version (e.g. V326).
- Querying the version over CFU requires sending vendor-specific HID feature reports to the device endpoint.
- **Conclusion:** To ensure complete hardware safety and avoid unverified communication with controller firmware, NitrOS does not send arbitrary HID feature packets. The MCU version state remains "can't tell", and the ROG MCU Firmware Update tool remains a hand-off package started by the user.

## 4. Summary Conclusions

| Kind | Technology | Documented Switches | Silent Install Proven | Default NitrOS Action |
|---|---|---|---|---|
| **BIOS** | Inno Setup Capsule Flasher | `/SUPPRESSMSGBOXES /VERYSILENT /NORESTART` | No (requires reboot, charger, user caution) | **HandOff** (or launch updater with user confirmation) |
| **PD / Touch FW** | Inno Setup | `/SUPPRESSMSGBOXES /VERYSILENT /NORESTART` | No | **HandOff** |
| **MCU Firmware** | ASUS CFU / Custom Flasher | None in listing | No | **HandOff** |
| **Drivers** | Inno Setup / Package Installer | `/SUPPRESSMSGBOXES /VERYSILENT /NORESTART` | Verified Inno Setup syntax | **HandOff** until proven in VM lane |
