#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds and packs Zakira.Exchange for NuGet publishing.
.DESCRIPTION
    Cleans, builds in Release mode, runs tests, and produces a .nupkg (and .snupkg)
    ready to push to NuGet. The package is output to the nupkgs/ directory at the repo root.
.PARAMETER OutputDir
    Directory to place the .nupkg file. Default: <repo-root>/nupkgs
.PARAMETER SkipTests
    Skip running tests before packing.
.PARAMETER Version
    Override the package version (e.g. 1.0.0). If omitted, uses the version from the project file.
.EXAMPLE
    ./scripts/pack.ps1
.EXAMPLE
    ./scripts/pack.ps1 -Version 1.2.0
.EXAMPLE
    ./scripts/pack.ps1 -SkipTests
#>
param(
    [string]$OutputDir,
    [switch]$SkipTests,
    [string]$Version
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
Get-ChildItem $OutputDir -Filter "*.nupkg" | ForEach-Object {
    Write-Host "  $($_.Name)  ($([math]::Round($_.Length / 1KB, 1)) KB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "To push to NuGet:" -ForegroundColor Cyan
Write-Host "  dotnet nuget push `"$OutputDir\*.nupkg`" --api-key <YOUR_API_KEY> --source https://api.nuget.org/v3/index.json"
