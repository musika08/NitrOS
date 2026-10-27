[CmdletBinding()]
param(
    [string]$OutputDir = "measure/work/guest"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName

$nitrosOut = Join-Path $repoRoot "$OutputDir/nitros"
$probeOut = Join-Path $repoRoot "$OutputDir/probe"
$commitFile = Join-Path $repoRoot "$OutputDir/commit.txt"

New-Item -ItemType Directory -Force (Join-Path $repoRoot $OutputDir) | Out-Null

Write-Host "Publishing NitrOS.Cli to $nitrosOut..."
dotnet publish (Join-Path $repoRoot "src/NitrOS.Cli/NitrOS.Cli.csproj") `
    -c Release -r win-x64 --self-contained true -o $nitrosOut

Write-Host "Publishing NitrOS.Measure.Probe to $probeOut..."
dotnet publish (Join-Path $repoRoot "src/NitrOS.Measure.Probe/NitrOS.Measure.Probe.csproj") `
    -c Release -r win-x64 --self-contained true -o $probeOut

$sha = (git -C $repoRoot rev-parse HEAD).Trim()
Set-Content -Path $commitFile -Value $sha -Encoding UTF8
Write-Host "Guest tools published for commit $sha."
