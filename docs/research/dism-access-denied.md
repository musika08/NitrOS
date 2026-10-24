# Why DISM's provisioned-app query is denied on the owner's PC

Research 2026-10-25 (plan 3, task 11). Read-only. Raw output is saved under
`docs/research/evidence/`, and each claim below names its file. "Checked
live, output not saved" marks anything without one. Nothing on the PC was
changed.

## Short answer

DISM's Appx provider **probably** fails opening a registry key for writing.
The log line is `Failed to create or open Software key. -
CAppxManager::InitializeResources(hr:0x80070005)`, and it names neither the
key nor the access asked for. Feature and capability queries work. That
they never reach this code is inferred, because they never log the
"Software key" step. The same query still works when it runs as **SYSTEM**
(Winhance's logon task), and fails for the elevated administrator. The
elevated token is also refused write access to `HKLM\SOFTWARE` itself
(confirmed by a second elevated run, Win32 error 5), even though the
permissions grant administrators full control and no integrity label applies.
`HKLM\SOFTWARE\Policies` is writable (see 3).

The likely cause is **Shadow Defender**, installed 2026-10-20 at 12:50,
between the last logged admin success (10-20 03:23) and the first failure
(10-24 17:33). Its manual describes registry-level handling. This rests on
timing and is not proven.

NitrOS must detect the refusal and tell the user. It must never change
permissions, ownership or Shadow Defender's settings to get around it.

## Evidence

### 1. Where it fails

Source: `evidence/elevated-dism-evidence-2026-10-25.txt` (the controller's
elevated run of `tools/verify/collect-dism-evidence.ps1`, 2026-10-25 02:12,
"Elevated admin: True") and `evidence/dism-log-excerpts.txt`.

```
DISM Appx Provider: Failed to create or open Software key. - CAppxManager::InitializeResources(hr:0x80070005)
DISM Appx Provider: Failed to initialize members. - CAppxManager::GetAllUserAppx(hr:0x80070005)
DISM Appx Provider: Failed to get all provisioned Appx packages collection - CAppxManager::ProcessCommandGetAllUserAppx(hr:0x80070005)
DISM Appx Provider: Failed processing command to get Appx packages - CAppxManager::ExecuteCmdLine(hr:0x80070005)
```

`dism /English /Online /Get-ProvisionedAppxPackages` prints "Error: 5,
Access is denied" and exits with 5. `Get-AppxProvisionedPackage -Online`
throws `COMException`, HResult 0x80070005. In the same run,
`/Get-FeatureInfo /FeatureName:Printing-PrintToPDFServices-Features` exits
with 0. DISM is 10.0.26100.9549, servicing image 10.0.26300.9711.

### 2. Who succeeds and who fails

Source: `evidence/dism-log-excerpts.txt` (callers grouped, every 0x80070005
line, and the 10-20, 10-23 and 10-24 excerpts). The task's account comes from
`evidence/elevated-dism-evidence-2026-10-25.txt`.

| When | Caller | Account | Result |
|---|---|---|---|
| 10-19 16:56 to 10-24 17:03 (94 runs, plus 6 earlier with `-File`) | Winhance `BloatRemoval.ps1` | `\Winhance\BloatRemoval` task: SYSTEM, Highest, at logon | Works; 10-24 17:03:56 "Successfully removed package" `Microsoft.Office.ActionsServer` |
| 10-20 03:23 | `powershell.exe -Command "Get-AppXProvisionedPackage -Online ... Remove-AppxProvisionedPackage -Online"` | Not logged; most likely an elevated admin (unverified) | No error logged |
| 10-20 03:37 and 10-23 23:58 | `dism /online /Add-ProvisionedAppxPackage` (the ASUS installer, and a `cmd.exe` caller) | Not logged | No error logged; adding may use a different code path from listing, so these do not decide the question |
| 10-24 17:33 | NitrOS `collect-identifiers.ps1` | Elevated admin | 0x80070005 |
| 10-24 17:34, 10-25 02:12 | `dism.exe` and the evidence script | Elevated admin | 0x80070005 |

All 0x80070005 lines in the log are from the elevated-admin runs.

### 3. Registry: which keys the elevated token may open for writing

Source: `evidence/elevated-dism-evidence-2026-10-25-rerun.txt` (the
controller's second elevated run of the updated script, 2026-10-25 02:24) and
the first run in `evidence/elevated-dism-evidence-2026-10-25.txt`.
`evidence/registry-sddl-labels.txt` has the permissions (SDDL) and integrity
labels, read without elevation.

The script opened each key with read, set-value and create-subkey rights and
closed it without writing anything. The first run logged only PowerShell's
wrapper exception. The re-run logs the real error in two ways: the .NET
inner exception, and a direct `RegOpenKeyExW` that returns the Win32 error
code. The refusals are **confirmed**.

| Key (HKLM) | Result (confirmed) | Administrators in the permissions |
|---|---|---|
| `SOFTWARE` | refused: Win32 error 5 (Access is denied); .NET `SecurityException` 0x8013150A | Full control, this key and subkeys (`(A;CI;KA;;;BA)`) |
| `SOFTWARE\Policies` | write OK (`RegOpenKeyExW`) | not read |
| `...\CurrentVersion\Appx` | refused: Win32 error 5; `SecurityException` 0x8013150A | Read only; owner TrustedInstaller |
| `...\Appx\AppxAllUserStore` | write OK | Full control; owner Administrators |
| `...\AppxAllUserStore\Applications`, `Deprovisioned`, `Staged`, `Config` | write OK | Full control |
| `...\AppxAllUserStore\InboxApplications` | refused: Win32 error 5; `SecurityException` 0x8013150A | Read only; owner TrustedInstaller |
| `...\CurrentVersion\AppModel` | refused: Win32 error 5; `SecurityException` 0x8013150A | Read only; owner TrustedInstaller |
| `...\CurrentVersion\AppReadiness` | write OK | not read |

The refusals on `Appx`, `InboxApplications` and `AppModel` match their
permissions. Whether a clean Windows 11 sets them the same way is unverified
(plan 4 VM).

The refusal on `HKLM\SOFTWARE` does not match its permissions:
administrators have full control there, and the integrity label is
`S:NO_ACCESS_CONTROL` (no label). Something below the permissions, such as
a kernel registry filter, is refusing the request. Its subkey
`HKLM\SOFTWARE\Policies` is writable, and that is where most catalog policy
switches write. Plain `HKLM\SOFTWARE` values outside `Policies` may fail on
this PC.

The token is normal: Administrators enabled, High Mandatory Level, the usual
admin privileges (`evidence/elevated-dism-evidence-2026-10-25.txt`, user name
and SID redacted).

### 4. What changed between the last admin success and the first failure

Source: `evidence/system-7045-services.txt` (System log event 7045, grouped).

- **2026-10-20 12:50:50: Shadow Defender Service**
  (`C:\Program Files\Shadow Defender\Service.exe`). Its `user.dat` was last
  written 10-20 12:57 (same file).
- Also new in the window: Armoury Crate and ASUS services (10-20 03:28 to
  03:44), Gaming Services, GameInput, `gameflt`, XVDD, PostgreSQL, Claude,
  Edge, an AMD driver, Easy Anti-Cheat and CPU-Z's `cpuz163` driver (loaded
  on demand).

Shadow Defender's driver `diskpt.sys` is "Shadow Defender Filter Driver"
1.5.0.726, SHADOWDEFENDER.COM (`evidence/system-7045-services.txt`). It starts
at boot, is an upper filter on the disk class (`partmgr,diskpt`) and a lower
filter on the volume class (`fvevol,diskpt,iorate,rdyboost`), and `fltmc`
lists it as a legacy filter (`evidence/elevated-dism-evidence-2026-10-25.txt`).
Its manual's "Registry Exclusion List" page says: "When accessing these
registry items, the changes will automatically be saved to the original
volume" (`evidence/shadow-defender-manual.txt`). So it handles the registry
itself, not only the disk. The manual says nothing about refusing access,
DISM or Windows servicing.

Also seen: a kernel driver named `D`, file `C:\WINDOWS\TEMP\D.sys`, had
1,139 installs from 2026-10-20 03:02:16 to 2026-10-24 15:03:12 (inside this
window), and 1,395 events for service `D` from 2026-10-19 17:51:23 to
2026-10-24 23:03:07. Source: `evidence/system-7045-services.txt`. It was
already being installed before the 10-20 03:23 admin run that worked, so it
is a weaker suspect. Which program
loads it is unknown (open question for the owner).

### 5. What was ruled out

- **Not elevation.** The failing runs are elevated (High Mandatory Level).
- **Not the AppX services.** AppXSvc and StateRepository are running (checked
  live, output not saved), and the SYSTEM task gets the list on the same
  boot.
- **Not a file system minifilter.** `fltmc filters` shows only Windows
  filters plus the legacy `diskpt`
  (`evidence/elevated-dism-evidence-2026-10-25.txt`).
- **Not an integrity label, and not the permissions on `HKLM\SOFTWARE`**
  (`evidence/registry-sddl-labels.txt`).

### Not proven

- Which key the Appx provider opens, and with what access. The log says only
  "Software key".
- What refuses the `HKLM\SOFTWARE` write-open. The refusal itself is
  confirmed (Win32 error 5).
- That Shadow Defender is what refuses it. Ways to confirm: (a) on the plan 4
  VM, run the evidence script before and after installing Shadow Defender
  1.5.0.726; (b) on the Ally, the owner turns off or removes Shadow Defender
  and the script runs again. NitrOS must not do (b) itself.
- Whether Shadow Mode is on. `CmdTool.exe /pwd: /list` printed nothing, and
  the tool's help does not say what an empty result means.

## What NitrOS must do

1. **Detect.** Before an `appx.remove` that needs the provisioned list, and
   before reporting "not on this PC", run the provisioned query once. Treat
   0x80070005 (DISM exit code 5, or `COMException` HResult 0x80070005) as
   **"Windows refused to list built-in apps"**, never as "no packages".
   This matches plan 3's `DISM access denied` detail in `WinDism`, and the
   same rule belongs in `WinAppx`.
2. **Name the likely cause when it can see it.** Shadow Defender shows up
   read-only as the service whose path is under `C:\Program Files\Shadow
   Defender\`, or as the boot driver `diskpt` (file description "Shadow
   Defender Filter Driver"). If it is there, tell the user, for example:
   "Windows refused to change built-in apps. Shadow Defender is installed and
   may be blocking it. Turn it off or remove it, restart, and run NitrOS
   again." If not, say that a security or snapshot tool may be blocking it.
3. **Carry on with what still works.** Removing apps for the current user,
   and DISM features and capabilities, do not need the provisioned list. Mark
   only the "new users" (provisioned) part of each app switch as not done,
   with the reason.
4. **Test before writing to HKLM.** Before a run, open each HKLM target key
   for writing without writing anything. If a key is refused, report it before
   applying, so the user does not get a half-applied card.
5. **Warn about Shadow Mode.** If Shadow Defender reports a drive in Shadow
   Mode, every NitrOS change on that drive is thrown away at restart. Say so
   before applying. (How to read that reliably is unverified.)
6. **Never "fix" it.** No taking ownership, no permission changes, no
   TrustedInstaller or SYSTEM tricks, and no changes to Shadow Defender
   settings, services or drivers. That decision is the user's.
