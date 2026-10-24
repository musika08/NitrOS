# Why DISM's provisioned-app query is denied on the owner's PC

Research 2026-10-25 (plan 3, task 11). Read-only. Evidence comes from
`C:\Windows\Logs\DISM\dism.log`, the event logs, the registry and one
elevated run of `tools/verify/collect-dism-evidence.ps1` (2026-10-25 02:12,
"Elevated admin: True"). Nothing on the PC was changed.

## Short answer

The query fails in DISM's Appx provider when it opens a registry key for
writing: `Failed to create or open Software key. -
CAppxManager::InitializeResources(hr:0x80070005)`. Feature and capability
queries never open that key, so they work. The same query still works when it
runs as **SYSTEM** (Winhance's logon task does it), and fails only for the
elevated administrator. The registry permissions do grant administrators
write access to `HKLM\SOFTWARE`, and there is no integrity label in the way,
yet the elevated token is refused when it asks to write there. Something
below the permissions is refusing it.

The likely cause is **Shadow Defender**. It was installed on 2026-10-20 at
12:50, between the last time an admin query worked (10-20 03:23) and the
first failure (10-24 17:33). Its manual says it can save chosen registry
changes past Shadow Mode, so it works at the registry level. This is a strong
lead from timing, not proof: nobody has tested with Shadow Defender removed.

NitrOS must detect the refusal and tell the user. It must never change
permissions, ownership or Shadow Defender's settings to get around it.

## Evidence

### 1. Where it fails (dism.log)

Every failing run logs the same four lines. The 2026-10-25 02:12 elevated
run:

```
DISM Appx Provider: Failed to create or open Software key. - CAppxManager::InitializeResources(hr:0x80070005)
DISM Appx Provider: Failed to initialize members. - CAppxManager::GetAllUserAppx(hr:0x80070005)
DISM Appx Provider: Failed to get all provisioned Appx packages collection - CAppxManager::ProcessCommandGetAllUserAppx(hr:0x80070005)
DISM Appx Provider: Failed processing command to get Appx packages - CAppxManager::ExecuteCmdLine(hr:0x80070005)
```

`dism /English /Online /Get-ProvisionedAppxPackages` prints "Error: 5,
Access is denied" and exits with 5. `Get-AppxProvisionedPackage -Online`
throws `COMException`, HResult 0x80070005. In the same run,
`/Get-FeatureInfo /FeatureName:Printing-PrintToPDFServices-Features`
exits with 0.

DISM is 10.0.26100.9549 servicing image 10.0.26300.9711. The log does not
name the key.

### 2. Who succeeds and who fails (dism.log callers)

| When | Caller | Account | Result |
|---|---|---|---|
| 10-19 16:38 to 10-24 17:03 (100 runs) | Winhance `BloatRemoval.ps1` | `\Winhance\BloatRemoval` task: SYSTEM, Highest, at logon | Works; 10-24 17:03:56 it also deprovisioned `Microsoft.Office.ActionsServer` |
| 10-20 03:23 | `powershell.exe -Command "Get-AppXProvisionedPackage -Online ... Remove-AppxProvisionedPackage -Online"` | Most likely an elevated admin (not recorded) | No error logged |
| 10-24 17:33 | NitrOS `collect-identifiers.ps1` | Elevated admin (`elevated: true` in its output) | 0x80070005 |
| 10-24 17:34, 10-25 02:12 | `dism.exe` / evidence script | Elevated admin | 0x80070005 |

All the 0x80070005 lines in the log come from the elevated-admin runs.

### 3. Registry: what the elevated token may open for writing

The evidence script opened each key with read, set-value and create-subkey
rights, then closed it without writing anything:

| Key (HKLM) | Result | Administrators in the permissions |
|---|---|---|
| `SOFTWARE` | DENIED | Full control, this key and subkeys (`(A;CI;KA;;;BA)`) |
| `...\CurrentVersion\Appx` | DENIED | Read only; owner TrustedInstaller |
| `...\Appx\AppxAllUserStore` | WRITE-OK | Full control; owner Administrators |
| `...\AppxAllUserStore\Applications`, `Deprovisioned`, `Staged`, `Config` | WRITE-OK | Full control |
| `...\AppxAllUserStore\InboxApplications` | DENIED | Read only; owner TrustedInstaller |
| `...\CurrentVersion\AppModel` | DENIED | Read only; owner TrustedInstaller |
| `...\CurrentVersion\AppReadiness` | WRITE-OK | |

The denials on `Appx`, `InboxApplications` and `AppModel` are explained by
their permissions: TrustedInstaller owns them and administrators can only
read. Whether that is how a clean Windows 11 sets them is unverified (check
on the VM in plan 4).

The denial on `HKLM\SOFTWARE` is **not** explained by its permissions:
administrators have full control there. Reading the integrity labels (no
elevation needed) shows `S:NO_ACCESS_CONTROL` on `SOFTWARE`, so no
integrity label blocks a high-integrity administrator either. Something that
sits below the permissions, such as a kernel registry filter, is refusing the
request.

The token itself is normal: `BUILTIN\Administrators` enabled, High Mandatory
Level, the usual admin privileges. `SeBackupPrivilege` and
`SeRestorePrivilege` are present but disabled, as usual.

### 4. What changed between the last admin success and the first failure

The new services in the System log (event 7045) between 2026-10-20 03:23 and
2026-10-24 17:33:

- **2026-10-20 12:50:50: Shadow Defender Service** (`C:\Program Files\Shadow
  Defender\Service.exe`). Its settings file `user.dat` was last written
  10-20 12:57.
- Armoury Crate and ASUS services (10-20 03:28 to 03:44), Gaming Services,
  GameInput, `gameflt`, XVDD, PostgreSQL, Claude, Edge, an AMD driver,
  Easy Anti-Cheat and CPU-Z's `cpuz163` driver (loaded on demand).

Shadow Defender is the only new security product that changes how writes
reach the system. Its driver `diskpt.sys` (Shadow Defender Filter Driver
1.5.0.726, SHADOWDEFENDER.COM) starts at boot. It sits as an upper filter on
the disk class (`partmgr,diskpt`) and a lower filter on the volume class
(`fvevol,diskpt,iorate,rdyboost`), and `fltmc` lists it as a legacy filter
on C: and X:. Its own manual (Help.chm, (c) 2007-2018) has a "Registry
Exclusion List": "When accessing these registry items, the changes will
automatically be saved to the original volume". So it handles the registry
itself, not only the disk. The manual does not mention refusing access, DISM
or Windows servicing.

Also noted: a kernel driver named `D` from `C:\WINDOWS\TEMP\D.sys` was
installed 1,139 times from 2026-10-20 03:02 onward. It was already there
before the 03:23 admin run that worked, so it is a weaker suspect. Which
program loads it is unknown.

### 5. What was ruled out

- **Not elevation.** The failing runs are elevated (`whoami` shows High
  Mandatory Level).
- **Not the AppX services.** AppXSvc and StateRepository are running, and
  the SYSTEM task gets the list on the same boot.
- **Not a file system minifilter.** `fltmc filters` shows only Windows
  filters (bindflt, UCPD, WdFilter, storqosflt, wcifs, gameflt, CldFlt, bfs,
  FileCrypt, luafv, UnionFS, npsvctrig, Wof, FileInfo) plus the legacy
  `diskpt`.
- **Not an integrity label or the permissions on `HKLM\SOFTWARE`** (point 3).

### Not proven

- Which key the Appx provider opens. The log only says "Software key".
- That Shadow Defender's driver is what refuses the request. Ways to confirm:
  (a) on the plan 4 VM, run the evidence script before and after installing
  Shadow Defender 1.5.0.726; (b) on the Ally, the owner removes or turns off
  Shadow Defender and the script runs again. NitrOS must not do (b) itself.
- Whether Shadow Mode is on. `CmdTool.exe /pwd: /list` printed nothing; the
  tool's help does not say what an empty list or a wrong password prints.

## What NitrOS must do

1. **Detect.** Before an `appx.remove` that needs the provisioned list, and
   before reporting "not on this PC", run the provisioned query once.
   Treat 0x80070005 (DISM exit code 5, or `COMException` HResult 0x80070005)
   as **"Windows refused to list built-in apps"**, never as "no
   packages". This matches plan 3's `DISM access denied` detail in
   `WinDism`, and the same rule belongs in `WinAppx`.
2. **Name the likely cause when it can see it.** Shadow Defender shows up
   read-only as the service whose path is under `C:\Program Files\Shadow
   Defender\` or as the boot driver `diskpt` (file description "Shadow
   Defender Filter Driver"). If it is there, tell the user, for example:
   "Windows refused to change built-in apps. Shadow Defender is installed and
   may be blocking it. Turn it off or remove it, restart, and run NitrOS
   again." If it is not there, say that a security or snapshot tool may be
   blocking it.
3. **Carry on with what still works.** Removing apps for the current user
   and features and capabilities via DISM do not need the provisioned list.
   Mark only the "new users" (provisioned) part of each app switch as not
   done, with the reason. Do not count it as failed for the whole card.
4. **Check before writing to HKLM.** On this PC the elevated token is
   refused write access even to `HKLM\SOFTWARE`. Before a run, NitrOS should
   test-open each target key for writing without writing anything (as the
   evidence script does). If a key is refused, report it before applying, so
   the user gets no half-applied card.
5. **Warn about Shadow Mode.** If Shadow Defender reports a drive in Shadow
   Mode, every NitrOS change on that drive is thrown away at restart. Say so
   before applying. (How to read that reliably is unverified; see "Not
   proven".)
6. **Never "fix" it.** No taking ownership, no permission changes, no
   TrustedInstaller or SYSTEM tricks, no changes to Shadow Defender settings,
   services or drivers. That decision is the user's.
