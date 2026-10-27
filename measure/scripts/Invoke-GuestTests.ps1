[CmdletBinding()]
param(
    [string]$VmName = "nitros-measure",
    [string]$Checkpoint = "clean",
    [string]$Filter = "",
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Import-Module (Join-Path $PSScriptRoot "NitrOS.Measure.psm1") -Force

$config = Get-MeasureConfig -Path $ConfigPath
$workDir = Join-Path $repoRoot "measure\work\vm-tests"
New-Item -ItemType Directory -Force $workDir | Out-Null

Write-Host "Publishing NitrOS.Live.Tests self-contained..."
$pubDir = Join-Path $workDir "published"
dotnet publish "$repoRoot\tests\NitrOS.Live.Tests\NitrOS.Live.Tests.csproj" `
    -c Release -r win-x64 --self-contained true -o $pubDir

Write-Host "Restoring checkpoint '$Checkpoint'..."
Stop-VM -VMName $VmName -Force -ErrorAction SilentlyContinue
Restore-VMSnapshot -VMName $VmName -Name $Checkpoint -Confirm:$false | Out-Null
Connect-VMNetworkAdapter -VMName $VmName -SwitchName "Default Switch" -ErrorAction SilentlyContinue | Out-Null
Start-VM -VMName $VmName

$session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 15

Write-Host "Deploying test runner into guest..."
Copy-Item -ToSession $session -Path "$pubDir\*" -Destination "C:\NitrOS.Measure\tests" -Recurse -Force | Out-Null

$testCmd = "set NITROS_VM=1&& C:\NitrOS.Measure\tests\NitrOS.Live.Tests.exe"
if ($Filter) {
    $testCmd += " --filter `"$Filter`""
}

Write-Host "Running tests in VM: $testCmd"
$taskRes = Invoke-GuestTask -Session $session -Name "VmTests" -CommandLine $testCmd -TimeoutMinutes 30

$logOut = Join-Path $workDir "vm-tests.log"
Set-Content -Path $logOut -Value $taskRes.Log -Encoding UTF8

Write-Host "Tests finished with exit code $($taskRes.ExitCode)."
Write-Host "Log saved to '$logOut'."

Write-Host "Cleaning up and restoring checkpoint '$Checkpoint'..."
Stop-VM -VMName $VmName -Force -ErrorAction SilentlyContinue
Restore-VMSnapshot -VMName $VmName -Name $Checkpoint -Confirm:$false | Out-Null

exit $taskRes.ExitCode
