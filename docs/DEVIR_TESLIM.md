# Devir Teslim — 2026-11-04

## 1. Şu an üzerinde çalıştığım görev

**ID:** P2-13 — Tekrar Eden URL ve Silme Politikası
**Durum:** ✅ Bitti (Faz 2 Tamamlandı!)

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Manager/DownloadManager.cs`:
  - `FindDuplicateTask(url)` metodu eklendi. Orijinal veya nihai URL üzerinden eşleşen mevcut görevlerin tespiti sağlandı.
  - `AddHttpDownloadAsync` metoduna `allowDuplicate` (varsayılan true) parametresi eklendi. Tekrar eklenmek istendiğinde ve izin verilmediğinde var olan görev döndürülerek çift indirme engellendi.
  - `RemoveTaskAsync(taskId, deleteFile)` mantığı doğrulandı: Görev veritabanından ve aktif kuyruktan silinirken `deleteFile: false` durumunda indirilen dosyalar korunuyor, `deleteFile: true` durumunda hedef dosya ve tüm geçici dosyalar (`.fdown`, `.part`, `.tmp`) güvenli şekilde temizleniyor.
- `tests/FDownloader.Core.Tests/DuplicateAndDeletionTests.cs`:
  - 4 yeni birim testi yazıldı: duplicate engelleme, duplicate izin verme, disk dosyalarını koruyarak silme ve disk dosyalarını tamamen temizleyerek silme senaryoları doğrulandı.
- Toplam test sayısı 207'den **211'e** yükseldi. **211/211 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** **FAZ 2 (İndirme Motorunu Sağlamlaştırma) EKSİKSİZ TAMAMLANDI!** (P2-01'den P2-13'e kadar tüm görevler bitti).
- **Sıradaki Görev:** **Faz 3 — Güvenlik Sertleştirme**.
  - P3-01 (Eklenti köprüsü güvenliği) ve P3-03 (Dış süreç güvenliği) önceden tamamlanmıştı.
  - Sıradaki güvenlik görevi: **P3-02 — Arşiv Çıkarma Güvenliği (Zip Slip / Zip Bomb koruması)** (`ArchiveExtractorService.cs`).
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 211/211 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(engine): P2-13 - Tekrar eden URL kontrolu ve diskten silme politikalari (Faz 2 tamamlandi)"
   ```
2. **P3-02:** `ArchiveExtractorService.cs` Zip Slip path traversal ve Zip Bomb (sıkıştırma oranı / aşırı açılma boyutu) güvenlik kontrolleri ve testlerini uygula.

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

