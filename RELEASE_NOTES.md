### 🚀 FDownloader v2.0.12 Yenilikleri & Düzeltmeleri:

• ⚡ **Yeni Özellik: Turbo MP3 Modu:**
  - Ana araç çubuğuna Pano İzleyicisi'nin hemen yanına **⚡ Turbo MP3** modu seçeneği eklendi.
  - Açık olduğunda panodan veya tarayıcıdan yakalanan tüm medya/video bağlantıları doğrudan MP3 formatına önceden ayarlanır. Seçim kalıcı olarak kaydedilir.

• 🛡️ **Medya İsimlendirme ve "Zaten Var" Çakışma Koruması:**
  - YouTube ve medya bağlantılarında meta veriler (video başlığı, formatlar) arka planda çekilip listelenmeden kullanıcının erkenden "İndirmeyi Başlat" butonuna tıklaması engellendi (buton meta veriler gelene kadar devre dışı bırakılır).
  - Medya başlığı çekilmeden önce "Medya.mp3" gibi genel isimle indirme başlatılması ve ardından gelen indirmelerde "Hedef klasörde aynı isimde dosya zaten var" çakışması yaşanması sorunu tamamen ortadan kaldırıldı.

### 🚀 FDownloader v2.0.11 Yenilikleri & Düzeltmeleri:

• 🛠️ **Medya Ayrıştırma ve İndirme Hatası Düzeltildi:**
  - YouTube ve çeşitli video servislerinde `yt-dlp` tarafından döndürülen ondalıklı FPS (örn. 0.63 gibi storyboard/özel akış kare hızları) nedeniyle oluşan `FormatException: One of the identified items was in an invalid format.` hatası giderildi.
  - Video çözünürlük, süre, kare hızı ve dosya boyutu alanları esnek ve toleranslı tip dönüştürücülerle korunarak indirme başlatma süreci tamamen stabil hale getirildi.

### 🚀 FDownloader v2.0.10 Yenilikleri & Düzeltmeleri:

• 🛠️ **Kritik İndirme Başlatma Hatası Düzeltildi:**
  - Var olan veritabanı dosyalarında `FormatSpecifier` kolonunun eksik kalması nedeniyle indirme eklenirken oluşan `SQLite Error 1: 'table DownloadTasks has no column named FormatSpecifier'` hatası giderildi.
  - Veritabanı şema migrasyonu (V2) doğrudan uygulanarak eksik kolon geriye dönük uyumlu olarak otomatik eklendi.
