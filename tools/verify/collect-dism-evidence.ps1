# collect-dism-evidence.ps1
# Read-only evidence for "DISM provisioned-package query is denied even when
# elevated" (plan 3, task 11). It changes nothing on the PC: it only reads
# state, runs read-only queries and writes its own output file.
# The registry check opens keys with write rights and closes them again
# without writing anything, to see which key the elevated token is refused.
# Run it elevated.
# Output: $env:USERPROFILE\NitrOS-dism-evidence.txt

$ErrorActionPreference = 'Continue'
$outPath = Join-Path $env:USERPROFILE 'NitrOS-dism-evidence.txt'
$started = Get-Date
"NitrOS DISM evidence, collected $($started.ToString('o'))" | Out-File -FilePath $outPath -Encoding utf8

function W([string]$Title, [scriptblock]$Body) {
    Add-Content -Path $outPath -Encoding utf8 -Value "`r`n===== $Title ====="
    try {
        $text = & $Body 2>&1 | Out-String -Width 300
    } catch {
        $text = "ERROR: $($_.Exception.GetType().FullName): $($_.Exception.Message) HResult=0x{0:X8}" -f $_.Exception.HResult
    }
    Add-Content -Path $outPath -Encoding utf8 -Value $text
}

W 'Elevation and token' {
    $p = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    "Elevated admin: $($p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))"
    whoami.exe /all
}

W 'Shadow Defender: drives in Shadow Mode (CmdTool /list)' {
    $tool = 'C:\Program Files\Shadow Defender\CmdTool.exe'
    if (Test-Path $tool) { & $tool '/pwd:' '/list' } else { 'CmdTool.exe not found' }
}

W 'Shadow Defender: driver and service' {
    sc.exe qc diskpt
    sc.exe query diskpt
    Get-CimInstance Win32_Service | Where-Object { $_.PathName -match 'Shadow Defender' } |
        Select-Object Name, DisplayName, State, StartMode, StartName, PathName | Format-List
}

W 'File system filters (fltmc filters)' { fltmc.exe filters }
W 'Filter instances (fltmc instances)' { fltmc.exe instances }

W 'Storage class filters' {
    foreach ($g in '{4d36e967-e325-11ce-bfc1-08002be10318}', '{71a27cdd-812a-11d0-bec7-08002be2092f}') {
        $k = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Class\$g"
        "$g Upper=$($k.UpperFilters -join ',') Lower=$($k.LowerFilters -join ',')"
    }
}

W 'Scheduled tasks that run Winhance or other debloat scripts' {
    Get-ScheduledTask | Where-Object {
        ($_.Actions | ForEach-Object { "$($_.Execute) $($_.Arguments)" }) -match 'Winhance|BloatRemoval|debloat'
    } | ForEach-Object {
        "$($_.TaskPath)$($_.TaskName) | user=$($_.Principal.UserId) group=$($_.Principal.GroupId) logon=$($_.Principal.LogonType) runLevel=$($_.Principal.RunLevel) state=$($_.State)"
        ($_.Actions | ForEach-Object { "    action: $($_.Execute) $($_.Arguments)" })
        ($_.Triggers | ForEach-Object { "    trigger: $($_.CimClass.CimClassName)" })
    }
}

W 'Registry: can the elevated token open these keys for writing? (nothing is written)' {
    $rights = [Security.AccessControl.RegistryRights]::ReadKey -bor
              [Security.AccessControl.RegistryRights]::SetValue -bor
              [Security.AccessControl.RegistryRights]::CreateSubKey
    $keys = @(
        'SOFTWARE',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\InboxApplications',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Staged',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Config',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel',
        'SOFTWARE\Microsoft\Windows\CurrentVersion\AppReadiness'
    )
    foreach ($k in $keys) {
        try {
            $h = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($k, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, $rights)
            if ($null -eq $h) { "MISSING  HKLM\$k" } else { "WRITE-OK HKLM\$k"; $h.Close() }
        } catch {
            # Unwrap PowerShell's MethodInvocationException to the real cause.
            $ex = $_.Exception
            while ($ex.InnerException) { $ex = $ex.InnerException }
            "REFUSED  HKLM\$k inner={0} HResult=0x{1:X8} msg={2}" -f $ex.GetType().FullName, $ex.HResult, $ex.Message
        }
    }
}

