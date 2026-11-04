# Devir Teslim — 2026-11-04

## 1. Şu an üzerinde çalıştığım görev

**ID:** P3-06 — İndirilen Dosya Güvenliği
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Security/DownloadFileSecurity.cs`:
  - `DownloadFileSecurity` sınıfı oluşturuldu.
  - Windows NTFS Zone.Identifier (Mark-of-the-Web: ZoneId=3, Web) Alternate Data Stream (ADS) yazma ve kontrol mantığı (`ApplyZoneIdentifier`, `HasZoneIdentifier`) uygulandı. ReferrerUrl ve HostUrl meta verileri de ADS içine yazılıyor.
  - Tehlikeli dosya türleri tespiti (`IsDangerousFileType`): `.exe`, `.bat`, `.cmd`, `.scr`, `.js`, `.vbs`, `.ps1`, `.msi` vb. sistemde zararlı kod yürütebilecek 26 uzantı denetim altına alındı.
- `src/FDownloader.Core/Engine/HttpDownloader.cs`:
  - İndirme başarıyla tamamlanıp birleştirildiğinde (CommitAsync sonrası), otomatik olarak `ApplyZoneIdentifier` çağrılması sağlandı.
- `tests/FDownloader.Core.Tests/DownloadFileSecurityTests.cs`:
  - 15 teori ve birim testi eklendi: tehlikeli ve güvenli uzantı ayrımı, NTFS ADS Zone.Identifier yazımı ve doğrulaması test edildi.
- Toplam test sayısı 234'ten **249'a** yükseldi. **249/249 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P3-06 tamamlandı.
- **Sıradaki Görev:** **P3-07 — Torrent Güvenliği ve Kill-Switch**.
  - Torrent dosya adları/yollarında path traversal (`..` ve mutlak yol) engeli.
  - Varsayılan klasör dışına yazmama kontrolü.
  - Ağ adaptörü kilidi (VPN kill-switch) ve ağ arayüzü bağlama test senaryoları.
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 249/249 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(security): P3-06 - Indirilen dosya guvenligi, Zone.Identifier Mark-of-the-Web ve tehlikeli uzanti korumasi"
   ```
2. **P3-07:** `TorrentService` içinde path traversal denetimleri, VPN ağ arayüzü bağlama (kill-switch) mantığını ve testlerini uygula.

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

