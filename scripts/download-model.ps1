#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downloads the all-MiniLM-L6-v2 ONNX model and vocabulary file for Zakira.Exchange.
.DESCRIPTION
    Downloads the ONNX model (~90MB) and vocab.txt (~230KB) from HuggingFace
    into the specified output directory. Defaults to src/Zakira.Exchange.Core/Models/.
.PARAMETER OutputDir
    Directory to download the model files into. Created if it doesn't exist.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot ".." "src" "Zakira.Exchange.Core" "Models")
)

$ErrorActionPreference = "Stop"

$baseUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main"
$files = @(
    @{ Name = "all-MiniLM-L6-v2.onnx"; Url = "$baseUrl/onnx/model.onnx" },
    @{ Name = "vocab.txt";             Url = "$baseUrl/vocab.txt" }
)

# Resolve to absolute path
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

if (-not (Test-Path $OutputDir)) {
    Write-Host "Creating output directory: $OutputDir"
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

foreach ($file in $files) {
    $dest = Join-Path $OutputDir $file.Name
    if (Test-Path $dest) {
        Write-Host "Already exists, skipping: $($file.Name)"
        continue
    }

    Write-Host "Downloading $($file.Name) ..."
    try {
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $file.Url -OutFile $dest -UseBasicParsing
        $size = (Get-Item $dest).Length
        Write-Host "  Downloaded: $($file.Name) ($([math]::Round($size / 1MB, 2)) MB)"
    }
    catch {
        Write-Error "Failed to download $($file.Name): $_"
        if (Test-Path $dest) { Remove-Item $dest }
        exit 1
    }
}

Write-Host ""
Write-Host "Model files are ready in: $OutputDir"
Write-Host "You can use --model-path to point to the .onnx file, or place these files"
Write-Host "alongside the Zakira executable in a 'Models' subdirectory."