W 'Registry: same keys through RegOpenKeyEx (Win32 error code; nothing is written)' {
    if (-not ('NitrOSVerify.Reg' -as [type])) {
        Add-Type -Namespace NitrOSVerify -Name Reg -MemberDefinition @'
[DllImport("advapi32.dll", CharSet=CharSet.Unicode)] public static extern int RegOpenKeyExW(IntPtr h, string sub, int opt, int sam, out IntPtr res);
[DllImport("advapi32.dll")] public static extern int RegCloseKey(IntPtr h);
'@
    }
    $hklm = [IntPtr]::new(-2147483646)   # HKEY_LOCAL_MACHINE
    $sam = 0x20019 -bor 0x0002 -bor 0x0004 # KEY_READ | KEY_SET_VALUE | KEY_CREATE_SUB_KEY
    foreach ($k in 'SOFTWARE', 'SOFTWARE\Policies', 'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx',
                   'SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\InboxApplications',
                   'SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel') {
        $h = [IntPtr]::Zero
        $rc = [NitrOSVerify.Reg]::RegOpenKeyExW($hklm, $k, 0, $sam, [ref]$h)
        if ($rc -eq 0) { "WRITE-OK HKLM\$k"; [void][NitrOSVerify.Reg]::RegCloseKey($h) }
        else { "REFUSED  HKLM\$k Win32 error $rc ($([ComponentModel.Win32Exception]::new($rc).Message))" }
    }
}

W 'Registry: owner and SDDL of the Appx keys' {
    foreach ($k in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx',
                   'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore') {
        $a = Get-Acl $k
        "$k"
        "  owner: $($a.Owner)"
        "  sddl:  $($a.Sddl)"
    }
}

W 'DISM: feature query (expected to work)' {
    dism.exe /English /Online /Get-FeatureInfo /FeatureName:Printing-PrintToPDFServices-Features
    "exit code: $LASTEXITCODE"
}

W 'DISM: provisioned package query (the failing one)' {
    dism.exe /English /Online /Get-ProvisionedAppxPackages
    "exit code: $LASTEXITCODE"
}

W 'PowerShell: Get-AppxProvisionedPackage -Online' {
    try {
        $r = Get-AppxProvisionedPackage -Online -ErrorAction Stop
        "returned $(@($r).Count) packages"
    } catch {
        "{0}: {1} HResult=0x{2:X8}" -f $_.Exception.GetType().FullName, $_.Exception.Message, $_.Exception.HResult
    }
}

W 'DISM log lines written by this run' {
    $cut = $started.AddSeconds(-2)
    Get-Content 'C:\Windows\Logs\DISM\dism.log' | Where-Object {
        $_ -match '^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})' -and ([datetime]$Matches[1]) -ge $cut -and
        $_ -match 'Error|Warning|Appx|hr:0x|Software key'
    }
}

W 'AppX deployment events since this run started' {
    foreach ($log in 'Microsoft-Windows-AppXDeploymentServer/Operational', 'Microsoft-Windows-AppxPackaging/Operational') {
        "--- $log"
        Get-WinEvent -FilterHashtable @{ LogName = $log; StartTime = $started.AddSeconds(-2) } -ErrorAction SilentlyContinue |
            Where-Object { $_.Level -le 3 } | Select-Object -First 20 TimeCreated, Id, LevelDisplayName, Message | Format-List
    }
}

Add-Content -Path $outPath -Encoding utf8 -Value "`r`nDone $((Get-Date).ToString('o'))"
"Wrote $outPath"
