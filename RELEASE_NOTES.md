### 🚀 FDownloader v2.0.11 Yenilikleri & Düzeltmeleri:

• 🛠️ **Medya Ayrıştırma ve İndirme Hatası Düzeltildi:**
  - YouTube ve çeşitli video servislerinde `yt-dlp` tarafından döndürülen ondalıklı FPS (örn. 0.63 gibi storyboard/özel akış kare hızları) nedeniyle oluşan `FormatException: One of the identified items was in an invalid format.` hatası giderildi.
  - Video çözünürlük, süre, kare hızı ve dosya boyutu alanları esnek ve toleranslı tip dönüştürücülerle korunarak indirme başlatma süreci tamamen stabil hale getirildi.

### 🚀 FDownloader v2.0.10 Yenilikleri & Düzeltmeleri:

• 🛠️ **Kritik İndirme Başlatma Hatası Düzeltildi:**
  - Var olan veritabanı dosyalarında `FormatSpecifier` kolonunun eksik kalması nedeniyle indirme eklenirken oluşan `SQLite Error 1: 'table DownloadTasks has no column named FormatSpecifier'` hatası giderildi.
  - Veritabanı şema migrasyonu (V2) doğrudan uygulanarak eksik kolon geriye dönük uyumlu olarak otomatik eklendi.
