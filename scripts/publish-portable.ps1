param(
    [string]$Configuration = "Release",
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$publishDir = Join-Path $root "dist\JapaneseASR-portable"
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"

if ($DotnetPath -eq "dotnet" -and (Test-Path $localDotnet)) {
    $DotnetPath = $localDotnet
}

if (-not (Test-Path (Join-Path $root "bin\ffmpeg.exe"))) {
    throw "Missing bin\ffmpeg.exe. Run scripts\prepare-runtime.ps1 first."
}
if (-not (Test-Path (Join-Path $root "bin\whisper-cli.exe"))) {
    throw "Missing bin\whisper-cli.exe. Run scripts\prepare-runtime.ps1 first."
}
if (-not ((Test-Path (Join-Path $root "models\ggml-small-q5_0.bin")) -or (Test-Path (Join-Path $root "models\ggml-small-q5_1.bin")) -or (Test-Path (Join-Path $root "models\ggml-small-q8_0.bin")))) {
    throw "Missing small model. Run scripts\prepare-runtime.ps1 first."
}
if (-not (Test-Path (Join-Path $root "models\ggml-medium-q5_0.bin"))) {
    throw "Missing models\ggml-medium-q5_0.bin. Run scripts\prepare-runtime.ps1 first."
}

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

& $DotnetPath publish (Join-Path $root "JapaneseASR.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $publishDir

New-Item -ItemType Directory -Force -Path `
    (Join-Path $publishDir "output"), `
    (Join-Path $publishDir "logs"), `
    (Join-Path $publishDir "config") | Out-Null

Write-Host "Portable package created:"
Write-Host $publishDir
