#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds and packs Zakira.Exchange for NuGet publishing.
.DESCRIPTION
    Cleans, builds in Release mode, runs tests, and produces a .nupkg (and .snupkg)
    ready to push to NuGet. The package is output to the nupkgs/ directory at the repo root.
    Optionally pushes the resulting packages to nuget.org with -Push.
.PARAMETER OutputDir
    Directory to place the .nupkg file. Default: <repo-root>/nupkgs
.PARAMETER SkipTests
    Skip running tests before packing.
.PARAMETER Version
    Override the package version (e.g. 1.0.0). If omitted, uses the version from the project file.
.PARAMETER Push
    After packing, push the generated *.nupkg files to nuget.org.
.PARAMETER ApiKey
    NuGet API key for -Push. Falls back to the NUGET_API_KEY environment variable when omitted.
.PARAMETER Source
    NuGet source URL for -Push. Defaults to https://api.nuget.org/v3/index.json
.EXAMPLE
    ./scripts/pack.ps1
.EXAMPLE
    ./scripts/pack.ps1 -Version 1.2.0
.EXAMPLE
    ./scripts/pack.ps1 -SkipTests
.EXAMPLE
    ./scripts/pack.ps1 -Push                           # uses $env:NUGET_API_KEY
.EXAMPLE
    ./scripts/pack.ps1 -Push -ApiKey oy2xxxxxxx        # explicit key
#>
param(
    [string]$OutputDir,
    [switch]$SkipTests,
    [string]$Version,
    [switch]$Push,
    [string]$ApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repoRoot "Zakira.Exchange.slnx"
$cliProject = Join-Path $repoRoot "src" "Zakira.Exchange.Cli" "Zakira.Exchange.Cli.csproj"

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "nupkgs"
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

# --- Clean ---
Write-Host "Cleaning solution..." -ForegroundColor Cyan
dotnet clean $solution --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Clean failed."
    exit 1
}

# --- Build ---
Write-Host "Building in Release mode..." -ForegroundColor Cyan
dotnet build $solution --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

# --- Test ---
if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test $solution --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed. Use -SkipTests to bypass."
        exit 1
    }
} else {
    Write-Host "Skipping tests (-SkipTests)." -ForegroundColor Yellow
}

# --- Pack ---
Write-Host "Packing..." -ForegroundColor Cyan

$packArgs = @(
    "pack"
    $cliProject
    "--configuration", "Release"
    "--no-build"
    "--output", $OutputDir
    "--include-symbols"
    "-p:SymbolPackageFormat=snupkg"
)

if ($Version) {
    $packArgs += "-p:PackageVersion=$Version"
    $packArgs += "-p:Version=$Version"
}

& dotnet @packArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Pack failed."
    exit 1
}

# --- Done ---
Write-Host ""
Write-Host "Package created in: $OutputDir" -ForegroundColor Green
$packages = Get-ChildItem $OutputDir -Filter "*.nupkg"
$packages | ForEach-Object {
    Write-Host "  $($_.Name)  ($([math]::Round($_.Length / 1KB, 1)) KB)" -ForegroundColor Green
}

# --- Push ---
if ($Push) {
    if (-not $ApiKey) {
        $ApiKey = $env:NUGET_API_KEY
    }
    if (-not $ApiKey) {
        Write-Error "Pack succeeded, but -Push requires -ApiKey or the NUGET_API_KEY environment variable to be set."
        exit 1
    }

    if (-not $packages) {
        Write-Error "No .nupkg files found in $OutputDir to push."
        exit 1
    }

    Write-Host ""
    Write-Host "Pushing $($packages.Count) package(s) to $Source..." -ForegroundColor Cyan
    foreach ($pkg in $packages) {
        Write-Host "  -> $($pkg.Name)" -ForegroundColor Cyan
        dotnet nuget push $pkg.FullName `
            --api-key $ApiKey `
            --source $Source `
            --skip-duplicate
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Push failed for $($pkg.Name)."
            exit 1
        }
    }
    Write-Host ""
    Write-Host "Push complete." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "To push to NuGet:" -ForegroundColor Cyan
    Write-Host "  ./scripts/pack.ps1 -Push                  # uses `$env:NUGET_API_KEY"
    Write-Host "  ./scripts/pack.ps1 -Push -ApiKey <key>    # explicit key"
}
