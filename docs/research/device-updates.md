# Device updates without the maker's app

Research 2026-10-24.

## ASUS (checked live)

ASUS's support site serves per-model listings as JSON. The same endpoints are
used by the ASUS site itself and by G-Helper (seerge/g-helper, GPL-3.0, file
`app/Updates.cs`; NitrOS writes its own code and copies none of it):

- BIOS: `https://rog.asus.com/support/webapi/product/GetPDBIOS?website=global&model=<MODEL>&cpu=<MODEL>&systemCode=rog`
- Drivers: `https://rog.asus.com/support/webapi/product/GetPDDrivers?website=global&model=<MODEL>&cpu=<MODEL>&osid=52&systemCode=rog`

Not officially documented; ASUS can change them.

For the owner's Ally X (`RC73XA`), queried 2026-10-24, the listing returned:

| Item | Latest listed | Installed |
|---|---|---|
| BIOS (Windows updater) | 319, 2026-10-14 | RC73XA.319 |
| ROG MCU Firmware Update tool (controller) | V326, 2026-10-19 | unknown (no way found yet to read it without Armoury Crate) |
| Touch panel firmware | 2002, 2025-11-17 | not checked |
| AMD graphics driver | 32.0.32015.5008, 2026-10-16 | 32.0.32015.7029 (newer, from AMD) |
| MediaTek Wi-Fi | 3.6.2.1438 | 3.6.2.1438 |
| MediaTek Bluetooth | 1.1147.2.620 | 1.1147.2.620 |
| Genesys card reader | 1.2.31.0 | 1.2.31.0 |
| FocalTech fingerprint | 4.5.7901.6673 | 4.5.7901.6673 |
| Bosch G-sensor | 2.0.4.1 | 2.0.4.1 |
| TI smart amplifier | 3.1.52.0 | 3.1.52.0 |
| Realtek audio | 6.0.9882.1 | 6.0.9882.1 |

Download links are on `dlcdnets.asus.com`. Some ASUS items (BIOS 314,
power-delivery firmware) also came through Windows Update.

The `ASUS System Control Interface v3` driver (3.1.72.0 installed) is what
power and fan control use. It must stay when Armoury Crate is removed.

## 2026-10-28 Live Hardware & WUA Check on Ally X

- **Policy check:** `ExcludeWUDriversInQualityUpdate` = 1 is set on the host. A WUA search (`IsInstalled=0 and Type='Driver'`) still successfully returns available drivers and firmware items (e.g. Realtek USB network, ITE PD firmware 10.1.2.20, ASUS system firmware 10.1.2.319). Policy prevents automatic Windows Update driver downloads but does not block manual discovery.
- **ESRT Resources:**
  - System Firmware `{68e0fa36-5d2a-5046-91c6-e0bfe84f77f3}`: Version 793 (0x319, BIOS `RC73XA.319`).
  - ITE PD `{cbf5a488-f654-3fce-b3fc-8a1d94ead498}`: Version 21 (0x15).
  - NOVATEK TouchPanel `{222a79d6-d2aa-358b-b1ac-8771faf8fec0}`: Version 9.
  - Device Firmware `{22cc2999-e6fd-3ea9-800c-cff443d45585}`: Version 1.

## Other sources (web research, not yet tried)

- **Windows Update, all brands:** Windows Update Agent API (`IUpdateSearcher`,
  driver updates via `IWindowsDriverUpdate`),
  https://learn.microsoft.com/en-us/windows/win32/api/wuapi/
- **Dell:** public catalogs under `dl.dell.com/catalog` (CatalogPC.cab,
  CatalogIndexPC.cab), https://dell.com/support/kbdoc/en-in/000133115/dell-command-update-catalog
- **Lenovo:** catalog XML files used by Lenovo System Update, mainly business
  models; Legion Go coverage unconfirmed. Lenovo pulled Legion Go BIOS
  N3CN42WW in 2026 after boot failures,
  https://hwbusters.com/news/lenovo-pulls-the-legion-go-bios-that-was-bricking-handhelds-and-says-nothing-about-it/
- **HP:** HP Image Assistant catalog, not confirmed.
- **MSI Claw:** BIOS 109 and later update through MSI Center M Live Update;
  no public listing found.
- **GPU vendors:** AMD, NVIDIA, Intel direct; method to be verified.
