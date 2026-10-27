[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IsoPath,
    [Parameter(Mandatory)][string]$VmName,
    [string]$CheckpointName = "clean",
    [string]$ConfigPath = "measure\measure.local.json",
    [int]$ImageIndex = 0,
    [int]$SettleMinutes = 30
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Import-Module (Join-Path $PSScriptRoot "NitrOS.Measure.psm1") -Force

try {
    # 1. Mount ISO read-only and verify Windows 11 24H2 Pro
    Write-Host "Inspecting Windows ISO at '$IsoPath'..."
    $mounted = Mount-DiskImage -ImagePath $IsoPath -Access ReadOnly -PassThru
    $driveLetter = ($mounted | Get-Volume).DriveLetter
    $wimPath = "$($driveLetter):\sources\install.wim"
    if (-not (Test-Path $wimPath)) {
        $wimPath = "$($driveLetter):\sources\install.esd"
    }

    try {
        $images = Get-WindowsImage -ImagePath $wimPath
        $proImage = $images | Where-Object { $_.EditionId -eq 'Professional' } | Select-Object -First 1
        if (-not $proImage) {
            throw "No Professional edition found in ISO."
        }

        if ($proImage.Version.Major -lt 10 -or ($proImage.Version.Major -eq 10 -and $proImage.Version.Build -lt 26100)) {
            throw "NitrOS needs a Windows 11 24H2 or newer ISO (found version $($proImage.Version))."
        }

        if ($ImageIndex -le 0) {
            $ImageIndex = $proImage.ImageIndex
        }
        $targetBuild = $proImage.Version.ToString()
        $edition = $proImage.EditionId
    } finally {
        Dismount-DiskImage -ImagePath $IsoPath | Out-Null
    }

    # 2. Get local config and write answer.iso
    $config = Get-MeasureConfig -Path $ConfigPath
    $vmDir = Join-Path $config.VmRoot $VmName
    New-Item -ItemType Directory -Force $vmDir | Out-Null

    $answerIsoPath = Join-Path $vmDir "answer.iso"
    $templatePath = Join-Path $repoRoot "measure\unattend\autounattend.template.xml"

    $plainPassword = [System.Net.NetworkCredential]::new('', $config.Credential.Password).Password
    $env:NITROS_MEASURE_PASSWORD = $plainPassword
    try {
        & "$repoRoot\src\NitrOS.Measure.Host\bin\Release\net10.0-windows10.0.19041.0\nitros-measure.exe" `
            answer-iso --template $templatePath --out $answerIsoPath --image-index $ImageIndex --user $config.User --computer-name $VmName
        if ($LASTEXITCODE -ne 0) {
            throw "nitros-measure answer-iso failed with exit code $LASTEXITCODE."
        }
    } finally {
        $env:NITROS_MEASURE_PASSWORD = $null
    }

    # 3. Create Generation 2 VM
    $vhdPath = Join-Path $vmDir "disk.vhdx"
    New-VM -Name $VmName -Generation 2 -MemoryStartupBytes 4GB -NewVHDPath $vhdPath -NewVHDSizeBytes 64GB | Out-Null
    Set-VMMemory -VMName $VmName -DynamicMemoryEnabled $false | Out-Null
    Set-VMProcessor -VMName $VmName -Count 4 | Out-Null
    Set-VMKeyProtector -VMName $VmName -NewLocalKeyProtector | Out-Null
    Enable-VMTPM -VMName $VmName | Out-Null
    Set-VMFirmware -VMName $VmName -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows | Out-Null

    Add-VMDvdDrive -VMName $VmName -Path $config.IsoPath | Out-Null
    Add-VMDvdDrive -VMName $VmName -Path $answerIsoPath | Out-Null

    $dvdDrives = Get-VMDvdDrive -VMName $VmName
    Set-VMFirmware -VMName $VmName -FirstBootDevice $dvdDrives[0] | Out-Null
    Set-VM -VMName $VmName -CheckpointType Standard -AutomaticCheckpointsEnabled $false | Out-Null
    Disconnect-VMNetworkAdapter -VMName $VmName -ErrorAction SilentlyContinue | Out-Null

    # 4. Start machine and boot
    Start-VM -VMName $VmName
    Send-VmKeys -VmName $VmName -VirtualKey 13 -Seconds 30

    Write-Host "Waiting up to 90 minutes for unattended setup and guest session..."
    $session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 90

    # 5. Prepare guest through PowerShell Direct
    Invoke-Command -Session $session -ScriptBlock {
        New-Item -ItemType Directory -Force "C:\NitrOS.Measure\bin", "C:\NitrOS.Measure\out" | Out-Null
        powercfg /list | ForEach-Object {
            if ($_ -match "GUID: ([a-f0-9-]+)") {
                $guid = $matches[1]
                powercfg /setacvalueindex $guid SUB_SLEEP STANDBYIDLE 0
                powercfg /setacvalueindex $guid SUB_SLEEP HIBERNATEIDLE 0
            }
        }
        Set-WinHomeLocation -GeoId 244 # United States
    }

    # 6. Wait SettleMinutes, shut down cleanly and take checkpoint
    Write-Host "Settling for $SettleMinutes minutes..."
    Start-Sleep -Seconds ($SettleMinutes * 60)

    Stop-VM -VMName $VmName -Force
    Remove-VMDvdDrive -VMName $VmName -ControllerNumber 0 -ControllerLocation 1 -ErrorAction SilentlyContinue
    if (Test-Path $answerIsoPath) {
        Remove-Item $answerIsoPath -Force
    }

    $hardDrive = Get-VMHardDiskDrive -VMName $VmName
    Set-VMFirmware -VMName $VmName -FirstBootDevice $hardDrive | Out-Null
    Checkpoint-VM -VMName $VmName -SnapshotName $CheckpointName | Out-Null

    # 7. Write .vm.json record
    $workDir = Join-Path $repoRoot "measure\work"
    New-Item -ItemType Directory -Force $workDir | Out-Null
    $vmJsonPath = Join-Path $workDir "$VmName.vm.json"

    $vmRecord = [PSCustomObject]@{
        vmName       = $VmName
        checkpoint   = $CheckpointName
        windowsBuild = $targetBuild
        edition      = $edition
        imageIndex   = $ImageIndex
        createdAt    = (Get-Date).ToString("o")
    }
    $vmRecord | ConvertTo-Json -Depth 4 | Set-Content -Path $vmJsonPath -Encoding UTF8

    Write-Host "VM created successfully: $vmJsonPath"
    Write-Output $vmJsonPath
    exit 0
} catch {
    Write-Error $_.Exception.Message
    Write-Host "Failure reason: $($_.Exception.Message)"
    exit 1
}
