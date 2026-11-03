# Devir Teslim — 2026-11-03

## 1. Şu an üzerinde çalıştığım görev

**ID:** P2-05 — Hata Sınıflandırma ve Yeniden Deneme (Retry Logic)
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Engine/Exceptions/DownloadErrorClassifier.cs`:
  - `DownloadErrorKind` (Transient, TooManyRequests, PermanentNotFound, PermanentAuth, DiskFullOrPermission, RangeNotSupported) ve `DownloadException` tanımlandı.
  - HTTP durum kodları (404, 410, 401, 403, 429, 500, 503) ve sistem istisnaları (SocketException, timeout, abort, disk full HRESULT 0x80070070) sınıflandırıldı.
- `src/FDownloader.Core/Engine/Worker/SegmentWorker.cs`:
  - Kalıcı hatalarda (404, 410, 401, 403) boşuna tekrar deneme yapılmadan anında hata fırlatma eklendi.
  - HTTP 429 yanıtında `Retry-After` başlığına uyumlu bekleme sağlandı.
  - Disk veya izin hatalarında görev duraklatılarak diske/kullanıcıya müdahale imkanı verildi.
- `src/FDownloader.Core/Engine/HttpDownloader.cs`:
  - Worker hataları yakalanıp `TaskFailedEvent` ile kurtarılabilir (`isRetrying`) veya kalıcı olarak UI'ya anlaşılır Türkçe mesajlarla iletildi.
- `tests/FDownloader.Core.Tests/ErrorClassificationTests.cs`:
  - Senaryo 5 (Abort ve geçici hata yeniden deneme), Senaryo 8 (429 Retry-After, 404 kalıcı hata, 403 erişim engeli, 500/503 geçici hata) ve disk dolu simülasyon testleri yazıldı.
- Toplam test sayısı 114'ten **122'ye** yükseldi. **122/122 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P2-01, P2-02, P2-03, P2-04 ve P2-05 tamamlandı.
- **Sıradaki Görev:** **P2-06 — Takılma (Stall) Tespiti ve Bağlantı Yönetimi**.
  - Bağlantı zaman aşımı, okuma zaman aşımı ve "veri akışı durdu" zaman aşımı ayrımı.
  - Takılan parçayı kesme ve yeniden bağlanma (Senaryo 4 yavaş yanıt ve Senaryo 5).
  - Sunucu çok fazla bağlantıyı reddederse parça sayısını otomatik düşüren uyarlamalı mantık.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 122/122 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(engine): P2-05 - Hata siniflandirma, 429 Retry-After ve kalici hata erken durdurma mekanizmasi"
   ```
2. **P2-06:** `SegmentWorker.cs` ve `HttpDownloader.cs` içinde stall tespiti ve uyarlamalı bağlantı/parça düşürme mekanizmasını testleriyle uygula.

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

