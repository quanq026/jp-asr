param(
    [switch]$SkipModels
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$dotnetExe = Join-Path $root ".dotnet\dotnet.exe"
$startTime = Get-Date

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Japanese ASR - All-in-One Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ──────────────────────────────────────────
# Step 1: Install .NET SDK 8.0
# ──────────────────────────────────────────
Write-Host "[1/3] Installing .NET SDK 8.0..." -ForegroundColor Yellow
if (Test-Path $dotnetExe) {
    $version = & $dotnetExe --version 2>&1
    Write-Host "  Already installed: $dotnetExe (v$version)" -ForegroundColor Green
} else {
    & (Join-Path $root "scripts\install-dotnet-sdk.ps1")
    Write-Host "  .NET SDK 8.0 installed." -ForegroundColor Green
}
Write-Host ""

# ──────────────────────────────────────────
# Step 2: Download runtime binaries + models
# ──────────────────────────────────────────
Write-Host "[2/3] Downloading whisper.cpp, FFmpeg, and models..." -ForegroundColor Yellow
if ($SkipModels) {
    & (Join-Path $root "scripts\prepare-runtime.ps1") -SkipModels
} else {
    & (Join-Path $root "scripts\prepare-runtime.ps1")
}
Write-Host "  Runtime + models ready." -ForegroundColor Green
Write-Host ""

# ──────────────────────────────────────────
# Step 3: Build portable app
# ──────────────────────────────────────────
Write-Host "[3/3] Building portable app..." -ForegroundColor Yellow
& (Join-Path $root "scripts\publish-portable.ps1") -DotnetPath $dotnetExe
Write-Host "  Build complete." -ForegroundColor Green
Write-Host ""

# ──────────────────────────────────────────
# Done
# ──────────────────────────────────────────
$elapsed = (Get-Date) - $startTime
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Setup complete! ($($elapsed.ToString('mm\:ss')) minutes)" -ForegroundColor Green
Write-Host "  Output: $root\dist\JapaneseASR-portable\" -ForegroundColor Green
Write-Host "  Run:    $root\dist\JapaneseASR-portable\JapaneseASR.exe" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
