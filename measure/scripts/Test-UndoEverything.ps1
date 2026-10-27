[CmdletBinding()]
param(
    [string]$VmName = "nitros-measure",
    [string]$Checkpoint = "clean",
    [string]$Card = "recommended",
    [string]$OutDir = "measure\work\undo-proof",
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Import-Module (Join-Path $PSScriptRoot "NitrOS.Measure.psm1") -Force

$config = Get-MeasureConfig -Path $ConfigPath
$targetOut = Join-Path $repoRoot "$OutDir\$Card"
New-Item -ItemType Directory -Force $targetOut | Out-Null

Write-Host ">>> Testing Undo Everything for card '$Card'..."

# 1. Restore clean checkpoint
Stop-VM -VMName $VmName -Force -ErrorAction SilentlyContinue
Restore-VMSnapshot -VMName $VmName -Name $Checkpoint -Confirm:$false | Out-Null
Connect-VMNetworkAdapter -VMName $VmName -SwitchName "Default Switch" -ErrorAction SilentlyContinue | Out-Null
Start-VM -VMName $VmName

$session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 15
Copy-Item -ToSession $session -Path "$repoRoot\measure\work\guest\*" -Destination "C:\NitrOS.Measure\bin" -Recurse -Force | Out-Null

# 2. Before fingerprint
Write-Host "Capturing initial fingerprint..."
Invoke-GuestTask -Session $session -Name "FpBefore" `
    -CommandLine "C:\NitrOS.Measure\bin\probe\nitros-probe.exe fingerprint --out C:\NitrOS.Measure\out\before.json" -TimeoutMinutes 10 | Out-Null

# 3. Apply card
Write-Host "Applying card '$Card'..."
$applyRes = Invoke-GuestTask -Session $session -Name "ApplyCard" `
    -CommandLine "C:\NitrOS.Measure\bin\nitros\nitros.exe apply --card $Card --yes --allow-without-restore-point" -TimeoutMinutes 20

# 4. Restart and health check
Write-Host "Restarting for health check..."
Invoke-Command -Session $session -ScriptBlock { shutdown.exe /r /t 0 }
Remove-PSSession $session -ErrorAction SilentlyContinue
Start-Sleep -Seconds 60

$session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 15
Start-Sleep -Seconds 120

Write-Host "Running post-apply health check..."
$healthAfterApply = Invoke-GuestTask -Session $session -Name "HealthApply" `
    -CommandLine "C:\NitrOS.Measure\bin\nitros\nitros.exe health" -TimeoutMinutes 5

# 5. Undo card
Write-Host "Undoing card '$Card'..."
$newestJournal = Invoke-Command -Session $session -ScriptBlock {
    Get-ChildItem -Path "$env:ProgramData\NitrOS\backups" -Filter "*.json" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

$undoRes = Invoke-GuestTask -Session $session -Name "UndoCard" `
    -CommandLine "C:\NitrOS.Measure\bin\nitros\nitros.exe undo `"$newestJournal`" --yes" -TimeoutMinutes 20

# Restart after undo
Write-Host "Restarting after undo..."
Invoke-Command -Session $session -ScriptBlock { shutdown.exe /r /t 0 }
Remove-PSSession $session -ErrorAction SilentlyContinue
Start-Sleep -Seconds 60

$session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 15

# 6. After fingerprint
Write-Host "Capturing post-undo fingerprint..."
Invoke-GuestTask -Session $session -Name "FpAfter" `
    -CommandLine "C:\NitrOS.Measure\bin\probe\nitros-probe.exe fingerprint --out C:\NitrOS.Measure\out\after.json" -TimeoutMinutes 10 | Out-Null

# Copy artifacts out
Copy-Item -FromSession $session -Path "C:\NitrOS.Measure\out\before.json" -Destination (Join-Path $targetOut "before.json") -Force
Copy-Item -FromSession $session -Path "C:\NitrOS.Measure\out\after.json" -Destination (Join-Path $targetOut "after.json") -Force

# 7. Post-undo health check
Write-Host "Running post-undo health check..."
$healthAfterUndo = Invoke-GuestTask -Session $session -Name "HealthUndo" `
    -CommandLine "C:\NitrOS.Measure\bin\nitros\nitros.exe health" -TimeoutMinutes 5

# Compare fingerprints on host
Write-Host "Comparing fingerprints on host..."
$measureExe = "$repoRoot\src\NitrOS.Measure.Host\bin\Release\net10.0-windows10.0.19041.0\nitros-measure.exe"
& $measureExe compare-fingerprints `
    --before (Join-Path $targetOut "before.json") `
    --after (Join-Path $targetOut "after.json") `
    --catalog (Join-Path $repoRoot "catalog")

$compareExit = $LASTEXITCODE

Stop-VM -VMName $VmName -Force -ErrorAction SilentlyContinue

Write-Host "Undo check for '$Card' finished with exit code $compareExit."
exit $compareExit
