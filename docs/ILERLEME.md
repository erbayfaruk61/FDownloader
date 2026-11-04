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
| P2-10 | Hız/ETA/ilerleme | 🟡 | ✅ Bitti | — | SpeedCalculator hareketli ortalama, akış durduğunda hızın sıfıra düşmesi, doğru ETA hesaplaması ve 400ms (saniyede 2.5 kez) UI event sınırlaması doğrulandı (172/172 test yeşil) |
| P2-11 | Kuyruk/limit/zamanlayıcı | 🟡 | ✅ Bitti | — | SpeedLimiter (dinamik hız sınırlama), DownloadScheduler (belirli saatte başla/dur, bitince kapat/uyku), NetworkWatcher (bağlantı kopunca duraklat, gelince sürdür) ve MaxConcurrentDownloads kuyruk öncelik yönetimi tamamlandı (177/177 test yeşil) |
| P2-12 | Durum makinesi | 🟡 | ✅ Bitti | — | DownloadStateMachine eklendi. Geçersiz durum geçişleri engellendi (Örn: Queued/Paused -> Completed doğrudan geçişi engellendi, 207/207 test yeşil) |
| P2-13 | Tekrar eden URL/silme | 🟢 | ✅ Bitti | — | FindDuplicateTask ve allowDuplicate parametresi eklendi. Diskten silme (.fdown, .part, .tmp) ve veritabanından silme ayrımı doğrulandı (211/211 test yeşil) |

---

## Faz 3 — Güvenlik Sertleştirme

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P3-01 | Eklenti köprüsü güvenliği | 🔴 | ✅ Bitti | — | Origin + Host kontrolü, sınırlı akış ve şema doğrulaması eklendi |
| P3-02 | Arşiv çıkarma güvenliği | 🔴 | ✅ Bitti | — | Zip-Slip (dizin dışına çıkma engeli) ve Zip-Bomb (maks dosya sayısı, maks bayt ve aşırı sıkıştırma oranı denetimi) 7z & SharpCompress için uygulandı (214/214 test yeşil) |
| P3-03 | Dış süreç güvenliği | 🔴 | ✅ Bitti | — | YtDlpMediaService ArgumentList kullanımı ve URL doğrulama ile parametre enjeksiyonu önlendi |
| P3-04 | Hassas veri saklama | 🟡 | ✅ Bitti | — | Windows DPAPI ve PBKDF2/AES-GCM çapraz platform koruması (SecureDataProtector) eklendi. DownloadTasks Cookies ve Settings hassas değerleri (parola/token vb.) veritabanında şifreli saklanıyor (218/218 test yeşil) |
| P3-05 | Ağ güvenliği | 🟡 | ✅ Bitti | — | NetworkSecurityPolicy eklendi. Cross-domain ve HTTPS->HTTP yönlendirmelerde çerez/kimlik sızıntısı engellendi, güvensiz URL şemaları kısıtlandı (234/234 test yeşil) |
| P3-06 | İndirilen dosya güvenliği | 🟡 | ✅ Bitti | — | Windows Zone.Identifier (Mark-of-the-Web: ZoneId=3, Web) NTFS ADS desteği ve tehlikeli çalıştırılabilir dosya uzantıları koruması (DownloadFileSecurity) eklendi (249/249 test yeşil) |
| P3-07 | Torrent güvenliği/kill-switch | 🟡 | ✅ Bitti | — | TorrentSecurityValidator eklendi. Torrent metadata dosyalarında path traversal (.. ve mutlak yol) engeli ve VPN Kill-Switch Loopback koruması doğrulandı (260/260 test yeşil) |
| P3-08 | Güncelleme güvenliği | 🟡 | ✅ Bitti | — | Velopack HTTPS/TLS 1.3 zorunluluğu, SHA-256 paket doğrulaması ve acil durum Roll-Forward / Fail-Safe geri alma mimarisi docs/GUNCELLEME.md ile belgelendi |
| P3-09 | Gizlilik | 🟢 | ✅ Bitti | — | Sentry SetBeforeSend filtresi, kullanıcı adı/IP/email temizliği ve SensitiveDataMasker kullanıcı profil yolları (C:\Users\[USER]) maskelemesi eklendi (261/261 test yeşil) |

---

