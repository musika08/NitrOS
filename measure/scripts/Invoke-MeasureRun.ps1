[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VmName,
    [Parameter(Mandatory)][string]$Checkpoint,
    [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][string]$OutDir,
    [string[]]$Ids = @(),
    [ValidateSet("Off", "On")][string]$Network = "Off",
    [switch]$SkipApply,
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Import-Module (Join-Path $PSScriptRoot "NitrOS.Measure.psm1") -Force

try {
    # 1. Assert host readiness
    Write-Host "Checking host readiness for run '$RunId'..."
    if (-not (Assert-HostReadyForRun -MaxWaitMinutes 30)) {
        Write-Warning "Host is not ready (on battery or insufficient available RAM)."
        exit 3
    }

    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $config = Get-MeasureConfig -Path $ConfigPath

    # 2. Restore checkpoint and configure network
    $vm = Get-VM -Name $VmName -ErrorAction Stop
    if ($vm.State -ne 'Off') {
        Stop-VM -VMName $VmName -Force
    }

    Restore-VMSnapshot -VMName $VmName -Name $Checkpoint -Confirm:$false | Out-Null

    if ($Network -eq 'Off') {
        Disconnect-VMNetworkAdapter -VMName $VmName -ErrorAction SilentlyContinue | Out-Null
    } else {
        Connect-VMNetworkAdapter -VMName $VmName -SwitchName "Default Switch" -ErrorAction SilentlyContinue | Out-Null
    }

    # 3. Start guest and deploy binaries
    Start-VM -VMName $VmName
    $session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 15

    Copy-Item -ToSession $session -Path "$repoRoot\measure\work\guest\*" -Destination "C:\NitrOS.Measure\bin" -Recurse -Force | Out-Null

    # Register auto-sampling task on sign-in
    Invoke-Command -Session $session -ScriptBlock {
        $action = New-ScheduledTaskAction -Execute "C:\NitrOS.Measure\bin\probe\nitros-probe.exe" `
            -Argument "sample --out C:\NitrOS.Measure\out\samples.json --idle-minutes 10 --count 5 --interval-seconds 30 --kvp"
        $trigger = New-ScheduledTaskTrigger -AtLogOn
        $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest

        Unregister-ScheduledTask -TaskName "Sample" -TaskPath "\NitrOS.Measure\" -Confirm:$false -ErrorAction SilentlyContinue
        Register-ScheduledTask -TaskName "Sample" -TaskPath "\NitrOS.Measure\" -Action $action -Trigger $trigger -Principal $principal | Out-Null
    }

    # 4. Apply switches if not skipped
    $journalOutPath = Join-Path $OutDir "$RunId.journal.json"
    $applyExitCode = $null

    if (-not $SkipApply -and $Ids -and $Ids.Count -gt 0) {
        $idsJoined = ($Ids -join ',')
        $applyCmd = "C:\NitrOS.Measure\bin\nitros\nitros.exe apply --ids $idsJoined --yes --allow-without-restore-point"
        Write-Host "Applying switches: $idsJoined"
        $taskRes = Invoke-GuestTask -Session $session -Name "Apply" -CommandLine $applyCmd -TimeoutMinutes 15
        $applyExitCode = $taskRes.ExitCode

        # Retrieve journal
        $newestJournal = Invoke-Command -Session $session -ScriptBlock {
            Get-ChildItem -Path "$env:ProgramData\NitrOS\backups" -Filter "*.json" |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1 -ExpandProperty FullName
        }

        if ($newestJournal) {
            Copy-Item -FromSession $session -Path $newestJournal -Destination $journalOutPath -Force
        }
    }

    # 5. Restart guest and wait for sampling via KVP
    Write-Host "Restarting guest for sampling..."
    Invoke-Command -Session $session -ScriptBlock { shutdown.exe /r /t 0 }
    Remove-PSSession $session -ErrorAction SilentlyContinue

    Write-Host "Waiting for guest sampling via KVP..."
    Wait-GuestKvp -VmName $VmName -Key "NitrOS.Measure.State" -Value "done" -TimeoutMinutes 30

    # 6. Retrieve samples and summarize
    $postSession = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 10
    $guestSamplesPath = "C:\NitrOS.Measure\out\samples.json"
    $hostSamplesPath = Join-Path $OutDir "$RunId.samples.json"

    Copy-Item -FromSession $postSession -Path $guestSamplesPath -Destination $hostSamplesPath -Force
    Stop-VM -VMName $VmName -Force

    $resultOutPath = Join-Path $OutDir "$RunId.result.json"
    $sumArgs = @(
        "summarize-run",
        "--run", $RunId,
        "--samples", $hostSamplesPath,
        "--out", $resultOutPath
    )

    if ($applyExitCode -ne $null) {
        $sumArgs += @("--apply-exit", $applyExitCode)
    }
    if (Test-Path $journalOutPath) {
        $sumArgs += @("--journal", $journalOutPath)
    }

    & "$repoRoot\src\NitrOS.Measure.Host\bin\Release\net10.0-windows10.0.19041.0\nitros-measure.exe" $sumArgs
    if ($LASTEXITCODE -ne 0) {
        throw "nitros-measure summarize-run failed."
    }

    $resJson = Get-Content $resultOutPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($resJson.slept) {
        Write-Warning "Run '$RunId' guest slept during sampling."
        exit 3
    }

    Write-Host "Run '$RunId' completed successfully: $resultOutPath"
    Write-Output $resultOutPath
    exit 0
} catch {
    Write-Error $_.Exception.Message
    Write-Host "Run '$RunId' failed: $($_.Exception.Message)"
    exit 1
}
