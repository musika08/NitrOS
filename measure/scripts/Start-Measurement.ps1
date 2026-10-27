[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CampaignDir,
    [string]$VmName = "nitros-measure",
    [string]$Checkpoint = "clean",
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
$measureExe = "$repoRoot\src\NitrOS.Measure.Host\bin\Release\net10.0-windows10.0.19041.0\nitros-measure.exe"
$logFile = Join-Path $CampaignDir "log.txt"

# Prevent host sleep while script runs
$pInvokeCode = @"
using System;
using System.Runtime.InteropServices;
public static class PowerControl {
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);
    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
}
"@
Add-Type -TypeDefinition $pInvokeCode -ErrorAction SilentlyContinue | Out-Null
[PowerControl]::SetThreadExecutionState([PowerControl]::ES_CONTINUOUS -bor [PowerControl]::ES_SYSTEM_REQUIRED) | Out-Null

try {
    Write-Host "Starting / Resuming measurement campaign at '$CampaignDir'..."

    while ($true) {
        $nextOut = & $measureExe next --campaign $CampaignDir
        $nextExit = $LASTEXITCODE

        if ($nextExit -eq 4) {
            Write-Host "All runs in campaign completed."
            break
        }
        if ($nextExit -ne 0) {
            throw "nitros-measure next failed with exit code $nextExit."
        }

        $runSpec = $nextOut | ConvertFrom-Json
        $runId = $runSpec.runId
        $ids = $runSpec.ids
        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        Write-Host ">>> Running: $runId ($($ids.Count) switches)..."

        $invokeArgs = @{
            VmName     = $VmName
            Checkpoint = $Checkpoint
            RunId      = $runId
            OutDir     = (Join-Path $CampaignDir "runs")
            ConfigPath = $ConfigPath
        }
        if ($ids -and $ids.Count -gt 0) {
            $invokeArgs.Ids = $ids
        }

        & "$PSScriptRoot\Invoke-MeasureRun.ps1" @invokeArgs
        $runExit = $LASTEXITCODE
        $sw.Stop()
        $mins = [math]::Round($sw.Elapsed.TotalMinutes, 1)

        $resultFile = Join-Path $CampaignDir "runs\$runId.result.json"

        if ($runExit -eq 0) {
            & $measureExe complete --campaign $CampaignDir --run $runId --result $resultFile
            $logLine = "$((Get-Date).ToString("o")) | $runId | Success | ${mins}m"
        } elseif ($runExit -eq 3) {
            Write-Warning "Run '$runId' reported not ready or guest slept. Pausing 10 minutes..."
            & $measureExe fail --campaign $CampaignDir --run $runId --error "Host not ready or guest slept"
            $logLine = "$((Get-Date).ToString("o")) | $runId | Slept/NotReady | ${mins}m"
            Start-Sleep -Seconds 600
        } else {
            & $measureExe fail --campaign $CampaignDir --run $runId --error "Invoke-MeasureRun failed (exit $runExit)"
            $logLine = "$((Get-Date).ToString("o")) | $runId | Failed (exit $runExit) | ${mins}m"
        }

        Add-Content -Path $logFile -Value $logLine -Encoding UTF8
    }

    Write-Host "Generating results report..."
    & $measureExe results --campaign $CampaignDir --catalog (Join-Path $repoRoot "catalog")
} finally {
    # Restore normal host sleep
    [PowerControl]::SetThreadExecutionState([PowerControl]::ES_CONTINUOUS) | Out-Null
}
