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

.PARAMETER Platforms
    Which platform-specific packages to build and publish, as a comma-separated list.
    Accepted values: "android", "ios". Default: both.
    The core ConnectedDevice.NET package is always built regardless of this value.
    Example: -Platforms android  (useful when building without the iOS workload, e.g. on Linux).

.PARAMETER AndroidSdkDir
    Path to the Android SDK. If omitted, the script uses ANDROID_HOME / ANDROID_SDK_ROOT,
    or falls back to "$HOME/Android/Sdk" if it exists.

.EXAMPLE
    ./publish-nuget.ps1 -ApiKey ghp_xxxxxxxxxxxx

.EXAMPLE
    # Build/publish only the core and Android packages
    ./publish-nuget.ps1 -Platforms android

.EXAMPLE
    $env:GITHUB_TOKEN = "ghp_xxxx"; ./publish-nuget.ps1
#>

[CmdletBinding()]
param(
    [string] $ApiKey = $env:GITHUB_TOKEN,
    [string] $Configuration = "Release",
    [string] $OutputDir = "./nupkgs",
    [string] $AndroidSdkDir = $(if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }),
    [ValidateSet("android", "ios")]
    [string[]] $Platforms = @("android", "ios")
)

$ErrorActionPreference = "Stop"

$Source = "https://nuget.pkg.github.com/SparkEVO/index.json"

# --- Resolve Android SDK --------------------------------------------------
if ([string]::IsNullOrWhiteSpace($AndroidSdkDir)) {
    $fallback = Join-Path $HOME "Android/Sdk"
    if (Test-Path $fallback) { $AndroidSdkDir = $fallback }
}

# Normalize the requested platforms (case-insensitive, de-duplicated).
$selectedPlatforms = @($Platforms | ForEach-Object { $_.ToLowerInvariant() } | Select-Object -Unique)

# The core package is always built; platform packages depend on -Platforms.
$projects = @(
    "ConnectedDevice.NET/ConnectedDevice.NET.csproj"
)
if ($selectedPlatforms -contains "android") {
    $projects += "ConnectedDevice.NET.Android/ConnectedDevice.NET.Android.csproj"
}
if ($selectedPlatforms -contains "ios") {
    $projects += "ConnectedDevice.NET.iOS/ConnectedDevice.NET.iOS.csproj"
}

Write-Host "==> Building for platforms: $($selectedPlatforms -join ', ')" -ForegroundColor Cyan

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
