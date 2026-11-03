# FDownloader — İlerleme Takibi

> **Son güncelleme:** 2026-11-03 (P0-04 görevi)
> Durumlar: `⬜ Bekliyor` · `🔄 Devam` · `✅ Bitti` · `⏸ Engelli (neden)` · `⏭ Atlandı (neden)`

---

## Faz 0 — Denetim ve Başlangıç Durumu

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P0-01 | Repo haritası (MIMARI.md) | 🟡 | ✅ Bitti | — | `docs/MIMARI.md` oluşturuldu |
| P0-02 | Derleme doğrulaması | 🟢 | ✅ Bitti | — | Derleme: ✅ başarılı (app çalışırken dosya kilidi nedeniyle değil); Test: 82/82 ✅ |
| P0-03 | Bağımlılık envanteri | 🟡 | ✅ Bitti | — | `docs/BAGIMLILIKLAR.md` oluşturuldu; güvenlik açığı yok |
| P0-04 | Güvenlik/güvenilirlik denetimi | 🔴 | ✅ Bitti | — | `docs/DENETIM.md` tamamlandı (15 soru, 2 Kritik, 6 Yüksek bulgu) |
| P0-05 | Git hijyeni | 🟢 | ✅ Bitti | — | .gitignore yeterli; kirli dosya yok; çalışma değişiklikleri bekliyor (commit edilecek) |
| P0-06 | İlerleme/devir dosyaları | 🟢 | ✅ Bitti | — | Bu dosya + DEVIR_TESLIM.md oluşturuldu |

---

## Faz 1 — Temeller

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P1-00 | (Ad değişirse) Yeniden adlandırma | 🔴 | ⏸ Engelli | — | Şimdilik ertelendi (kullanıcı talimatı) |
| P1-01 | Çekirdeği arayüzden ayır | 🟡 | ⏭ Atlandı | — | Core zaten ayrı proje; MIMARI.md'ye not düşüldü |
| P1-02 | Test projeleri | 🟡 | ✅ Bitti | — | .github/workflows/ci.yml iş akışı eklendi (Release modunda derleme ve test çalıştırma) |
| P1-03 | Yerel test sunucusu | 🔴 | ✅ Bitti | — | TestHttpServer oluşturuldu; 13 senaryonun tümü (Range, Chunked, ETag, Auth vb.) test edildi (95/95 test yeşil) |
| P1-04 | Statik kod kalitesi | 🟢 | ✅ Bitti | — | .editorconfig eklendi, güvenlik kuralları (CA2100 vb.) hata seviyesine çıkarıldı, CI'ya kod biçim denetimi entegre edildi |
| P1-05 | Loglama altyapısı | 🟢 | ✅ Bitti | — | SensitiveDataMasker eklendi (Bearer/Basic Auth, Cookie, URL token/API key maskeleme) ve test edildi (98/98 test yeşil) |

---

