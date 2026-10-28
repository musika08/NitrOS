[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
Write-Host "=== NitrOS Ally Firmware & ESRT Reader (Read-Only) ===" -ForegroundColor Cyan

# 1. System BIOS Information
Write-Host "`n--- System BIOS ---" -ForegroundColor Yellow
$biosReg = Get-ItemProperty -Path "HKLM:\HARDWARE\DESCRIPTION\System\BIOS" -ErrorAction SilentlyContinue
if ($biosReg) {
    [PSCustomObject]@{
        Manufacturer     = $biosReg.SystemManufacturer
        ProductName      = $biosReg.SystemProductName
        BaseBoardProduct = $biosReg.BaseBoardProduct
        BIOSVersion      = $biosReg.BIOSVersion
        BIOSReleaseDate  = $biosReg.BIOSReleaseDate
    } | Format-List
} else {
    Write-Host "Could not read BIOS registry."
}

# 2. UEFI ESRT Resources
Write-Host "`n--- UEFI ESRT Resources ---" -ForegroundColor Yellow
$esrtRoot = "HKLM:\HARDWARE\UEFI\ESRT"
if (Test-Path $esrtRoot) {
    Get-ChildItem -Path $esrtRoot | ForEach-Object {
        $props = Get-ItemProperty -Path $_.PSPath
        [PSCustomObject]@{
            Guid               = $_.PSChildName
            Type               = $props.Type
            Version            = $props.Version
            VersionHex         = "0x{0:X}" -f $props.Version
            LastAttemptVersion = $props.LastAttemptVersion
            LastAttemptStatus  = $props.LastAttemptStatus
        }
    } | Format-Table -AutoSize
} else {
    Write-Host "No ESRT registry entries found under $esrtRoot."
}

# 3. PnP Firmware Devices
Write-Host "`n--- PnP Firmware Class Devices ---" -ForegroundColor Yellow
$firmwareDevices = Get-CimInstance Win32_PnPEntity -Filter "PNPClass = 'Firmware'" -ErrorAction SilentlyContinue
if ($firmwareDevices) {
    $firmwareDevices | Select-Object Name, DeviceID, Status, Manufacturer | Format-Table -AutoSize
} else {
    Write-Host "No PnP devices in Firmware class found."
}

# 4. Controller & CFU Devices
Write-Host "`n--- ROG Controller & CFU Devices ---" -ForegroundColor Yellow
$controllerDevices = Get-CimInstance Win32_PnPEntity | Where-Object { $_.DeviceID -match "VID_0B05&PID_1B4C" }
if ($controllerDevices) {
    $controllerDevices | Select-Object Name, DeviceID, Status, PNPClass | Format-Table -AutoSize
} else {
    Write-Host "No devices with VID 0B05 PID 1B4C found."
}
