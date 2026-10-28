# run-device-updates-check.ps1
# Runs NitrOS to check Device updates interactively or in test mode.
[CmdletBinding()]
param(
    [string]$LogPath = "$env:USERPROFILE\nitros-device-updates-check.txt"
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
$appExe = Join-Path $repoRoot "src\NitrOS.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\NitrOS.App.exe"

if (-not (Test-Path $appExe)) {
    Write-Host "Building NitrOS.App..."
    dotnet build (Join-Path $repoRoot "src\NitrOS.App\NitrOS.App.csproj") -c Debug
}

Write-Host "Starting NitrOS. Output logged to $LogPath"
"NitrOS Device Updates Check started at $(Get-Date -Format o)" | Out-File -FilePath $LogPath -Encoding utf8

$proc = Start-Process -FilePath $appExe -PassThru
"PID: $($proc.Id)" | Out-File -FilePath $LogPath -Append -Encoding utf8
Write-Host "NitrOS running with PID $($proc.Id). Follow the checklist in docs/research/device-updates-ally-checklist.md"
