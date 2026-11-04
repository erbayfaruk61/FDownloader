# Devir Teslim — 2026-11-04

## 1. Şu an üzerinde çalıştığım görev

**ID:** P3-07 — Torrent Güvenliği ve VPN Kill-Switch
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Torrent/TorrentSecurityValidator.cs`:
  - `TorrentSecurityValidator` statik güvenlik sınıfı oluşturuldu.
  - Torrent içindeki dosyaların `ValidateTorrentFilePaths` ile hedef indirme dizini sınırları içinde kaldığı garanti altına alındı. `..`, mutlak kök yolları (`C:\...`, `/etc/...`) ve UNC paylaşımları engellenerek path traversal açıklarına set çekildi.
  - `EvaluateNetworkBinding` ile ağ adaptörü bağlama ve VPN Kill-Switch kuralları standardize edildi: Seçili VPN adaptörü kapalı veya erişilemez olduğunda trafik Loopback (127.0.0.1) adresine kilitlenerek gerçek IP sızıntısı önleniyor.
- `src/FDownloader.Core/Torrent/TorrentService.cs`:
  - `NotifyMetadataLoaded` aşamasında gelen torrent dosyaları taranarak güvensiz dosya yolu tespit edilirse görev durdurulup kullanıcıya `TaskFailedEvent` ile güvenlik uyarısı veriliyor.
  - Motor başlatılırken `TorrentSecurityValidator.EvaluateNetworkBinding` entegrasyonu sağlandı.
- `tests/FDownloader.Core.Tests/TorrentSecurityTests.cs`:
  - 11 teori ve birim testi eklendi: güvenli/güvensiz torrent dosya yolları, path traversal engeli, normal ağ bağlama ve VPN koptuğunda loopback kill-switch devreye girişi doğrulandı.
- Toplam test sayısı 249'dan **260'a** yükseldi. **260/260 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P3-07 tamamlandı.
- **Sıradaki Görev:** **P3-08 — Güncelleme Güvenliği**.
  - Velopack güncelleme akışında HTTPS kanalı, SHA-256 paket bütünlüğü ve geri alma (rollback) planının `docs/GUNCELLEME.md` ile doğrulanması/belgelenmesi.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 260/260 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(security): P3-07 - Torrent guvenligi, path traversal engeli ve VPN kill-switch korumasi"
   ```
2. **P3-08:** Güncelleme güvenliği (`docs/GUNCELLEME.md`, Velopack HTTPS/bütünlük kontrolleri) dokümantasyon ve güvenlik denetimini uygula.

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