## Faz 4 — Tarayıcı Eklentisi ve Entegrasyon

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P4-01 | Eklenti denetimi ve temizliği | 🟡 | ✅ Bitti | `4f21d6c` | Manifest V3 izinleri asgariye indirildi (scripting ve polling heartbeat kaldırıldı), docs/EKLENTI.md ve docs/GIZLILIK.md hazırlandı |
| P4-02 | Yakalama doğruluğu | 🔴 | ✅ Bitti | `7b7f560` | Tarayıcı indirmesini iptal edip uygulamaya devretme, Alt tuşu ile atlama, hariç tutulan siteler/minimum boyut kuralları, uygulama kapalıyken normal devam (265/265 test yeşil) |
| P4-03 | Medya algılama | 🟡 | ✅ Bitti | `b2fafc1` | Doğrudan video, HLS/DASH manifest tespiti, DRM koruma tespiti ve engellemesi, KARARLAR.md belgelendi (271/271 test yeşil) |
| P4-04 | Firefox desteği | 🟡 | ✅ Bitti | — | manifest.firefox.json, gecko kimliği, moz-extension origin kabulü ve KURULUM_REHBERI.md Firefox talimatları eklendi |
| P4-05 | Eklenti paketleme otomasyonu | 🟢 | ✅ Bitti | — | tools/browser-extension/package.ps1 eklendi; CI/CD release.yml ve ci.yml içine otomatik Chrome ve Firefox ZIP üretimleri entegre edildi |
| P4-06 | Mağaza yayını hazırlığı | 🧑 | ✅ Bitti | — | docs/MAGAZA_YAYINI.md ile Chrome Web Store, Edge Add-ons ve Firefox AMO için başlık, açıklama, izin gerekçeleri ve görsel listesi hazırlandı |

---

## Faz 5 — Medya, Torrent ve Ek Özellikler

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P5-01 | yt-dlp entegrasyonu sağlamlığı | 🟡 | ✅ Bitti | `e337dd9` | Sürüm güncelleme (yt-dlp -U & GitHub), anlaşılır Türkçe hata çevirisi, yetim süreç temizliği (tree kill + Dispose) ve format/çözünürlük modeli eklendi (301/301 test yeşil) |
| P5-02 | HLS/DASH ve birleştirme | 🟡 | ✅ Bitti | — | HlsStreamDownloader eklendi; paralel parça indirme, master/media playlist ayrıştırma, DRM koruma tespiti, sıralı TS birleştirme ve ffmpeg MP4 konteyner dönüşümü TestHttpServer ile doğrulandı (312/312 test yeşil) |
| P5-03 | Torrent sağlamlığı | 🟡 | ✅ Bitti | — | MonoTorrent entegrasyonu testleri eklendi (dosya önceliği, sıralı streaming indirme, hash kontrolü, tracker yönetimi, restore ve remote .torrent denetimi - 324/324 test yeşil) |
| P5-04 | Site/galeri yakalayıcı | 🟡 | ✅ Bitti | — | SiteGrabberService güçlendirildi; tarama derinliği (MaxDepth), aynı alan adı kısıtı, robots.txt ayrıştırma/nezaketi, hız sınırı gecikmesi, ReDoS koruması, iptal desteği ve HEAD boyut sorgulama eklendi (335/335 test yeşil) |
| P5-05 | Proxy ve kimlik doğrulama | 🟡 | ✅ Bitti | — | HTTP/HTTPS/SOCKS5 proxy, sistem proxy, site giriş bilgileri yöneticisi (DPAPI şifreli, CredentialCache, Basic/Digest/NTLM desteği) (344/344 test yeşil) |
| P5-06 | Toplu indirme | 🟢 | ✅ Bitti | — | Link listesi yapıştırma, sayı/harf aralığı deseni ([01-20], [a-z]), dosya adı şablonları ({index}, {name}), kuyruk yönetimi (351/351 test yeşil) |

---

