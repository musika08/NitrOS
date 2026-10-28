[CmdletBinding()]
param(
    [string]$OutputPath = "$env:USERPROFILE\nitros-app-smoke.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$exe = Join-Path $repoRoot "src\NitrOS.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\NitrOS.App.exe"

if (-not (Test-Path $exe)) {
    throw "NitrOS.App.exe not found at '$exe'. Build src/NitrOS.App first."
}

# Ensure elevated
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "run-app-smoke.ps1 must be run elevated (Administrator required)."
}

Write-Host "Running NitrOS.App smoke test to '$OutputPath'..."
$proc = Start-Process -FilePath $exe -ArgumentList @("--smoke", "`"$OutputPath`"") -PassThru -NoNewWindow

$timeoutSeconds = 120
$sw = [System.Diagnostics.Stopwatch]::StartNew()

while (-not $proc.HasExited -and $sw.Elapsed.TotalSeconds -lt $timeoutSeconds) {
    Start-Sleep -Milliseconds 500
}

if (-not $proc.HasExited) {
    $proc.Kill()
    throw "Smoke test timed out after $timeoutSeconds seconds."
}

$proc.WaitForExit()
$exitCode = $proc.ExitCode
if ($null -eq $exitCode) {
    $exitCode = 0
}
Write-Host "Smoke test exited with code: $exitCode"

$exitCodeFile = "$OutputPath.exitcode"
[System.IO.File]::WriteAllText($exitCodeFile, "$exitCode")

if ($exitCode -ne 0) {
    throw "Smoke test failed with exit code $exitCode."
}

Write-Host "Smoke test passed successfully."
