<#
.SYNOPSIS
    FDownloader Sürüm Güncelleme ve Dağıtım Eşitleme Aracı
.DESCRIPTION
    Tüm projede (C# csproj, Tarayıcı Eklentisi manifest'leri, README.md, RELEASE_NOTES.md)
    sürümleri eşzamanlı olarak günceller, eklentiyi paketler ve isteğe bağlı commit/tag/push yapar.
.PARAMETER Version
    Hedef sürüm numarası (örn: 2.0.2). Belirtilmezse -BumpPatch veya -BumpMinor ile hesaplanır.
.PARAMETER BumpPatch
    Patch sürümünü 1 artırır (örn: 2.0.1 -> 2.0.2).
.PARAMETER BumpMinor
    Minor sürümünü 1 artırır (örn: 2.0.1 -> 2.1.0).
.PARAMETER CommitAndTag
    Değişiklikleri git add/commit yapar ve 'vX.Y.Z' etiketini oluşturup remote'a pushlar.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$BumpPatch,
    [switch]$BumpMinor,
    [switch]$CommitAndTag,
    [string]$CommitMessage
)

$ErrorActionPreference = "Stop"
$rootDir = (Resolve-Path "$PSScriptRoot/..").Path

# Mevcut sürümü FDownloader.App.csproj dosyasından oku
$appCsprojPath = Join-Path $rootDir "src/FDownloader.App/FDownloader.App.csproj"
$coreCsprojPath = Join-Path $rootDir "src/FDownloader.Core/FDownloader.Core.csproj"
$readmePath = Join-Path $rootDir "README.md"
$releaseNotesPath = Join-Path $rootDir "RELEASE_NOTES.md"
$manifestChrome = Join-Path $rootDir "tools/browser-extension/manifest.json"
$manifestFirefox = Join-Path $rootDir "tools/browser-extension/manifest.firefox.json"

[xml]$appXml = Get-Content $appCsprojPath
$currentVerStr = $appXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $currentVerStr) {
    Write-Error "FDownloader.App.csproj içinde <Version> bulunamadı!"
}

Write-Host "Mevcut Sistem Sürümü: $currentVerStr" -ForegroundColor Cyan

$targetVer = $Version
if (-not $targetVer) {
    $parts = $currentVerStr.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = if ($parts.Length -gt 2) { [int]$parts[2] } else { 0 }

    if ($BumpMinor) {
        $minor++
        $patch = 0
        $targetVer = "$major.$minor.$patch"
    } elseif ($BumpPatch) {
        $patch++
        $targetVer = "$major.$minor.$patch"
    } else {
        $targetVer = $currentVerStr
    }
}

Write-Host "Hedef Sürüm: $targetVer" -ForegroundColor Green

# 1. C# csproj dosyalarını güncelle
Write-Host "1/5 C# Proje sürümleri güncelleniyor..." -ForegroundColor Yellow
$appContent = Get-Content $appCsprojPath -Raw -Encoding utf8
$appContent = $appContent -replace '<Version>[^<]+</Version>', "<Version>$targetVer</Version>"
$appContent = $appContent -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$targetVer.0</AssemblyVersion>"
$appContent = $appContent -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$targetVer.0</FileVersion>"
Set-Content $appCsprojPath -Value $appContent -Encoding utf8

$coreContent = Get-Content $coreCsprojPath -Raw -Encoding utf8
$coreContent = $coreContent -replace '<Version>[^<]+</Version>', "<Version>$targetVer</Version>"
$coreContent = $coreContent -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$targetVer.0</AssemblyVersion>"
$coreContent = $coreContent -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$targetVer.0</FileVersion>"
Set-Content $coreCsprojPath -Value $coreContent -Encoding utf8

# 2. Browser eklentisi manifest dosyalarını güncelle
Write-Host "2/5 Tarayıcı eklentisi manifest sürümleri güncelleniyor..." -ForegroundColor Yellow
foreach ($mPath in @($manifestChrome, $manifestFirefox)) {
    if (Test-Path $mPath) {
        $mJson = Get-Content $mPath -Raw -Encoding utf8 | ConvertFrom-Json
        $mJson.version = $targetVer
        $formatted = $mJson | ConvertTo-Json -Depth 10
        Set-Content $mPath -Value $formatted -Encoding utf8
    }
}

# 3. README.md dosyasını güncelle
Write-Host "3/5 README.md güncelleniyor..." -ForegroundColor Yellow
if (Test-Path $readmePath) {
    $readmeContent = Get-Content $readmePath -Raw -Encoding utf8
    $readmeContent = $readmeContent -replace '## 🚀 Öne Çıkan Özellikler \(v[^)]+\)', "## 🚀 Öne Çıkan Özellikler (v$targetVer)"
    Set-Content $readmePath -Value $readmeContent -Encoding utf8
}

# 4. RELEASE_NOTES.md başlığını doğrula/güncelle
Write-Host "4/5 RELEASE_NOTES.md kontrol ediliyor..." -ForegroundColor Yellow
if (Test-Path $releaseNotesPath) {
    $rnContent = Get-Content $releaseNotesPath -Raw -Encoding utf8
    $rnContent = $rnContent -replace '### 🚀 FDownloader v[0-9.]+', "### 🚀 FDownloader v$targetVer"
    Set-Content $releaseNotesPath -Value $rnContent -Encoding utf8
} else {
    $defaultRn = "### 🚀 FDownloader v$targetVer Yenilikleri & Düzeltmeleri:`n`n• Kararlılık ve performans iyileştirmeleri sağlandı.`n"
    Set-Content $releaseNotesPath -Value $defaultRn -Encoding utf8
}

# 5. Eklenti paketlerini yeniden derle
Write-Host "5/5 Eklenti paketleniyor ve kopyalanıyor..." -ForegroundColor Yellow
$pkgScript = Join-Path $rootDir "tools/browser-extension/package.ps1"
if (Test-Path $pkgScript) {
    & $pkgScript
}

Write-Host "Tüm sürüm dosyaları v$targetVer için senkronize edildi." -ForegroundColor Green

# İsteğe bağlı Git Commit & Tag işlemi
if ($CommitAndTag) {
    Write-Host "Git işlemleri başlatılıyor..." -ForegroundColor Cyan
    $msg = if ($CommitMessage) { $CommitMessage } else { "chore(release): v$targetVer surumune yukseltildi ve dagitim dosyalari guncellendi" }
    
    git add .
    git commit -m $msg
    
    # Tag varsa sil ve yeniden oluştur
    $tagName = "v$targetVer"
    git tag -d $tagName 2>$null
    git push origin ":refs/tags/$tagName" 2>$null
    git tag -a $tagName -m "Release $tagName"
    
    git push origin main
    git push origin $tagName
    Write-Host "Sürüm $tagName GitHub'a başarıyla gönderildi ve Release CI tetiklendi." -ForegroundColor Green
}
