[CmdletBinding()]
param(
    [string]$SourceIso,
    [string]$VmName = "nitros-builder",
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Import-Module (Join-Path $PSScriptRoot "NitrOS.Measure.psm1") -Force

try {
    $config = Get-MeasureConfig -Path $ConfigPath
    if (-not $SourceIso) {
        $SourceIso = $config.IsoPath
    }

    # 1. Restore 'builder-ready' checkpoint
    Write-Host "Restoring checkpoint 'builder-ready' on '$VmName'..."
    Restore-VMSnapshot -VMName $VmName -Name "builder-ready" -Confirm:$false | Out-Null

    # 2. Point DVD drive at source ISO
    if ($SourceIso) {
        Write-Host "Mounting source ISO in DVD drive: '$SourceIso'..."
        $dvd = Get-VMDvdDrive -VMName $VmName | Select-Object -First 1
        if ($dvd) {
            Set-VMDvdDrive -VMName $VmName -ControllerNumber $dvd.ControllerNumber -ControllerLocation $dvd.ControllerLocation -Path $SourceIso | Out-Null
        }
    }

    # 3. Publish tests self-contained for win-x64
    $testPublishDir = Join-Path $repoRoot "measure\work\guest\image-tests"
    Write-Host "Publishing NitrOS.Image.Tests to $testPublishDir..."
    dotnet publish (Join-Path $repoRoot "tests\NitrOS.Image.Tests\NitrOS.Image.Tests.csproj") `
        -c Release -r win-x64 --self-contained true -o $testPublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish NitrOS.Image.Tests."
    }

    # 4. Start VM
    Write-Host "Starting '$VmName'..."
    Start-VM -VMName $VmName
    $session = Get-GuestSession -VmName $VmName -Credential $config.Credential -TimeoutMinutes 10

    # 5. Copy tests into guest
    Write-Host "Copying tests into guest C:\NitrOS.Measure\tests\..."
    Copy-VMFile -VMName $VmName -SourcePath (Join-Path $testPublishDir "*") -DestinationPath "C:\NitrOS.Measure\tests\" -CreateFullPath -FileSource Host -Force

    # 6. Run tests with NITROS_VM=1
    Write-Host "Running VM-lane tests in guest..."
    $cmd = "cmd.exe /c set NITROS_VM=1 && C:\NitrOS.Measure\tests\NitrOS.Image.Tests.exe --report-trx --report-trx-filename C:\NitrOS.Measure\tests\results.trx"
    $taskResult = Invoke-GuestTask -Session $session -Name "run-image-tests" -CommandLine $cmd -TimeoutMinutes 30

    # 7. Copy report out
    $reportDir = Join-Path $repoRoot "measure\work\reports"
    New-Item -ItemType Directory -Force $reportDir | Out-Null
    $destReport = Join-Path $reportDir "image-tests-$((Get-Date).ToString('yyyyMMdd-HHmmss')).trx"
    Copy-VMFile -VMName $VmName -SourcePath "C:\NitrOS.Measure\tests\results.trx" -DestinationPath $destReport -FileSource Guest -Force -ErrorAction SilentlyContinue

    Write-Host "Test run exit code: $($taskResult.ExitCode)"
    if ($taskResult.ExitCode -ne 0) {
        Write-Host "VM-lane tests failed."
        exit 1
    }

    Write-Host "VM-lane tests passed successfully."
    exit 0
} catch {
    Write-Error $_.Exception.Message
    Write-Host "Failure reason: $($_.Exception.Message)"
    exit 1
}
