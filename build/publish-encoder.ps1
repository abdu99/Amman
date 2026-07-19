#Requires -Version 5.1
<#
  Publishes Aman.Encoder as a self-contained, single-file win-x64
  executable. Requires the Player.exe stub to already be in place
  (run publish-player.ps1 first).
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$encoderProj = Join-Path $root "src/Aman.Encoder/Aman.Encoder.csproj"
$stub = Join-Path $root "src/Aman.Encoder/Resources/PlayerStub/Player.exe"

if (-not (Test-Path $stub)) {
    throw "Player.exe stub not found at $stub. Run build/publish-player.ps1 first."
}

Write-Host "==> Publishing Aman.Encoder (self-contained, single-file, win-x64)" -ForegroundColor Cyan
dotnet publish $encoderProj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=false

$out = Join-Path $root "src/Aman.Encoder/bin/Release/net8.0-windows/win-x64/publish/Aman.Encoder.exe"
Write-Host "==> Done: $out" -ForegroundColor Green
