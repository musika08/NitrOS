[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceIso,
    [Parameter(Mandatory)][string]$Name,
    [string]$Card,
    [string[]]$Ids,
    [switch]$Restore,
    [string]$StopAfter,
    [string]$FirstBootRoute = "SetupComplete",
    [string]$VmName = "nitros-builder",
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Import-Module (Join-Path $PSScriptRoot "NitrOS.Measure.psm1") -Force

try {
    $config = Get-MeasureConfig -Path $ConfigPath

    # 1. Optionally restore checkpoint 'builder-ready'
    if ($Restore) {
        Write-Host "Restoring checkpoint 'builder-ready' on '$VmName'..."
        Restore-VMSnapshot -VMName $VmName -Name "builder-ready" -Confirm:$false | Out-Null
    }

    # 2. Point DVD drive at source ISO
    Write-Host "Mounting source ISO in DVD drive: '$SourceIso'..."
    $dvd = Get-VMDvdDrive -VMName $VmName | Select-Object -First 1
    if (-not $dvd) {
        Add-VMDvdDrive -VMName $VmName -Path $SourceIso | Out-Null
    } else {
        Set-VMDvdDrive -VMName $VmName -ControllerNumber $dvd.ControllerNumber -ControllerLocation $dvd.ControllerLocation -Path $SourceIso | Out-Null
    }

    # 3. Start VM
    Write-Host "Starting '$VmName'..."
    Start-VM -VMName $VmName
    $session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 10

    # 4. Copy guest tools in
    $guestTools = Join-Path $repoRoot "measure\work\guest\nitros"
    if (Test-Path $guestTools) {
        Write-Host "Copying guest CLI to C:\NitrOS.Measure\bin\..."
        Copy-VMFile -VMName $VmName -SourcePath (Join-Path $guestTools "*") -DestinationPath "C:\NitrOS.Measure\bin\" -CreateFullPath -FileSource Host -Force
    }

    # 5. Build CLI command
    $cmd = "C:\NitrOS.Measure\bin\nitros.exe build-iso --source D:\ --out W:\out\$Name.iso --yes"
    if ($Card) {
        $cmd += " --card $Card"
    } elseif ($Ids -and $Ids.Count -gt 0) {
        $cmd += " --ids $($Ids -join ',')"
    }
    if ($StopAfter) {
        $cmd += " --stop-after $StopAfter"
    }
    if ($FirstBootRoute) {
        $cmd += " --first-boot-route $FirstBootRoute"
    }

    Write-Host "Running guest command: $cmd"
    $taskResult = Invoke-GuestTask -Session $session -Name "build-iso-$Name" -CommandLine $cmd -TimeoutMinutes 180

    # 6. Copy output and logs to measure\work\iso\
    $isoOutDir = Join-Path $repoRoot "measure\work\iso"
    New-Item -ItemType Directory -Force $isoOutDir | Out-Null

    Invoke-Command -Session $session -ScriptBlock {
        param($name)
        # Find any build.json and build log
        $work = Get-ChildItem -Path "W:\" -Directory -Filter "NitrOS-work-*" | Select-Object -First 1
        if ($work) {
            Copy-Item -Path "$($work.FullName)\build.json" -Destination "W:\out\$name.build.json" -ErrorAction SilentlyContinue
        }
        $log = Get-ChildItem -Path "C:\ProgramData\NitrOS\logs" -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($log) {
            Copy-Item -Path $log.FullName -Destination "W:\out\$name.log" -ErrorAction SilentlyContinue
        }
    } -ArgumentList $Name

    # Copy files from guest W:\out\ to host measure\work\iso\
    Write-Host "Collecting build outputs..."
    $guestFiles = @(
        "W:\out\$Name.iso",
        "W:\out\$Name.build.json",
        "W:\out\$Name.log"
    )
    foreach ($gf in $guestFiles) {
        $dest = Join-Path $isoOutDir (Split-Path $gf -Leaf)
        try {
            Copy-VMFile -VMName $VmName -SourcePath $gf -DestinationPath $dest -FileSource Guest -Force -ErrorAction SilentlyContinue
        } catch {
            # File may not exist if stopped before generation
        }
    }

    if ($StopAfter -and $taskResult.ExitCode -eq 5) {
        Write-Host "Build stopped as requested by --stop-after $StopAfter (exit code 5)."
        exit 5
    }

    if ($taskResult.ExitCode -ne 0) {
        Write-Host "Build failed with exit code $($taskResult.ExitCode)."
        exit 1
    }

    Write-Host "Build completed successfully: $(Join-Path $isoOutDir "$Name.iso")"
    exit 0
} catch {
    Write-Error $_.Exception.Message
    Write-Host "Failure reason: $($_.Exception.Message)"
    exit 1
}
