# Değişiklik Günlüğü (Changelog)

Bu projedeki tüm önemli değişiklikler bu dosyada belgelenmektedir.  
Format, [Keep a Changelog](https://keepachangelog.com/tr/1.0.0/) standardına dayanmakta olup, bu proje [Semantic Versioning (SemVer 2.0)](https://semver.org/lang/tr/) sürümleme kurallarına uymaktadır.

---

## [1.5.7] - 2026-11-06

### İyileştirildi & Düzeltildi
- **Torrent Seçimli İndirme:** Parça ve dosya seçiminde yalnızca seçilen dosyaların net boyutu ve doğru tahmini kalan süresi hesaplanacak şekilde güncellendi.
- **Tarayıcı Entegrasyonu:** Alt tuşu basılı tutulduğunda indirmeyi tarayıcıya bırakma (baypas) mekanizması hem eklenti hem de masaüstü tarafında kusursuzlaştırıldı.
- **Çoklu Dil (i18n):** İngilizce dil seçildiğinde durum çubuğu, hız sınırları, eklenti bekleme rozeti, tablo başlıkları ve alt bilgi kartlarındaki metinlerin anlık güncellenmesi tamamlandı.
- **Arayüz & Yerleşim:** Durum çubuğundaki taşma sorunları giderildi, görsel hizalamalar optimize edildi.
- **Eklenti Sürümü:** Tarayıcı eklentisi sürümü masaüstü uygulaması ile eşitlenerek `v1.5.7` yapıldı.

---

## [1.5.0] - 2026-11-04

### Eklendi
- **Çok Parçalı İndirme Motoru:** HTTP/HTTPS bağlantılarını dinamik parçalara (segment) ayırarak paralel indirme altyapısı.
- **BitTorrent İstemcisi:** Magnet linkleri ve `.torrent` dosyaları için parça seçimi, eş takibi ve sıralı medya indirme (Streaming modu).
- **Akıllı Medya Yakalayıcı:** Web sitelerindeki video ve ses akışlarını yt-dlp ile yakalama desteği.
- **Otomatik Arşiv Açma:** İndirilen ZIP, RAR, 7Z ve TAR arşivlerini otomatik klasöre çıkarma ve şifreli arşivler için parola sorma desteği.
- **Ağ Adaptörü Kilidi (VPN Kill-Switch):** İndirmeleri yalnızca seçili ağ kartına bağlama ve bağlantı koptuğunda anında koruma sağlama.
- **Site & Galeri Yakalayıcı:** Bir web sayfasındaki tüm medya ve doküman bağlantılarını toplu tarama.
- **Toplu İndirme Desteği:** Sayısal ve harf aralıklı URL desenlerini (`[01-20]`, `[a-z]`) ayrıştırma ve kuyruğa alma.
- **Kullanıcı Deneyimi & Tasarım:**
  - 3 adımlı İlk Açılış Sihirbazı (`FirstRunWizard`).
  - Dinamik i18n çoklu dil desteği (Türkçe ve İngilizce).
  - 5 zengin renk teması (Okyanus Mavisi, Siber Mor, Platin Gri, Aydınlık, Yüksek Kontrast).
  - Ekran okuyucu (AutomationProperties) ve tam klavye navigasyonu (Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+P, Space, F1).
  - Ayarları varsayılana sıfırlama, JSON dışa ve içe aktarma araçları.
  - Windows uyku engelleme (`SetThreadExecutionState`) ve çıkışta aktif indirme koruma uyarısı.
- **Güvenlik:**
  - Proxy şifreleri ve site kimlik bilgileri için Windows DPAPI koruması (`SiteCredentialManager`).
  - Tehlikeli dosya uzantıları (`.exe`, `.bat`, `.cmd` vb.) ve Windows Zone.Identifier (Mark-of-the-Web) güvenlik etiketleri.
- **Dağıtım:**
  - Velopack otomatik güncelleme ve kurulum altyapısı.
  - Windows Package Manager (winget) resmi manifestleri.
  - Sürümlerde otomatik SHA-256 doğrulama hash listesi (`SHA256SUMS.txt`).

---

## [1.0.0] - 2026-10-12

### Eklendi
- FDownloader ilk kararlı masaüstü sürümü (.NET 9 & Avalonia UI).
- Temel tek parçalı HTTP indirme ve yerel SQLite görev veritabanı.
