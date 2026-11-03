# Devir Teslim — 2026-11-03

## 1. Şu an üzerinde çalıştığım görev

**ID:** P2-03 — Dosya Değişim Kontrolü (ETag / Last-Modified / Boyut Doğrulama)
**Durum:** ✅ Bitti

---

## 2. Yaptıklarım (bu oturumda)

- `src/FDownloader.Core/Events/DownloadEvents.cs`:
  - `FileChangedOnServerEvent(TaskId, OldEtag, NewEtag, OldModified, NewModified)` olayı eklendi.
- `src/FDownloader.Core/Engine/HttpDownloader.cs`:
  - Resume öncesinde (kısmen indirilmiş segmentler varsa) `DownloadProber` ile sunucu durumu sorgulanıyor.
  - Eğer sunucudaki `ETag` veya `Last-Modified` ya da `Content-Length` değişmişse, sessizce bozuk dosya indirmek yerine indirme derhal durduruluyor, `FileChangedOnServerEvent` fırlatılıyor ve görev `Failed` ("Dosya sunucuda değişmiş") olarak işaretleniyor.
- `tests/FDownloader.Core.Tests/FileChangeDetectionTests.cs`:
  - `TestHttpServer` Senaryo 12 (`/dynamic-file`) üzerinde ETag sürüm 1'den sürüm 2'ye geçtiğinde resume denemesinin başarıyla engellendiği ve olayın yayınlandığı test edildi.
- Toplam test sayısı 105'ten **106'ya** yükseldi. **106/106 test yeşil**.

---

## 3. Yarıda kalan iş

- **Tam olarak nerede kaldım:** P2-01, P2-02 ve P2-03 tamamlandı.
- **Sıradaki Görev:** **P2-04 — Probe (Ön İnceleme) Sağlamlığı**.
  - HEAD isteği 405/403 ile reddedilirse yedek olarak küçük Range (0-0 bayt) GET isteği gönderme.
  - Yönlendirme zinciri takibi, döngü tespiti ve üst limit.
  - Dosya adı öncelik hiyerarşisi (`Content-Disposition` -> final URL -> sorgu -> mime).
- **Derleme durumu:** ✅ Başarılı (0 hata)
- **Test durumu:** 106/106 geçiyor

---

## 4. Sıradaki adım (net ve küçük)

1. Değişiklikleri commit et:
   ```
   git add src/ tests/ docs/
   git commit -m "feat(engine): P2-03 - Resume oncesi ETag/Last-Modified dosya degisim tespiti ve korumasi"
   ```
2. **P2-04:** `DownloadProber.cs` içinde HEAD fallback (Range 0-0 GET) ve yönlendirme/dosya adı öncelik testlerini yaz ve uygula.

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

