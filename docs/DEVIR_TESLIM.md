# Devir Teslim — 2026-11-03

## 1. Şu an üzerinde çalıştığım görev

**ID:** P2-04 — Probe (Ön İnceleme) Sağlamlığı
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Utilities/FileNameSanitizer.cs`:
  - `ExtractFromUrl` metodu uzantılı dosya adı, sorgu parametreleri (`?file=`, `?filename=`, `?name=`) ve uzantısız path segmentlerini öncelik sırasıyla çözümleyecek şekilde geliştirildi.
- `src/FDownloader.Core/Engine/Probe/DownloadProber.cs`:
  - HEAD isteği 405 veya 403 gibi durum kodlarıyla reddedildiğinde Range (0-0 bayt) GET isteği fallback mekanizması sağlandı.
  - Manuel ve otomatik yönlendirme (redirect) zinciri takibi eklendi; `visited` kümesi ile yönlendirme döngüleri (`redirect loop`) tespit edilip engellendi ve üst yönlendirme sınırı (10) tanımlandı.
- `tests/FDownloader.Core.Tests/DownloadProberTests.cs`:
  - TestHttpServer Senaryo 2 (Range desteksiz dosya), Senaryo 3 (Chunked/bilinmeyen boyut), Senaryo 7 (Yönlendirme zinciri ve yönlendirme döngüsü), Senaryo 9 (Content-Disposition: simple, utf-8, zararlı path traversal) ve sorgu parametresi dosya adı çıkarma testleri yazıldı.
- Toplam test sayısı 106'dan **114'e** yükseldi. **114/114 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P2-01, P2-02, P2-03 ve P2-04 tamamlandı.
- **Sıradaki Görev:** **P2-05 — Hata Sınıflandırma ve Yeniden Deneme (Retry Logic)**.
  - Tabloya göre durum kodlarına tepki (Üstel geri çekilme + jitter, 429 Retry-After, 404/410 kalıcı hata, 416 range yeniden değerlendirme).
  - Parça başına deneme sayacı ve toplam süre sınırı.
  - Senaryo 5, 6, 8 test sunucusu uç noktalarıyla doğrulama.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 114/114 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(engine): P2-04 - DownloadProber fallback, yonlendirme dongusu korumasi ve dosya adi onceligi"
   ```
2. **P2-05:** `SegmentWorker.cs` ve `HttpDownloader.cs` içinde hata sınıflandırma ve üstel geri çekilme + jitter mantığını uygula, Senaryo 5, 6, 8 testlerini yaz.

---

## 5. Bilmesi gerekenler

### Mimari
- **Core ↔ App ayrımı tamamen sağlanmış.** P1-01 atlandı.
- `BrowserBridgeService` yerel HTTP sunucu açıyor — port ve kimlik doğrulama bilgisi DENETIM.md'de netleşecek.
- Sürüm numarası tek yerden: `FDownloader.App.csproj → <Version>1.5.6</Version>`.

### Güvenlik (doğrulanmamış — DENETIM.md'de yanıtlanacak)
- `NetworkConstants.cs`'de port sabiti var — güvenlik açıklığı riski bilinmiyor.
- Loglarda hassas veri maskeleme uygulanmamış (Serilog).
- MD5/SHA1 kullanımı için CA5350/CA5351 uyarısı var (`HashCalculator.cs`).

### Bağımlılıklar
- NuGet güvenlik açığı: **sıfır**.
- Güncel olmayan paketler var (Microsoft.*: 9→10, Serilog 4.2→4.4, Sinks.File 6→7, Avalonia 12.1.4→12.1.5).

### CI/CD
- yt-dlp `latest`'ten indiriliyor — sürüm sabitlenmemiş.
- İmzalama yok (P7-01).

---

## 6. Dokunulmaması gerekenler

- `docs/AJAN_PLANI.md` — yalnızca proje sahibi günceller.
- `FDownloader.sln` — proje yapısı değişmedikçe dokunma.
- `tools/7z/` — 7-Zip ikili, sürüm bilinmiyor; P8-01'de lisans kontrolü yapılacak.

