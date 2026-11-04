# Devir Teslim — 2026-11-04

## 1. Şu an üzerinde çalıştığım görev

**ID:** P3-05 — Ağ Güvenliği
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Network/NetworkSecurityPolicy.cs`:
  - `NetworkSecurityPolicy` sınıfı eklendi.
  - Cross-domain yönlendirmelerde (`ShouldPreserveSensitiveHeaders`) ve HTTPS'ten HTTP'ye düşürme (`IsHttpsDowngrade`) senaryolarında çerezlerin ve oturum kimliklerinin yabancı etki alanlarına sızması engellendi.
  - Güvensiz ve yerel kod çalıştırma riski taşıyan URL şemaları (`file://`, `javascript://`, `data://`) `IsSafeUrlScheme` ile engellendi.
- `src/FDownloader.Core/Engine/Probe/DownloadProber.cs`:
  - `DownloadProber` yönlendirme döngüsünde cross-domain veya downgrade tespit edildiğinde `activeCookies` sıfırlanarak hedef sunucuya oturum çerezinin gitmesi önlendi.
- `src/FDownloader.Core/Engine/Worker/SegmentWorker.cs`:
  - İndirme worker'larında referer ile hedef domain uyuşmadığında güvenlik uyarısı verilerek çerez başlığı kaldırıldı.
- `tests/FDownloader.Core.Tests/NetworkSecurityTests.cs`:
  - 16 teori ve birim testi eklendi: host kuralları, HTTPS düşürme tespiti, güvenli şema doğrulama ve mock sunucu ile cross-domain yönlendirmede çerez sızıntısı olmaması test edildi.
- Toplam test sayısı 218'den **234'e** yükseldi. **234/234 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P3-05 tamamlandı.
- **Sıradaki Görev:** **P3-06 — İndirilen Dosya Güvenliği (Zone.Identifier ve tehlikeli dosya uzantıları koruması)**.
  - İndirilen dosyalara NTFS Alternate Data Stream `Zone.Identifier` (ZoneId=3, Web) eklenmesi.
  - `.exe`, `.bat`, `.cmd`, `.scr`, `.js`, `.vbs`, `.ps1`, `.msi` vb. dosyalar için çalıştırma uyarısı.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 234/234 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(security): P3-05 - Ag guvenligi, cross-domain yonlendirme ve cerez sizinti korumasi"
   ```
2. **P3-06:** `ZoneIdentifierHelper` (Zone.Identifier NTFS ADS) ve tehlikeli dosya uzantı listesi doğrulamasını uygula ve test et.

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

