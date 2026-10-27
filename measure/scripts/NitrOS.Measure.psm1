function Get-MeasureConfig {
    [CmdletBinding()]
    param(
        [string]$Path = "measure\measure.local.json"
    )

    if (-not (Test-Path $Path)) {
        throw "Measure configuration file not found at '$Path'."
    }

    $raw = Get-Content $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $sec = $raw.passwordProtected | ConvertTo-SecureString
    $cred = [System.Management.Automation.PSCredential]::new($raw.user, $sec)

    return [PSCustomObject]@{
        IsoPath           = $raw.isoPath
        VmRoot            = $raw.vmRoot
        VmName            = $raw.vmName
        Checkpoint        = $raw.checkpoint
        User              = $raw.user
        Credential        = $cred
        PasswordProtected = $raw.passwordProtected
    }
}

function Get-GuestSession {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$VmName,
        [Parameter(Mandatory)][System.Management.Automation.PSCredential]$Credential,
        [int]$TimeoutMinutes = 15
    )

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        try {
            $session = New-PSSession -VMName $VmName -Credential $Credential -ErrorAction Stop
            return $session
        } catch {
            Start-Sleep -Seconds 5
        }
    }

    throw "Timed out waiting for PowerShell Direct session on VM '$VmName' after $TimeoutMinutes minutes."
}

function Invoke-GuestTask {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$CommandLine,
        [int]$TimeoutMinutes = 30
    )

    $script = {
        param($TaskName, $Cmd, $TimeoutMins)

        $taskPath = "\NitrOS.Measure\"
        $action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c $Cmd > C:\NitrOS.Measure\out\$TaskName.log 2>&1"
        $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest

        Unregister-ScheduledTask -TaskName $TaskName -TaskPath $taskPath -Confirm:$false -ErrorAction SilentlyContinue
        Register-ScheduledTask -TaskName $TaskName -TaskPath $taskPath -Action $action -Principal $principal | Out-Null
        Start-ScheduledTask -TaskName $TaskName -TaskPath $taskPath

        $limit = (Get-Date).AddMinutes($TimeoutMins)
        while ((Get-Date) -lt $limit) {
            Start-Sleep -Seconds 2
            $info = Get-ScheduledTaskInfo -TaskName $TaskName -TaskPath $taskPath
            $task = Get-ScheduledTask -TaskName $TaskName -TaskPath $taskPath
            if ($task.State -ne 'Running') {
                $logContent = ""
                $logFile = "C:\NitrOS.Measure\out\$TaskName.log"
                if (Test-Path $logFile) {
                    $logContent = Get-Content $logFile -Raw -Encoding UTF8
                }
                return [PSCustomObject]@{
                    ExitCode = $info.LastTaskResult
                    Log      = $logContent
                }
            }
        }
        throw "Scheduled task '$TaskName' timed out after $TimeoutMins minutes."
    }

    return Invoke-Command -Session $Session -ScriptBlock $script -ArgumentList $Name, $CommandLine, $TimeoutMinutes
}

function Wait-GuestKvp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$VmName,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Value,
        [int]$TimeoutMinutes = 30
    )

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        $vm = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem -Filter "ElementName='$VmName'" -ErrorAction SilentlyContinue
        if ($vm) {
            $kvpComp = Get-CimAssociatedInstance -InputObject $vm -ResultClassName Msvm_KvpExchangeComponent -ErrorAction SilentlyContinue
            if ($kvpComp -and $kvpComp.GuestExchangeItems) {
                foreach ($xmlItem in $kvpComp.GuestExchangeItems) {
                    if ($xmlItem -match "<PROPERTY NAME=`"Name`" TYPE=`"string`"><VALUE>(.+?)</VALUE></PROPERTY>" -and $matches[1] -eq $Key) {
                        if ($xmlItem -match "<PROPERTY NAME=`"Data`" TYPE=`"string`"><VALUE>(.+?)</VALUE></PROPERTY>") {
                            $currentVal = $matches[1]
                            if ($currentVal -eq $Value) {
                                return $true
                            }
                            if ($currentVal -eq "error") {
                                throw "Guest reported error in KVP '$Key'."
                            }
                        }
                    }
                }
            }
        }
        Start-Sleep -Seconds 5
    }

    throw "Timed out waiting for KVP key '$Key' to become '$Value' after $TimeoutMinutes minutes."
}

function Assert-HostReadyForRun {
    [CmdletBinding()]
    param(
        [int]$MaxWaitMinutes = 30
    )

    $deadline = (Get-Date).AddMinutes($MaxWaitMinutes)
    while ((Get-Date) -lt $deadline) {
        $battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
        $onAc = $true
        if ($battery) {
            $wmiStatus = Get-CimInstance -Namespace root\wmi -ClassName BatteryStatus -ErrorAction SilentlyContinue
            $onAc = ($battery.BatteryStatus -eq 2) -or ($battery.BatteryStatus -eq 3) -or ($wmiStatus -and ($wmiStatus.PowerOnline -or $wmiStatus.Charging))
        }

        $availCounter = Get-Counter '\Memory\Available MBytes' -ErrorAction SilentlyContinue
        $availMb = if ($availCounter) { $availCounter.CounterSamples[0].CookedValue } else { 0 }

        if ($onAc -and $availMb -ge 4608) {
            return $true
        }

        Start-Sleep -Seconds 60
    }

    return $false
}

function Send-VmKeys {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$VmName,
        [int]$VirtualKey = 13, # Enter key
        [int]$Seconds = 20
    )

    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $vm = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem -Filter "ElementName='$VmName'" -ErrorAction SilentlyContinue
        if ($vm) {
            $kb = Get-CimAssociatedInstance -InputObject $vm -ResultClassName Msvm_Keyboard -ErrorAction SilentlyContinue
            if ($kb) {
                Invoke-CimMethod -InputObject $kb -MethodName TypeKey -Arguments @{ keyCode = [uint32]$VirtualKey } -ErrorAction SilentlyContinue | Out-Null
            }
        }
        Start-Sleep -Milliseconds 500
    }
}

Export-ModuleMember -Function Get-MeasureConfig, Get-GuestSession, Invoke-GuestTask, Wait-GuestKvp, Assert-HostReadyForRun, Send-VmKeys
