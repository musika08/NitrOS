# Test-CatalogKeysWritable.ps1
# Read-only check: probes write access to every HKLM key the catalog writes outside SOFTWARE\Policies.
# Never writes or modifies registry data. Writes results to $env:USERPROFILE\NitrOS-catalog-keys.txt.

[CmdletBinding()]
param(
    [string]$CatalogDir = (Join-Path $PSScriptRoot "..\..\catalog"),
    [string]$OutFile = (Join-Path $env:USERPROFILE "NitrOS-catalog-keys.txt")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CatalogDir)) {
    throw "Catalog directory not found: $CatalogDir"
}

$keys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

Get-ChildItem -Path $CatalogDir -Filter "*.json" | ForEach-Object {
    if ($_.Name -eq "cards.json") { return }
    try {
        $json = Get-Content -Raw $_.FullName | ConvertFrom-Json
        if ($json.switches) {
            foreach ($s in $json.switches) {
                if ($s.live) {
                    foreach ($act in $s.live) {
                        if (($act.type -eq "registry.set" -or $act.type -eq "registry.delete") -and $act.hive -eq "HKLM") {
                            if ($act.key -and -not ($act.key -like "SOFTWARE\Policies*")) {
                                [void]$keys.Add($act.key)
                            }
                        }
                        elseif ($act.type -eq "startup.disable" -and $act.scope -eq "machine") {
                            [void]$keys.Add("SOFTWARE\Microsoft\Windows\CurrentVersion\Run")
                        }
                    }
                }
            }
        }
    }
    catch {
        Write-Warning "Could not parse $($_.FullName): $_"
    }
}

$results = [System.Collections.Generic.List[string]]::new()
$results.Add("NitrOS Catalog HKLM Key Write Probing Report")
$results.Add("Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$results.Add("Probing $($keys.Count) keys under HKLM (outside SOFTWARE\Policies)...")
$results.Add("----------------------------------------------------------------------")

$hklm = [Microsoft.Win32.Registry]::LocalMachine

foreach ($keyPath in ($keys | Sort-Object)) {
    # Find nearest existing ancestor key
    $current = $keyPath
    $foundKey = $null
    $ancestor = $null

    while ($current -and -not $foundKey) {
        try {
            $testRead = $hklm.OpenSubKey($current, $false)
            if ($testRead -ne $null) {
                $testRead.Dispose()
                $ancestor = $current
                break
            }
        } catch { }
        $idx = $current.LastIndexOf('\')
        if ($idx -gt 0) {
            $current = $current.Substring(0, $idx)
        } else {
            break
        }
    }

    $probeTarget = if ($ancestor) { $ancestor } else { "(HKLM root)" }
    $status = "UNKNOWN"

    try {
        if ($ancestor) {
            $writeTest = $hklm.OpenSubKey($ancestor, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree)
            if ($writeTest -ne $null) {
                $writeTest.Dispose()
                $status = "WRITABLE (nearest: HKLM\$ancestor)"
            } else {
                $status = "DENIED (OpenSubKey returned null for HKLM\$ancestor)"
            }
        } else {
            $status = "NOT_FOUND"
        }
    }
    catch {
        $status = "DENIED ($($_.Exception.Message))"
    }

    $line = "$keyPath -> $status"
    $results.Add($line)
    Write-Host $line
}

[System.IO.File]::WriteAllLines($OutFile, $results, [System.Text.Encoding]::UTF8)
Write-Host "Results saved to $OutFile"
