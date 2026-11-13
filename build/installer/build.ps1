<#
  Builds SpartanX-Setup-<Version>-<Arch>.exe (+ .sha256) into ./dist with Inno Setup.
  Installs Inno Setup through Chocolatey when it is missing (GitHub runners, fresh PCs).
  Usage: ./build/installer/build.ps1 -Version 0.1.2 -Arch x64 -SourceDir out/win-x64
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [ValidateSet('x64', 'arm64')] [string] $Arch,
    [Parameter(Mandatory)] [string] $SourceDir
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$source = Resolve-Path $SourceDir

if (-not (Test-Path (Join-Path $source 'SpartanX.exe'))) {
    throw "SpartanX.exe not found in $source - run dotnet publish first."
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host 'Installing Inno Setup...'
    choco install innosetup -y --no-progress | Out-Host
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}

# Inno's VersionInfoVersion must be numeric (a.b.c); strip any pre-release suffix.
$numeric = ($Version -split '[-+]')[0]

& $iscc "/DAppVersion=$Version" "/DNumericVersion=$numeric" "/DArch=$Arch" "/DSourceDir=$source" (Join-Path $PSScriptRoot 'SpartanX.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }

$setup = Join-Path $root "dist\SpartanX-Setup-$Version-$Arch.exe"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()
"$hash  $(Split-Path $setup -Leaf)" | Out-File "$setup.sha256" -Encoding ascii
Write-Host "Built $setup"
