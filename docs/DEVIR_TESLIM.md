# Devir Teslim — 2026-11-03

## 1. Şu an üzerinde çalıştığım görev

**ID:** P2-09 — Dosya Adı Güvenliği ve Çakışma Politikası
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Utilities/FileNameSanitizer.cs`:
  - `Sanitize`: Unicode Form C normalleştirmesi, null byte ve kontrol karakteri temizliği, ters eğik çizgi normalleştirmesi eklendi.
  - Windows ayrılmış/rezerve cihaz adları (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`) hem yalın hem de uzantılı varyasyonlarında (`_CON`, `_nul.tar.gz`) güvenli ön eke bağlandı.
  - `IsSafePath`: Hedef klasör dışına çıkışı (`Path Traversal` - `../`, `..\`) kesin olarak engelleyen doğrulama metodu eklendi.
  - `FileConflictPolicy` (RenameAuto, Overwrite, Skip) enum ve çakışma çözümleme politikası uygulandı.
- `tests/FDownloader.Core.Tests/FileNameSafetyTests.cs`:
  - 33'ü aşkın kötü niyetli girdi varyasyonunu içeren (path traversal, rezerve adlar, yasak karakterler, sondaki nokta/boşluk, kontrol karakterleri, aşırı uzun isimler) kapsamlı güvenlik test seti yazıldı.
  - Çakışma politikalarının (otomatik numaralandırma `(1)`, `(2)`, üzerine yazma ve atlama) testleri tamamlandı.
- Toplam test sayısı 133'ten **170'e** yükseldi. **170/170 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P2-01'den P2-09'a kadar olan motor sağlamlaştırma görevleri tamamlandı.
- **Sıradaki Görev:** **P2-10 — Hız Hesabı, ETA ve İlerleme Olayları**.
  - Hareketli ortalama (3-5 sn) hız hesabı, kalan süre (ETA) tahmini.
  - Arayüze giden ilerleme güncellemelerinin saniyede 2-5 aralığına sınırlandırılması (throttling) ve UI akıcılığı.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 170/170 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(engine): P2-09 - Dosya adi guvenligi, path traversal engeli ve 37 testlik guvenlik seti"
   ```
2. **P2-10:** `SpeedCalculator.cs` ve `HttpDownloader.cs` ilerleme olaylarının hız/ETA doğruluğunu ve saniyede 2-5 UI sınırlandırmasını testleriyle incele ve uygula.

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