## Faz 6 — Kullanıcı Deneyimi

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P6-01 | İlk açılış sihirbazı | 🟡 | ✅ Bitti | — | 3 adımlı karşılama sihirbazı (hoş geldiniz & tema seçimi, kayıt klasörü & Windows başlangıç entegrasyonu, tarayıcı eklentisi 3 adımlı rehberi) tamamlandı |
| P6-02 | Çoklu dil altyapısı | 🟡 | ✅ Bitti | — | Türkçe ve İngilizce dil tabloları, sistem dilini otomatik algılama, reaktif INotifyPropertyChanged indeksleyici ile dinamik dil değiştirme (355/355 test yeşil) |
| P6-03 | Hata mesajları | 🟡 | ✅ Bitti | — | Anlaşılır hata açıklamaları (ne oldu/neden/ne yapılabilir), docs/HATA_KODLARI.md (361/361 test yeşil) |
| P6-04 | Arayüz iyileştirmeleri | 🟡 | ✅ Bitti | — | VirtualizingStackPanel sanallaştırma, global klavye kısayolları (Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+P, F1, Space, Del, Shift+Del), sürükle-bırak desteği ve sistem tepsisi menüsü (361/361 test yeşil) |
| P6-05 | Erişilebilirlik ve ekran uyumu | 🟡 | ✅ Bitti | — | AutomationProperties etiketleri (ekran okuyucu), klavyeyle tam kullanım (Tab, Space, Enter), HighContrast yüksek kontrast renk teması (361/361 test yeşil) |
| P6-06 | Ayarlar düzeni | 🟢 | ✅ Bitti | — | Ayarlar sekmeleri, ayar açıklamaları, varsayılana sıfırla, JSON dışa/içe aktarma (365/365 test yeşil) |
| P6-07 | Güç ve sistem davranışları | 🟡 | ✅ Bitti | — | PowerManagementService SetThreadExecutionState uyku engelleme, aktif indirmede çıkış onay diyaloğu (365/365 test yeşil) |

---

## Faz 7 — Dağıtım ve Güven

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P7-01 | Kod imzalama stratejisi | 🧑 | ✅ Bitti | — | docs/KOD_IMZALAMA.md rehberi (Azure Trusted Signing, SignPath.io, Velopack vpk entegrasyonu) |
| P7-02 | CI/CD sürüm hattı | 🟡 | ✅ Bitti | — | .github/workflows/release.yml (etiket atılınca build, vpk pack, browser-extension, SHA256SUMS ve GitHub Release) |
| P7-03 | Güven artırıcılar | 🟢 | ✅ Bitti | — | SHA256SUMS.txt otomatik üretimi ve docs/YANLIS_POZITIF.md itiraz kılavuzu |
| P7-04 | Paket yöneticileri | 🟡 | ✅ Bitti | — | packaging/winget/ (FarukErbay.FDownloader manifestleri tr-TR, en-US, installer, version) |
| P7-05 | Güncelleme deneyimi | 🟡 | ✅ Bitti | — | Velopack UpdateService, Ayarlar/Hakkında sekmesinde güncelleme denetimi & kurulumu |
| P7-06 | Çapraz platform mimarisi | 🟡 | ✅ Bitti | — | Windows'a özel bileşenlerin soyutlama ve OSPlatform koruması altına alınması |

---

## Faz 8 — Hukuk, Belgeler, Topluluk

| ID | Görev | Zorluk | Durum | Commit | Not |
|---|---|---|---|---|---|
| P8-01 | Lisans ve üçüncü taraf bildirimleri | 🟡 | ✅ Bitti | — | THIRD_PARTY_NOTICES.md (Avalonia, MonoTorrent, yt-dlp, 7-Zip, SharpCompress vb.) ve Ayarlar > Hakkında sekmesinde lisans kartı |
| P8-02 | Sorumluluk reddi ve içerik politikası | 🟡 | ✅ Bitti | — | Telif hakları, kullanım koşulları sorumluluk reddi ve DRM aşmama beyanı (README.md, README.en.md) |
| P8-03 | Gizlilik politikası ve kullanım koşulları | 🟢 | ✅ Bitti | — | docs/GIZLILIK.md (Sıfır izleme, sıfır telemetri ve yerel depolama ilkeleri) |
| P8-04 | README ve dokümantasyon | 🟡 | ✅ Bitti | — | Türkçe (README.md) ve İngilizce (README.en.md), CONTRIBUTING.md, SECURITY.md, CODE_OF_CONDUCT.md |
| P8-05 | Topluluk altyapısı | 🟢 | ✅ Bitti | — | Issue şablonları (.github/ISSUE_TEMPLATE/), PR şablonu (PULL_REQUEST_TEMPLATE.md), CHANGELOG.md (SemVer) |

---

## Faz 9 — Kararlılık ve Sürüm 2.0

| ID | Görev | Zorluk | Durum |
|---|---|---|---|
| P9-01 → P9-05 | (Tüm görevler) | — | ⬜ Bekliyor |

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

