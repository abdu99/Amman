#Requires -Version 5.1
<#
  Publishes MultiDownloader.App as a self-contained, single-file win-x64 executable.
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src/MultiDownloader.App/MultiDownloader.App.csproj"

Write-Host "==> Publishing MultiDownloader (self-contained, single-file, win-x64)" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=false

$out = Join-Path $root "src/MultiDownloader.App/bin/Release/net8.0-windows/win-x64/publish/MultiDownloader.exe"
Write-Host "==> Done: $out" -ForegroundColor Green
Write-Host "Note: yt-dlp.exe is fetched automatically on first run (see docs/BUILD.md); ffmpeg is detected but not bundled." -ForegroundColor Yellow
