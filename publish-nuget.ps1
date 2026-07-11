#! /usr/bin/pwsh

<#
.SYNOPSIS
    Builds (Release) and publishes the ConnectedDevice.NET NuGet packages
    to the SparkEVO GitHub Packages feed.

.DESCRIPTION
    Packs ConnectedDevice.NET, ConnectedDevice.NET.Android and
    ConnectedDevice.NET.iOS in Release configuration and pushes the
    resulting .nupkg files to https://nuget.pkg.github.com/SparkEVO/index.json

.PARAMETER ApiKey
    The GitHub Personal Access Token (PAT) with 'write:packages' scope.
    If omitted, the script falls back to the GITHUB_TOKEN environment variable.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER OutputDir
    Folder where the .nupkg files are written. Default: ./nupkgs

.PARAMETER SkipIos
    Skip the iOS project (useful when building without the iOS workload, e.g. on Linux).

.PARAMETER AndroidSdkDir
    Path to the Android SDK. If omitted, the script uses ANDROID_HOME / ANDROID_SDK_ROOT,
    or falls back to "$HOME/Android/Sdk" if it exists.

.EXAMPLE
    ./publish-nuget.ps1 -ApiKey ghp_xxxxxxxxxxxx

.EXAMPLE
    $env:GITHUB_TOKEN = "ghp_xxxx"; ./publish-nuget.ps1
#>

[CmdletBinding()]
param(
    [string] $ApiKey = $env:GITHUB_TOKEN,
    [string] $Configuration = "Release",
    [string] $OutputDir = "./nupkgs",
    [string] $AndroidSdkDir = $(if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }),
    [switch] $SkipIos
)

$ErrorActionPreference = "Stop"

$Source = "https://nuget.pkg.github.com/SparkEVO/index.json"

# --- Resolve Android SDK --------------------------------------------------
if ([string]::IsNullOrWhiteSpace($AndroidSdkDir)) {
    $fallback = Join-Path $HOME "Android/Sdk"
    if (Test-Path $fallback) { $AndroidSdkDir = $fallback }
}

$projects = @(
    "ConnectedDevice.NET/ConnectedDevice.NET.csproj",
    "ConnectedDevice.NET.Android/ConnectedDevice.NET.Android.csproj"
)
if (-not $SkipIos) {
    $projects += "ConnectedDevice.NET.iOS/ConnectedDevice.NET.iOS.csproj"
}

# --- Validate credentials -------------------------------------------------
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Error "No token provided. Pass -ApiKey <PAT> or set the GITHUB_TOKEN environment variable."
    exit 1
}

# --- Clean output ---------------------------------------------------------
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# --- Pack -----------------------------------------------------------------
foreach ($proj in $projects) {
    Write-Host "==> Packing $proj ($Configuration)" -ForegroundColor Cyan

    $extraArgs = @()
    if ($proj -like "*Android*" -and -not [string]::IsNullOrWhiteSpace($AndroidSdkDir)) {
        $extraArgs += "-p:AndroidSdkDirectory=$AndroidSdkDir"
    }

    dotnet pack $proj `
        --configuration $Configuration `
        --output $OutputDir `
        @extraArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Pack failed for $proj"
        exit $LASTEXITCODE
    }
}

# --- Push -----------------------------------------------------------------
$packages = Get-ChildItem -Path $OutputDir -Filter *.nupkg
if ($packages.Count -eq 0) {
    Write-Error "No .nupkg packages were generated in $OutputDir"
    exit 1
}

foreach ($pkg in $packages) {
    Write-Host "==> Pushing $($pkg.Name)" -ForegroundColor Green
    dotnet nuget push $pkg.FullName `
        --source $Source `
        --api-key $ApiKey `
        --skip-duplicate
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Push failed for $($pkg.Name)"
        exit $LASTEXITCODE
    }
}

Write-Host "`nDone. $($packages.Count) packages published to $Source" -ForegroundColor Green
