[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$outPath = Join-Path $env:USERPROFILE "NitrOS-hyperv-enable.txt"

try {
    $result = Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All -NoRestart
    $lines = @(
        "Result: Success",
        "RestartNeeded: $($result.RestartNeeded)"
    )
    $lines | Set-Content -Path $outPath -Encoding UTF8
    $lines | ForEach-Object { Write-Output $_ }
} catch {
    $errLines = @(
        "Result: Failed",
        "Error: $($_.Exception.Message)"
    )
    $errLines | Set-Content -Path $outPath -Encoding UTF8
    $errLines | ForEach-Object { Write-Output $_ }
    exit 1
}
