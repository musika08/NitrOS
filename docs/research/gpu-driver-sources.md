# GPU Vendor Driver Sources Research

Date: 2026-10-28 / 2026-10-29.

## Overview

Investigation of whether AMD, NVIDIA, and Intel publish machine-readable (JSON/XML) APIs for determining the latest driver version for a specific GPU device ID or product line.

---

## 1. AMD

- **Mechanism:** AMD does not offer a public or documented REST/JSON API for querying GPU driver updates.
- **Auto-Detect Utility:** AMD uses a proprietary, closed-source client executable (`AMD Auto-detect and Install` / `amdupdaterconstructor.exe`) that reads PCI vendor/device IDs locally and communicates with private backend services over TLS.
- **Web Pages:** AMD's driver selector forms are rendered with Akamai/Cloudflare protection and dynamic front-end scripts, requiring interactive browser sessions.
- **Handheld Support (Ally X / Radeon 890M):**
  - The ROG Ally X uses the AMD Radeon 890M Graphics (`PCI\VEN_1002&DEV_150E&SUBSYS_33591043&REV_C7`).
  - AMD's generic Adrenalin driver packages (e.g. version 32.0.32015.7029) install and run on the Ally X, providing a newer version than ASUS's packaged OEM driver (`32.0.32015.5008`).
  - However, OEM handheld features (custom power profiles, Armoury Crate overlays, or ASUS-specific display timings) are tuned by ASUS in their OEM packages.
- **Verdict:** **Page only**
  - URL: `https://www.amd.com/en/support/download/drivers.html`

---

## 2. NVIDIA

- **Mechanism:** NVIDIA has an undocumented HTTP AJAX endpoint used by GeForce Experience and the legacy website search form:
  - Step 1: Mapping lookup values via `https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID={type_id}&ParentID={parent_id}`
  - Step 2: Driver query via `https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid={psid}&pfid={pfid}&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=1&numberOfResults=1`
- **Fields in Response:**
  - `IDS[0].downloadInfo.Version`: e.g. "572.83"
  - `IDS[0].downloadInfo.ReleaseDate`: e.g. "Ver. 572.83"
  - `IDS[0].downloadInfo.DownloadURL`: e.g. `https://us.download.nvidia.com/Windows/.../...-desktop-win10-win11-64bit-international-dch-whql.exe`
- **Limitations:**
  - The API does not accept arbitrary PCI hardware IDs directly; it requires proprietary numeric product series (`psid`) and product family (`pfid`) IDs.
  - The endpoint is undocumented, unsupported for third-party use, and subject to change or rate-limiting.
- **Verdict:** **Page only** (per Spec 11 rule: without official documentation and direct hardware ID mapping, fallback to official download page)
  - URL: `https://www.nvidia.com/en-us/drivers/`

---

## 3. Intel

- **Mechanism:** Intel does not publish a public REST/JSON API for Driver and Support Assistant (DSA).
- **Intel DSA Architecture:**
  - Runs as a local service on `localhost` (TCP ports 28385–28389).
  - Uses private cryptographic tokens to talk to `dsadata.intel.com` and `downloadmirror.intel.com`.
  - External HTTP requests to web endpoints return HTTP 403 Forbidden to non-browser / non-authenticated scripts.
- **Verdict:** **Page only**
  - URL: `https://www.intel.com/content/www/us/en/download-center/home.html`

---

## Summary Verdict Table

| Vendor | Machine-Readable API | Reliable / Documented | Verdict | Support / Download Page URL |
|---|---|---|---|---|
| **AMD** | None (closed Auto-detect app only) | No | **Page only** | `https://www.amd.com/en/support/download/drivers.html` |
| **NVIDIA** | Undocumented GFE AJAX (`AjaxDriverService.php`) | No (requires internal numeric IDs) | **Page only** | `https://www.nvidia.com/en-us/drivers/` |
| **Intel** | None (DSA local agent required; 403 on direct HTTP) | No | **Page only** | `https://www.intel.com/content/www/us/en/download-center/home.html` |
