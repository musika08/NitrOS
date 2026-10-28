# ISO Builder Test Machine and VM Lane

## Architecture

To enforce Ruling 1 ("Real image servicing never runs on owner's PC directly; only fake-based tests on host and real servicing inside Hyper-V VM lane"), NitrOS uses a dedicated Hyper-V virtual machine for all real image mounting, registry servicing, DISM servicing, and ISO builds.

- **VM Name:** `nitros-builder`
- **Specification:** Generation 2, 4 vCPUs, 4 GB RAM, UEFI / Secure Boot, TPM enabled.
- **Drives:**
  - `C:` 64 GB OS disk (clean Windows 11 installation).
  - `D:` DVD Drive holding the source Windows 11 ISO (read-only; mounted via Hyper-V `Set-VMDvdDrive`).
  - `W:` 200 GB Dynamic NTFS Data Disk (`data.vhdx`) for work folders (`W:\NitrOS-work-*`) and ISO output (`W:\out\`).
- **Checkpoints:**
  - `clean`: Fresh OS installation before data disk attachment.
  - `builder-ready`: Data disk initialized as `W:`, `oscdimg.exe` installed to `C:\ProgramData\NitrOS\tools\`.

## Scripts

1. `New-BuilderVm.ps1 -SourceIso <iso> [-VmName nitros-builder]`
   Creates the builder VM, attaches the 200 GB data disk, formats it as `W:`, installs `oscdimg.exe`, and checkpoints as `builder-ready`.
2. `Invoke-IsoBuild.ps1 -SourceIso <iso> -Name <name> (-Card <card> | -Ids <ids>) [-Restore] [-StopAfter <step>]`
   Restores `builder-ready`, sets source ISO in DVD drive, copies latest NitrOS CLI into guest, runs `nitros build-iso`, and copies output ISO and logs to `measure\work\iso\`.
3. `Invoke-BuilderTests.ps1 [-SourceIso <iso>]`
   Publishes `NitrOS.Image.Tests`, copies them into `nitros-builder`, runs all `Lane=Vm` tests with `NITROS_VM=1`, and extracts results.

## Stock Image Information

- **Host Hyper-V Cmdlets Check:** Verified with `Get-Command -Module Hyper-V -Name New-VHD, Add-VMHardDiskDrive, Set-VMDvdDrive, Copy-VMFile`. The Hyper-V PowerShell module is awaiting host Hyper-V feature enablement (`Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All`) and system restart.
- **Image format:** To be populated from real captured DISM output once VM runs.
- **Stock build:** Windows 11 (build >= 26100).
