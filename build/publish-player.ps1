#Requires -Version 5.1
<#
  Publishes Aman.Player as a self-contained, single-file win-x64 executable
  and copies the result into Aman.Encoder's PlayerStub folder, where the
  Encoder appends encrypted container data to it at export time.

  Run this BEFORE publish-encoder.ps1 — the Encoder embeds this exact file.
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$playerProj = Join-Path $root "src/Aman.Player/Aman.Player.csproj"
$publishDir = Join-Path $root "src/Aman.Player/bin/Release/net8.0-windows/win-x64/publish"
$stubDir = Join-Path $root "src/Aman.Encoder/Resources/PlayerStub"

Write-Host "==> Publishing Aman.Player (self-contained, single-file, win-x64)" -ForegroundColor Cyan
dotnet publish $playerProj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=false

$builtExe = Join-Path $publishDir "Player.exe"
if (-not (Test-Path $builtExe)) {
    throw "Expected output not found: $builtExe"
}

New-Item -ItemType Directory -Force -Path $stubDir | Out-Null
Copy-Item $builtExe (Join-Path $stubDir "Player.exe") -Force

Write-Host "==> Player.exe stub copied to $stubDir" -ForegroundColor Green
