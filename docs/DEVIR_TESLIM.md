# Devir Teslim — 2026-11-03

## 1. Şu an üzerinde çalıştığım görev

**ID:** P2-10 — Hız Hesabı, ETA ve İlerleme Olayları
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Utilities/SpeedCalculator.cs`:
  - `BytesPerSecond`: Okuma anında süresi dolmuş eski örneklerin pencereden temizlenmesi (`PruneExpiredSamples`) sağlandı; böylece veri akışı durduğunda hızın eski değerde takılı kalmayıp gerçekçi şekilde 0'a inmesi garantilendi.
  - `GetEta`: Aşırı büyük veya sonsuz/NaN süreler için 365 gün tavanı ve doğru TimeSpan dönüşümü sağlandı.
- `src/FDownloader.Core/Engine/HttpDownloader.cs`:
  - İlerleme yayınlama döngüsü 400ms aralıkla çalışarak arayüze giden `ProgressUpdatedEvent` bildirimlerini saniyede 2.5 ile sınırladı (UI kilitlenmesini önleme).
- `tests/FDownloader.Core.Tests/SpeedCalculatorTests.cs`:
  - Veri akışı durduğunda hızın sıfırlanması (`SpeedCalculator_WhenStreamStalls_SpeedDropsToZero`), doğru ETA hesabı ve birim biçimlendirme testleri tamamlandı.
- Toplam test sayısı 170'ten **172'ye** yükseldi. **172/172 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P2-01'den P2-10'a kadar olan motor sağlamlaştırma görevleri tamamlandı.
- **Sıradaki Görev:** **P2-11 — Kuyruk, Hız Sınırlayıcı ve Zamanlayıcı**.
  - Eşzamanlı indirme limiti (`MaxConcurrentDownloads`), öncelik sırası ve kuyruk yönetimi.
  - Genel ve görev başı hız sınırlayıcı (`SpeedLimiter` / token bucket).
  - Ağ kesintisinde otomatik duraklatma ve bağlantı geldiğinde otomatik sürdürme.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 172/172 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(engine): P2-10 - Hiz hesabi hareketli ortalama, akis durma sifirlamasi ve ETA dogrulamasi"
   ```
2. **P2-11:** `DownloadManager.cs` ve `SpeedLimiter.cs` içinde kuyruk limitleri, hız sınırlayıcı ve ağ bağlantısı değişim testlerini uygula.

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

