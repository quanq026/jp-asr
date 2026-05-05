param(
    [string]$WhisperReleaseApi = "https://api.github.com/repos/ggml-org/whisper.cpp/releases/latest",
    [string]$FfmpegReleaseApi = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest",
    [switch]$SkipModels
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$tools = Join-Path $root ".tools"
$bin = Join-Path $root "bin"
$models = Join-Path $root "models"

New-Item -ItemType Directory -Force -Path $tools, $bin, $models | Out-Null

function Download-File {
    param([string]$Uri, [string]$OutFile)
    if (Test-Path $OutFile) {
        Write-Host "Already exists: $OutFile"
        return
    }

    Write-Host "Downloading $Uri"
    Invoke-WebRequest -Uri $Uri -OutFile $OutFile
}

function Expand-ZipFresh {
    param([string]$ZipPath, [string]$Destination)
    if (Test-Path $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Expand-Archive -Path $ZipPath -DestinationPath $Destination -Force
}

Write-Host "Preparing whisper.cpp..."
$whisperRelease = Invoke-RestMethod -Uri $WhisperReleaseApi
$whisperAsset = $whisperRelease.assets | Where-Object { $_.name -eq "whisper-bin-x64.zip" } | Select-Object -First 1
if (-not $whisperAsset) {
    throw "Cannot find whisper-bin-x64.zip in latest whisper.cpp release."
}
$whisperZip = Join-Path $tools $whisperAsset.name
$whisperExtract = Join-Path $tools "whisper-bin-x64"
Download-File -Uri $whisperAsset.browser_download_url -OutFile $whisperZip
Expand-ZipFresh -ZipPath $whisperZip -Destination $whisperExtract
$whisperCli = Get-ChildItem -Path $whisperExtract -Recurse -Filter "whisper-cli.exe" | Select-Object -First 1
if (-not $whisperCli) {
    throw "whisper-cli.exe not found after extracting whisper.cpp release."
}
Copy-Item -Path (Join-Path $whisperCli.DirectoryName "*") -Destination $bin -Recurse -Force

Write-Host "Preparing FFmpeg..."
$ffmpegRelease = Invoke-RestMethod -Uri $FfmpegReleaseApi
$ffmpegAsset = $ffmpegRelease.assets |
    Where-Object { $_.name -eq "ffmpeg-master-latest-win64-lgpl.zip" } |
    Select-Object -First 1
if (-not $ffmpegAsset) {
    throw "Cannot find ffmpeg-master-latest-win64-lgpl.zip in latest FFmpeg release."
}
$ffmpegZip = Join-Path $tools $ffmpegAsset.name
$ffmpegExtract = Join-Path $tools "ffmpeg-win64"
Download-File -Uri $ffmpegAsset.browser_download_url -OutFile $ffmpegZip
Expand-ZipFresh -ZipPath $ffmpegZip -Destination $ffmpegExtract
$ffmpegExe = Get-ChildItem -Path $ffmpegExtract -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
if (-not $ffmpegExe) {
    throw "ffmpeg.exe not found after extracting FFmpeg release."
}
Copy-Item -Path $ffmpegExe.FullName -Destination (Join-Path $bin "ffmpeg.exe") -Force

if (-not $SkipModels) {
    Write-Host "Preparing Whisper models..."
    Download-File `
        -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin" `
        -OutFile (Join-Path $models "ggml-small-q5_1.bin")
    Download-File `
        -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium-q5_0.bin" `
        -OutFile (Join-Path $models "ggml-medium-q5_0.bin")
}

Write-Host "Runtime files prepared."
