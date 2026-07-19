#Requires -Version 5.1
<# Runs the full build sequence: Player first, then Encoder (which embeds it). #>
$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "publish-player.ps1")
& (Join-Path $PSScriptRoot "publish-encoder.ps1")

Write-Host ""
Write-Host "Build complete." -ForegroundColor Green
Write-Host "Aman.Encoder.exe: src/Aman.Encoder/bin/Release/net8.0-windows/win-x64/publish/Aman.Encoder.exe"
Write-Host "Use it to produce branded, password-protected Player.exe packages for your end users."
