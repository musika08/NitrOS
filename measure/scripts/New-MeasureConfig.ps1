[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IsoPath,
    [string]$VmRoot = "X:\NitrOS-VM",
    [string]$VmName = "nitros-measure",
    [string]$User = "nitros-test",
    [string]$ConfigPath = "measure\measure.local.json"
)

$ErrorActionPreference = 'Stop'

if (Test-Path $ConfigPath) {
    throw "Refusing to overwrite existing configuration file at '$ConfigPath'."
}

if (-not (Test-Path $IsoPath)) {
    throw "Windows ISO file not found at '$IsoPath'."
}

# Generate 24-character password using RandomNumberGenerator
$chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+"
$bytes = [byte[]]::new(24)
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()

$sb = [System.Text.StringBuilder]::new(24)
foreach ($b in $bytes) {
    $sb.Append($chars[$b % $chars.Length]) | Out-Null
}
$plainPassword = $sb.ToString()

# Protect with DPAPI for current user
$secString = ConvertTo-SecureString $plainPassword -AsPlainText -Force
$protectedString = ConvertFrom-SecureString $secString

$config = [PSCustomObject]@{
    isoPath           = (Resolve-Path $IsoPath).Path
    vmRoot            = $VmRoot
    vmName            = $VmName
    checkpoint        = "clean"
    user              = $User
    passwordProtected = $protectedString
}

$dir = Split-Path -Parent $ConfigPath
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Force $dir | Out-Null
}

$config | ConvertTo-Json -Depth 4 | Set-Content -Path $ConfigPath -Encoding UTF8
Write-Host "Created measure configuration at '$ConfigPath'."
