# Devir Teslim — 2026-11-04

## 1. Şu an üzerinde çalıştığım görev

**ID:** P2-11 — Kuyruk, Hız Sınırlayıcı ve Zamanlayıcı
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Engine/SpeedLimiter.cs`:
  - Token-bucket algoritması ile çalışan hız sınırlayıcı eklendi. İndirme sırasında dinamik limit güncelleme (LimitBytesPerSecond) desteklendi.
- `src/FDownloader.Core/Manager/DownloadScheduler.cs`:
  - Belirli saatte otomatik başlatma/durdurma ve tüm indirmeler tamamlandığında bilgisayarı kapatma/uykuya alma mantığı uygulandı.
- `src/FDownloader.Core/Manager/NetworkWatcher.cs`:
  - Ağ bağlantı durumu izleyicisi entegre edildi. İnternet koptuğunda aktif indirmeler güvenli şekilde duraklatılıyor, bağlantı sağlandığında otomatik sürdürülüyor.
- `src/FDownloader.Core/Manager/DownloadManager.cs`:
  - `MaxConcurrentDownloads` eşzamanlı indirme sınırı, `ProcessQueueAsync` kuyruk öncelik sırası (`Priority` ve `CreatedAt`), görev önceliğini değiştirme ve sürükle-bırak kuyruk sırası yönetimi entegre edildi.
  - `DisposeAsync` içinde koleksiyon değişikliği hataları engellendi ve güvenli temizlik sağlandı.
- `tests/FDownloader.Core.Tests/QueueAndSchedulerTests.cs`:
  - 5 yeni birim testi yazıldı: dinamik hız sınırlama, eşzamanlı indirme kuyruk sınırı, öncelik sıralaması, zamanlayıcı tetikleyicisi ve ağ izleme olayları test edildi.
- Toplam test sayısı 172'den **177'ye** yükseldi. **177/177 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P2-11 başarıyla tamamlandı ve test edildi.
- **Sıradaki Görev:** **P2-12 — Görev Durum Makinesi**.
  - `docs/MIMARI.md`'deki durum geçiş tablosuna göre geçersiz durum geçişlerinin engellenmesi (`DownloadStatus` state transitions: Queued, Connecting, Downloading, Paused, Merging, Completed, Failed, Cancelled).
  - Geçersiz geçişlerde exception fırlatılması veya koruma mekanizması ve testleri.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 177/177 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(engine): P2-11 - Kuyruk yonetimi, oncelik sirasi, hiz sinirlayici, zamanlayici ve ag izleyici"
   ```
2. **P2-12:** `DownloadStateMachine.cs` (veya `DownloadTask` durum doğrulama kuralları) durum geçiş mantığını ve birim testlerini uygula.

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

