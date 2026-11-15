### 🚀 FDownloader v2.0.12 Yenilikleri & Düzeltmeleri:

• ⚡ **Yeni Özellik: Turbo MP3 Modu:**
  - Ana araç çubuğuna Pano İzleyicisi'nin hemen yanına **⚡ Turbo MP3** modu seçeneği eklendi.
  - Açık olduğunda panodan veya tarayıcıdan yakalanan medya/video bağlantıları diyalog penceresi açılmadan doğrudan arka planda MP3 formatında indirilmeye başlar.
• 🎵 **YouTube ve YouTube Music İndirme Ayrıştırması Düzeltildi:**
  - `yt-dlp` ses formatı parametreleri ayrıştırılarak `Requested format is not available` hatası tamamen giderildi, `bestaudio/best` akış seçicisiyle kesintisiz indirme sağlandı.
• ✨ **Canlı Medya Durum Animasyonu:**
  - Yeni indirme modalinde medya bilgisi taranırken döner neon shimmer efekti ve dinamik nabız simgesi eklendi.
• 🛡️ **Medya İsimlendirme ve Çakışma Koruması:**
  - Medya meta verileri çekilmeden erken tıklama engellendi, çakışmalar ve dosya üzerine yazma sorunları önlendi.

### 🚀 FDownloader v2.0.11 Yenilikleri & Düzeltmeleri:

• 🛠️ **Medya Ayrıştırma ve İndirme Hatası Düzeltildi:**
  - YouTube ve çeşitli video servislerinde `yt-dlp` tarafından döndürülen ondalıklı FPS (örn. 0.63 gibi storyboard/özel akış kare hızları) nedeniyle oluşan `FormatException: One of the identified items was in an invalid format.` hatası giderildi.
  - Video çözünürlük, süre, kare hızı ve dosya boyutu alanları esnek ve toleranslı tip dönüştürücülerle korunarak indirme başlatma süreci tamamen stabil hale getirildi.

### 🚀 FDownloader v2.0.10 Yenilikleri & Düzeltmeleri:

• 🛠️ **Kritik İndirme Başlatma Hatası Düzeltildi:**
  - Var olan veritabanı dosyalarında `FormatSpecifier` kolonunun eksik kalması nedeniyle indirme eklenirken oluşan `SQLite Error 1: 'table DownloadTasks has no column named FormatSpecifier'` hatası giderildi.
  - Veritabanı şema migrasyonu (V2) doğrudan uygulanarak eksik kolon geriye dönük uyumlu olarak otomatik eklendi.
