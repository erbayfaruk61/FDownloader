param(
    [string]$OutputDir = "./dist"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$rootExtensionDir = $scriptDir

Write-Host "FDownloader Eklenti Paketleme Baslatiliyor..." -ForegroundColor Cyan

# Cikti klasorlerini hazirla
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$outPath = (Resolve-Path -Path $OutputDir).Path

$chromeDist = Join-Path $outPath "chrome"
$firefoxDist = Join-Path $outPath "firefox"

if (Test-Path $chromeDist) { Remove-Item -Recurse -Force $chromeDist }
if (Test-Path $firefoxDist) { Remove-Item -Recurse -Force $firefoxDist }

New-Item -ItemType Directory -Path $chromeDist -Force | Out-Null
New-Item -ItemType Directory -Path $firefoxDist -Force | Out-Null

# Ortak dosyalari kopyala
$commonFiles = @("background.js", "content.js", "popup.html", "popup.js", "KURULUM_REHBERI.md")
foreach ($file in $commonFiles) {
    $src = Join-Path $rootExtensionDir $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $chromeDist -Force
        Copy-Item -Path $src -Destination $firefoxDist -Force
    }
}

# Ikonlari kopyala
$iconsSrc = Join-Path $rootExtensionDir "icons"
if (Test-Path $iconsSrc) {
    Copy-Item -Recurse -Path $iconsSrc -Destination (Join-Path $chromeDist "icons") -Force
    Copy-Item -Recurse -Path $iconsSrc -Destination (Join-Path $firefoxDist "icons") -Force
}

# 1. Chromium Manifest kopyala
Copy-Item -Path (Join-Path $rootExtensionDir "manifest.json") -Destination (Join-Path $chromeDist "manifest.json") -Force

# 2. Firefox Manifest kopyala (manifest.firefox.json -> manifest.json olarak adlandirilir)
Copy-Item -Path (Join-Path $rootExtensionDir "manifest.firefox.json") -Destination (Join-Path $firefoxDist "manifest.json") -Force

# ZIP paketlerini uret
$chromeZip = Join-Path $outPath "FDownloader-Extension-Chrome.zip"
$firefoxZip = Join-Path $outPath "FDownloader-Extension-Firefox.zip"

if (Test-Path $chromeZip) { Remove-Item -Force $chromeZip }
if (Test-Path $firefoxZip) { Remove-Item -Force $firefoxZip }

Compress-Archive -Path "$chromeDist\*" -DestinationPath $chromeZip -Force
Compress-Archive -Path "$firefoxDist\*" -DestinationPath $firefoxZip -Force

Write-Host "Chrome Eklentisi: $chromeZip" -ForegroundColor Green
Write-Host "Firefox Eklentisi: $firefoxZip" -ForegroundColor Green
Write-Host "Tum eklenti paketleri basariyla olusturuldu." -ForegroundColor Cyan
