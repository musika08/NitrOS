# Hyper-V Host Readiness Research

Date: 2026-10-28
Device: ASUS ROG Ally X (RC72LA)

## Host Pre-Check Results (Non-Elevated)

Command: `powershell.exe -ExecutionPolicy Bypass -File measure\scripts\Test-HostReady.ps1`

```
PASS: Windows edition is Pro, Enterprise, or Education (Microsoft Windows 11 Pro).
PASS: Firmware virtualization is enabled.
WARN: Hyper-V is not fully enabled (InstallState: 2, vmms: False, module: False).
WARN: A restart is already pending (CBS: False, WU: False, FileRename: True). Finish it first.
WARN: Shadow Defender is installed. If drive C: or the test machine's drive is in Shadow Mode, turning on Hyper-V and every test machine file are thrown away at restart. Check in Shadow Defender's window that no drive is in Shadow Mode.
PASS: Drive X: has 955.2 GB free space (>= 150 GB).
PASS: Device is connected to AC power.
```

## Observations

1. Windows 11 Pro build 26300 is installed. Virtualization firmware is enabled.
2. Hyper-V optional feature is disabled (`InstallState: 2`), `vmms` service is not installed, and Hyper-V PowerShell module is not yet present.
3. A pending restart is noted (`PendingFileRenameOperations`).
4. Shadow Defender is installed; Shadow Mode must be confirmed off before enabling Hyper-V so changes persist across reboot.
5. Drive X: has ample space (955.2 GB free).
