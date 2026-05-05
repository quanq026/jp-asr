param(
    [string]$Channel = "8.0",
    [string]$InstallDir = (Join-Path $PSScriptRoot "..\.dotnet")
)

$ErrorActionPreference = "Stop"
$installDirFull = [System.IO.Path]::GetFullPath($InstallDir)
$scriptPath = Join-Path $env:TEMP "dotnet-install.ps1"

Write-Host "Downloading dotnet-install.ps1..."
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $scriptPath

Write-Host "Installing latest .NET SDK from channel $Channel to $installDirFull..."
& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -Channel $Channel -InstallDir $installDirFull

Write-Host ""
Write-Host "Installed. Use:"
Write-Host "$installDirFull\dotnet.exe --info"