## Faz 2 — İndirme Motorunu Sağlamlaştırma

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P2-01 | Hash testleri | 🔴 | ✅ Bitti | — | 1, 2, 4, 8, 16 parça kombinasyonları ve tam bölünmeyen boyutlarda SHA-256 hash doğrulaması yapıldı (104/104 test yeşil) |
| P2-02 | Kayıpsız resume | 🔴 | ✅ Bitti | — | Pause/Cancel anında disk flush ve checkpoint kaydı, GetTask ile segment yükleme, Pause/Resume sonrası SHA-256 hash doğrulaması test edildi (105/105 test yeşil) |
| P2-03 | Dosya değişim kontrolü | 🟡 | ✅ Bitti | — | Resume öncesi ETag, Last-Modified ve boyut doğrulama, FileChangedOnServerEvent yayını ve iptal koruması eklendi (106/106 test yeşil) |
| P2-04 | Probe sağlamlığı | 🟡 | ✅ Bitti | — | HEAD/Range fallback, yönlendirme döngüsü/sınırı kontrolü, dosya adı önceliği (Content-Disposition > Path > Query) Senaryo 2, 3, 7, 9 ile doğrulandı (114/114 test yeşil) |
| P2-05 | Hata sınıflandırma/yeniden deneme | 🔴 | ✅ Bitti | — | DownloadErrorClassifier, SegmentWorker akıllı yeniden deneme (429 Retry-After, kalıcı 404/410 ve 401/403 erken durdurma, disk full koruması) ve HttpDownloader entegrasyonu tamamlandı (122/122 test yeşil) |
| P2-06 | Stall tespiti | 🔴 | ✅ Bitti | — | Connect timeout ve Stall timeout (veri akışı durdu) ayrımı, kopan/takılan parçanın Range ile kayıpsız toparlanması Senaryo 4 ve 5 ile kanıtlandı (125/125 test yeşil) |
| P2-07 | Dinamik parça bölme | 🔴 | ✅ Bitti | — | Boştaki worker'ların iş çalması (work-stealing), dinamik parça bölme, çift yazım/çakışma koruması ve hash bütünlüğü test edildi (128/128 test yeşil) |
| P2-08 | Disk/dosya sistemi | 🟡 | ✅ Bitti | — | Preallocation, FAT32 4 GB sınırı engeli, hedef klasör silinmesi koruması, atomik taşıma ve TEST_SENARYOLARI.md hazırlandı (133/133 test yeşil) |
| P2-09 | Dosya adı güvenliği | 🟡 | ✅ Bitti | — | Windows rezerve adları (_CON/_NUL), Unicode normalleştirme, IsSafePath ile path traversal koruması, 37 testlik kötü niyetli liste ve çakışma politikaları doğrulandı (170/170 test yeşil) |
| P2-10 | Hız/ETA/ilerleme | 🟡 | ⬜ Bekliyor | — | SpeedCalculator mevcut |
| P2-11 | Kuyruk/limit/zamanlayıcı | 🟡 | ⬜ Bekliyor | — | |
| P2-12 | Durum makinesi | 🟡 | ⬜ Bekliyor | — | |
| P2-13 | Tekrar eden URL/silme | 🟢 | ⬜ Bekliyor | — | |

---

## Faz 3 — Güvenlik Sertleştirme

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P3-01 | Eklenti köprüsü güvenliği | 🔴 | ✅ Bitti | — | Origin + Host kontrolü, sınırlı akış ve şema doğrulaması eklendi |
| P3-02 | Arşiv çıkarma güvenliği | 🔴 | ⬜ Bekliyor | — | |
| P3-03 | Dış süreç güvenliği | 🔴 | ✅ Bitti | — | YtDlpMediaService ArgumentList kullanımı ve URL doğrulama ile parametre enjeksiyonu önlendi |
| P3-04 | Hassas veri saklama | 🟡 | ⬜ Bekliyor | — | |
| P3-05 | Ağ güvenliği | 🟡 | ⬜ Bekliyor | — | |
| P3-06 | İndirilen dosya güvenliği | 🟡 | ⬜ Bekliyor | — | Zone.Identifier eksik |
| P3-07 | Torrent güvenliği/kill-switch | 🟡 | ⬜ Bekliyor | — | |
| P3-08 | Güncelleme güvenliği | 🟡 | ⬜ Bekliyor | — | |
| P3-09 | Gizlilik | 🟢 | ⬜ Bekliyor | — | Sentry entegre; kişisel veri filtresi kontrol gerekli |

---

## Faz 4–9

| ID | Görev | Zorluk | Durum |
|---|---|---|---|
| P4-01 → P9-05 | (Tüm görevler) | — | ⬜ Bekliyor |

---

## Önerilen Sıra

`P0-04 (DENETIM.md) → P1-02 → P1-03 → P2-01 → P2-02 → P3-01 → P3-02 → P3-03 → geri kalan P2 → P3 geri kalan → P4 → P5 → P6 → P7 → P8 → P9`

**Kural:** P2-01 ve P2-02 bitmeden yeni özellik eklenmez.

---

## Derleme ve Test Durumu

| Alan | Durum |
|---|---|
| `dotnet build` | ✅ Başarılı (0 hata; uygulama kapalıyken) |
| Derleme uyarısı sayısı | ~25 (CA1848 logger, CA1305 kültür, CA5350/CA5351 MD5/SHA1) |
| `dotnet test` | ✅ 82/82 başarılı |
| Bekleyen değişiklikler (commit edilmemiş) | `MainViewModel.cs`, `MainWindow.axaml`, `ClipboardWatcher.cs`, `background.js` |

