# FDownloader — Bağımlılık Envanteri

> **Oluşturuldu:** P0-03 görevi kapsamında. Komut: `dotnet list package --outdated` ve `--vulnerable`.
> **Tarih:** 2026-11-03

---

## 1. NuGet Paketleri — FDownloader.Core

| Paket | Mevcut | En Son | Lisans | Güvenlik Açığı |
|---|---|---|---|---|
| Microsoft.Data.Sqlite | 9.0.0 | 10.0.14 | MIT | ✅ Yok |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.0 | 10.0.14 | MIT | ✅ Yok |
| Microsoft.Extensions.Logging.Abstractions | 9.0.0 | 10.0.14 | MIT | ✅ Yok |
| MonoTorrent | 3.0.2 | — | MIT | ✅ Yok |
| SharpCompress | 0.50.7 | 0.50.8 | MIT | ✅ Yok |

---

## 2. NuGet Paketleri — FDownloader.App

| Paket | Mevcut | En Son | Lisans | Güvenlik Açığı |
|---|---|---|---|---|
| Avalonia | 12.1.4 | 12.1.5 | MIT | ✅ Yok |
| Avalonia.Desktop | 12.1.4 | 12.1.5 | MIT | ✅ Yok |
| Avalonia.Fonts.Inter | 12.1.4 | 12.1.5 | MIT | ✅ Yok |
| Avalonia.Themes.Fluent | 12.1.4 | 12.1.5 | MIT | ✅ Yok |
| AvaloniaUI.DiagnosticsSupport | 2.2.3 | — | MIT | ✅ Yok (yalnızca Debug) |
| CommunityToolkit.Mvvm | 8.4.2 | — | MIT | ✅ Yok |
| Microsoft.Extensions.DependencyInjection | 9.0.0 | 10.0.14 | MIT | ✅ Yok |
| Microsoft.Extensions.Logging | 9.0.0 | 10.0.14 | MIT | ✅ Yok |
| Sentry | 6.12.1 | — | MIT | ✅ Yok |
| Serilog | 4.2.0 | 4.4.0 | Apache-2.0 | ✅ Yok |
| Serilog.Extensions.Logging | 8.0.0 | 10.0.0 | Apache-2.0 | ✅ Yok |
| Serilog.Sinks.File | 6.0.0 | 7.0.0 | Apache-2.0 | ✅ Yok |
| Velopack | 1.2.161 | — | MIT | ✅ Yok |

> **Güvenlik açığı tarama sonucu:** `dotnet list package --vulnerable` → Her iki projede de bilinen açık **yoktur**.

---

## 3. Gömülü İkili Dosyalar (tools/)

| Araç | Klasör | Sürüm | Kaynak | Güncelleme Yöntemi | Gitignore? |
|---|---|---|---|---|---|
| 7-Zip | `tools/7z/` | Bilinmiyor (7z.exe dosyasından okunmadı) | 7-zip.org | Manuel | ❌ Hayır (repoda mevcut) |
| yt-dlp | `tools/yt-dlp/` | Bilinmiyor (CI'da runtime'da indiriliyor) | github.com/yt-dlp/yt-dlp | CI: `latest` tag'ından otomatik | ✅ Evet (`.gitignore`'da) |
| ffmpeg | `tools/ffmpeg/` | Bilinmiyor | — | CI: runner'daki kurulu ffmpeg | ✅ Evet (`.gitignore`'da) |
| ffprobe | `tools/ffmpeg/` | ffmpeg ile aynı | — | CI: runner'daki kurulu ffmpeg | ✅ Evet (`.gitignore`'da) |

> ⚠️ **Dikkat:** CI, yt-dlp'yi `latest` etiketiyle indirir — sürüm sabitleme yoktur. Bu, beklenmedik kırılmalara yol açabilir (P5-01'de ele alınacak).
> ⚠️ **7-Zip:** Repoda gömülü olarak bulunuyor; sürüm bilgisi ve lisans bildirimi `docs/` altında eksik (P8-01'de düzenlenecek).

---

## 4. Tarayıcı Eklentisi Bağımlılıkları

Tarayıcı eklentisi (`tools/browser-extension/`) yalnızca yerel JavaScript'tir; harici npm paketi veya CDN bağımlılığı **yoktur**.

| Dosya | Açıklama |
|---|---|
| `manifest.json` | Manifest V3, `host_permissions: ["<all_urls>"]`, `webRequest` izni |
| `background.js` | Service worker |
| `content.js` | Content script |

---

## 5. Güncelleme Önerileri

| Öncelik | Paket / Araç | Not |
|---|---|---|
| 🟡 Orta | Avalonia 12.1.4 → 12.1.5 | Küçük yama; UI davranışı kontrol edilmeli |
| 🟡 Orta | Serilog 4.2.0 → 4.4.0 | Minör; güvenli yükseltme |
| 🟡 Orta | Serilog.Sinks.File 6.0.0 → 7.0.0 | Major yükseltme; API değişikliği olabilir |
| 🟡 Orta | Microsoft.* 9.0.0 → 10.0.14 | .NET 10 ailesi; .NET 9 ile birlikte kullanım uyumluluğu kontrol edilmeli |
| 🟢 Düşük | SharpCompress 0.50.7 → 0.50.8 | Yama; güvenli |
| 🔴 Kritik plan | yt-dlp CI sürüm sabitleme | `latest` yerine belirli sürümü pinin (P5-01) |

