# collect-identifiers.ps1
# Read-only snapshot of the identifiers the NitrOS switch inventory refers to.
# It changes nothing on the PC. Run it elevated so the AllUsers, provisioned,
# feature and capability sections can be read.
# Output: $env:USERPROFILE\NitrOS-identifiers.json

$ErrorActionPreference = 'Stop'
$outPath = Join-Path $env:USERPROFILE 'NitrOS-identifiers.json'
$result = [ordered]@{}

function Invoke-Section {
    param([string]$Name, [scriptblock]$Body)
    try {
        $result[$Name] = & $Body
    } catch {
        $result[$Name] = [ordered]@{ error = $_.Exception.Message }
    }
}

$result['collectedAt'] = (Get-Date).ToString('o')
$result['elevated'] = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

Invoke-Section 'os' {
    $cv = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    [ordered]@{
        osVersion      = [Environment]::OSVersion.VersionString
        version        = [Environment]::OSVersion.Version.ToString()
        displayVersion = $cv.DisplayVersion
        currentBuild   = $cv.CurrentBuild
        ubr            = $cv.UBR
        productName    = $cv.ProductName
        editionId      = $cv.EditionID
    }
}

Invoke-Section 'appxPackages' {
    @(Get-AppxPackage -AllUsers | Sort-Object Name | ForEach-Object {
        [ordered]@{ Name = $_.Name; PackageFamilyName = $_.PackageFamilyName }
    })
}

Invoke-Section 'provisionedPackages' {
    @(Get-AppxProvisionedPackage -Online | Sort-Object DisplayName | ForEach-Object {
        [ordered]@{ DisplayName = $_.DisplayName; PackageName = $_.PackageName }
    })
}

Invoke-Section 'services' {
    @(Get-Service | Sort-Object Name | ForEach-Object {
        [ordered]@{ Name = $_.Name; DisplayName = $_.DisplayName; StartType = [string]$_.StartType }
    })
}

Invoke-Section 'scheduledTasks' {
    @(Get-ScheduledTask | Sort-Object TaskPath, TaskName | ForEach-Object {
        [ordered]@{ Task = ($_.TaskPath + $_.TaskName); State = [string]$_.State }
    })
}

Invoke-Section 'optionalFeatures' {
    @(Get-WindowsOptionalFeature -Online | Sort-Object FeatureName | ForEach-Object {
        [ordered]@{ FeatureName = $_.FeatureName; State = [string]$_.State }
    })
}

Invoke-Section 'capabilities' {
    @(Get-WindowsCapability -Online | Sort-Object Name | ForEach-Object {
        [ordered]@{ Name = $_.Name; State = [string]$_.State }
    })
}

$uninstallRoots = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
)
$uninstall = [ordered]@{}
foreach ($root in $uninstallRoots) {
    try {
        $uninstall[$root] = @(Get-ChildItem -Path $root | ForEach-Object {
            $p = Get-ItemProperty -Path $_.PSPath
            [ordered]@{
                Key                  = $_.PSChildName
                DisplayName          = $p.DisplayName
                Publisher            = $p.Publisher
                QuietUninstallString = $p.QuietUninstallString
                UninstallString      = $p.UninstallString
            }
        })
    } catch {
        $uninstall[$root] = [ordered]@{ error = $_.Exception.Message }
    }
}
$result['uninstallEntries'] = $uninstall

$runKeys = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce'
)
$run = [ordered]@{}
foreach ($key in $runKeys) {
    try {
        $item = Get-Item -Path $key
        $values = [ordered]@{}
        foreach ($n in $item.GetValueNames()) { $values[$n] = [string]$item.GetValue($n) }
        $run[$key] = $values
    } catch {
        $run[$key] = [ordered]@{ error = $_.Exception.Message }
    }
}
$result['runKeys'] = $run

Invoke-Section 'startupFolders' {
    $folders = [ordered]@{}
    foreach ($f in @([Environment]::GetFolderPath('Startup'), [Environment]::GetFolderPath('CommonStartup'))) {
        try {
            $folders[$f] = @(Get-ChildItem -Path $f -Force | ForEach-Object { $_.Name })
        } catch {
            $folders[$f] = [ordered]@{ error = $_.Exception.Message }
        }
    }
    $folders
}

Invoke-Section 'powercfgList' { (& powercfg.exe /list | Out-String) }
Invoke-Section 'powercfgQh' { (& powercfg.exe /qh | Out-String) }

$json = $result | ConvertTo-Json -Depth 8
$json | Out-File -FilePath $outPath -Encoding utf8
Write-Output "Wrote $outPath"
