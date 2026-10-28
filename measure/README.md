# NitrOS Measure

This folder contains scripts and templates for running automated NitrOS measurement campaigns in a Hyper-V virtual machine.

## Folder Contents

- `unattend/autounattend.template.xml`: Answer file template used during automated Windows installation in the test VM.
- `measure.example.json`: Example configuration file.
- `scripts/`: PowerShell scripts driving the VM lifecycle and runs.

## Local Configuration and Work Directories

- `measure.local.json`: Machine-specific configuration created by `New-MeasureConfig.ps1`. Contains the encrypted password for the test account and VM paths. This file is git-ignored and never committed.
- `work/`: Working directory for published guest binaries, intermediate results, and VM disks. This directory is git-ignored.

## Workflow Order

1. Check host readiness with `scripts/Test-HostReady.ps1`.
2. Enable Hyper-V with `scripts/Enable-HyperV.ps1` (if not enabled; needs admin and restart).
3. Generate local configuration with `scripts/New-MeasureConfig.ps1 -IsoPath <path>`.
4. Create and prepare test VM with `scripts/New-MeasureVm.ps1`.
5. Run measurement campaigns or single switch runs with `scripts/Invoke-MeasureRun.ps1`.

## ISO Builder VM Lane

1. Create builder VM with 200 GB data disk on `W:` using `scripts/New-BuilderVm.ps1 -SourceIso <path>`.
2. Build slim ISOs inside the builder VM using `scripts/Invoke-IsoBuild.ps1 -SourceIso <path> -Name <output-name> -Card <card>`.
3. Run VM-lane image tests inside the builder VM using `scripts/Invoke-BuilderTests.ps1`.
