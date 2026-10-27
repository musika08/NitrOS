[CmdletBinding()]
param(
    [switch]$Elevated,
    [string]$VmRoot = "X:\NitrOS-VM"
)

$ErrorActionPreference = 'Continue'
$lines = [System.Collections.Generic.List[string]]::new()

# 1. Windows Edition
$os = Get-CimInstance Win32_OperatingSystem
$caption = $os.Caption
if ($caption -match "Pro|Enterprise|Education") {
    $lines.Add("PASS: Windows edition is Pro, Enterprise, or Education ($caption).")
} else {
    $lines.Add("FAIL: Windows edition does not support Hyper-V ($caption).")
}

# 2. Firmware Virtualization
$proc = Get-CimInstance Win32_Processor | Select-Object -First 1
if ($proc.VirtualizationFirmwareEnabled) {
    $lines.Add("PASS: Firmware virtualization is enabled.")
} else {
    $lines.Add("FAIL: Firmware virtualization is disabled in firmware.")
}

# 3. Hyper-V Feature States
$hf = Get-CimInstance Win32_OptionalFeature -Filter "Name = 'Microsoft-Hyper-V-All'" -ErrorAction SilentlyContinue
$vmms = Get-Service vmms -ErrorAction SilentlyContinue
$mod = Get-Module -ListAvailable Hyper-V
if ($hf -and $hf.InstallState -eq 1 -and $vmms -and $mod) {
    $lines.Add("PASS: Hyper-V feature is enabled, vmms is present, and Hyper-V module exists.")
} else {
    $stateStr = if ($hf) { $hf.InstallState } else { "Unknown" }
    $lines.Add("WARN: Hyper-V is not fully enabled (InstallState: $stateStr, vmms: $($null -ne $vmms), module: $($null -ne $mod)).")
}

# 4. Pending Restarts
$pendingCbs = Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
$pendingWu = Test-Path "HKLM:\SOFTWARE\Microsoft\WindowsUpdate\Auto Update\RebootRequired"
$pendingRen = $false
$renameKey = Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager" -Name "PendingFileRenameOperations" -ErrorAction SilentlyContinue
if ($renameKey -and $renameKey.PendingFileRenameOperations) {
    $pendingRen = $true
}

if ($pendingCbs -or $pendingWu -or $pendingRen) {
    $lines.Add("WARN: A restart is already pending (CBS: $pendingCbs, WU: $pendingWu, FileRename: $pendingRen). Finish it first.")
} else {
    $lines.Add("PASS: No restart is pending.")
}

# 5. Shadow Defender
$sdDir = Test-Path "C:\Program Files\Shadow Defender"
$sdDriver = Get-CimInstance Win32_SystemDriver -Filter "Name = 'diskpt'" -ErrorAction SilentlyContinue
if ($sdDir -or $sdDriver) {
    $lines.Add("WARN: Shadow Defender is installed. If drive C: or the test machine's drive is in Shadow Mode, turning on Hyper-V and every test machine file are thrown away at restart. Check in Shadow Defender's window that no drive is in Shadow Mode.")
} else {
    $lines.Add("PASS: Shadow Defender not detected.")
}

# 6. Free Space on VmRoot Drive
$driveRoot = [System.IO.Path]::GetPathRoot($VmRoot)
$driveLetter = $driveRoot.TrimEnd('\', ':')
$psDrive = Get-PSDrive -Name $driveLetter -ErrorAction SilentlyContinue
if ($psDrive) {
    $freeGb = [math]::Round($psDrive.Free / 1GB, 1)
    if ($freeGb -ge 150) {
        $lines.Add("PASS: Drive $driveLetter`: has $freeGb GB free space (>= 150 GB).")
    } else {
        $lines.Add("FAIL: Drive $driveLetter`: has only $freeGb GB free space (< 150 GB required).")
    }
} else {
    $lines.Add("WARN: Could not inspect drive for VmRoot '$VmRoot'.")
}

# 7. Elevated Checks
if ($Elevated) {
    # DISM Feature Info
    $dismOut = & dism.exe /English /Online /Get-FeatureInfo /FeatureName:Microsoft-Hyper-V 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) {
        $lines.Add("PASS: DISM feature info query succeeded (exit code 0).")
    } else {
        $lines.Add("FAIL: DISM feature info query failed with exit code $LASTEXITCODE.")
    }

    # BitLocker State
    $bl = Get-BitLockerVolume -MountPoint C: -ErrorAction SilentlyContinue
    if ($bl -and $bl.ProtectionStatus -eq 'On') {
        $lines.Add("WARN: Have your BitLocker recovery key ready before the restart.")
    } else {
        $lines.Add("PASS: BitLocker is off or not protecting C:.")
    }
}

# 8. AC Power Check
$battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
if ($battery) {
    $wmiStatus = Get-CimInstance -Namespace root\wmi -ClassName BatteryStatus -ErrorAction SilentlyContinue
    $onAc = ($battery.BatteryStatus -eq 2) -or ($battery.BatteryStatus -eq 3) -or ($wmiStatus -and ($wmiStatus.PowerOnline -or $wmiStatus.Charging))
    if ($onAc) {
        $lines.Add("PASS: Device is connected to AC power.")
    } else {
        $lines.Add("WARN: Device is on battery power. Connect to AC power before measurement runs.")
    }
} else {
    $lines.Add("PASS: Desktop host (no battery detected).")
}

$outPath = Join-Path $env:USERPROFILE "NitrOS-host-ready.txt"
$lines | Set-Content -Path $outPath -Encoding UTF8
$lines | ForEach-Object { Write-Output $_ }
