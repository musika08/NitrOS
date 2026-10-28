[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceIso,
    [string]$VmName = "nitros-builder",
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Import-Module (Join-Path $PSScriptRoot "NitrOS.Measure.psm1") -Force

try {
    # 1. Create base measure VM from source ISO with checkpoint 'clean'
    Write-Host "Creating base VM '$VmName' from '$SourceIso'..."
    & (Join-Path $PSScriptRoot "New-MeasureVm.ps1") `
        -IsoPath $SourceIso `
        -VmName $VmName `
        -CheckpointName "clean" `
        -ConfigPath $ConfigPath `
        -SettleMinutes 5
    if ($LASTEXITCODE -ne 0) {
        throw "New-MeasureVm failed with exit code $LASTEXITCODE."
    }

    $config = Get-MeasureConfig -Path $ConfigPath
    $vmDir = Join-Path $config.VmRoot $VmName

    # 2. Add 200 GB dynamic data disk on host
    Write-Host "Creating 200 GB dynamic data disk at '$vmDir\data.vhdx'..."
    $dataVhd = Join-Path $vmDir "data.vhdx"
    New-VHD -Path $dataVhd -SizeBytes 200GB -Dynamic | Out-Null
    Add-VMHardDiskDrive -VMName $VmName -Path $dataVhd | Out-Null

    # 3. Start VM and initialize data disk as W: NTFS
    Write-Host "Starting VM to initialize data disk..."
    Start-VM -VMName $VmName
    $session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 10

    Invoke-Command -Session $session -ScriptBlock {
        $rawDisk = Get-Disk | Where-Object PartitionStyle -eq 'RAW' | Select-Object -First 1
        if ($rawDisk) {
            $rawDisk | Initialize-Disk -PartitionStyle GPT -PassThru |
                New-Partition -DriveLetter W -UseMaximumSize |
                Format-Volume -FileSystem NTFS -NewFileSystemLabel "BuilderData" -Confirm:$false | Out-Null
        }
        New-Item -ItemType Directory -Force "W:\out", "C:\ProgramData\NitrOS\tools" | Out-Null
    }

    # 4. Copy oscdimg.exe to guest C:\ProgramData\NitrOS\tools\
    $oscdimgLocal = Join-Path $repoRoot "measure\work\tools\oscdimg.exe"
    if (Test-Path $oscdimgLocal) {
        Write-Host "Copying oscdimg.exe into guest tools..."
        Copy-VMFile -VMName $VmName -SourcePath $oscdimgLocal -DestinationPath "C:\ProgramData\NitrOS\tools\oscdimg.exe" -CreateFullPath -FileSource Host -Force
    }

    # 5. Shut down cleanly and take checkpoint 'builder-ready'
    Write-Host "Shutting down VM and creating checkpoint 'builder-ready'..."
    Stop-VM -VMName $VmName -Force
    Checkpoint-VM -VMName $VmName -SnapshotName "builder-ready" | Out-Null

    Write-Host "Builder VM '$VmName' ready with checkpoint 'builder-ready'."
    exit 0
} catch {
    Write-Error $_.Exception.Message
    Write-Host "Failure reason: $($_.Exception.Message)"
    exit 1
}
