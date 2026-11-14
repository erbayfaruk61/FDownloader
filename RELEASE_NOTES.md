### 🚀 FDownloader v2.0.10 Yenilikleri & Düzeltmeleri:

• 🛠️ **Kritik İndirme Başlatma Hatası Düzeltildi:**
  - Var olan veritabanı dosyalarında `FormatSpecifier` kolonunun eksik kalması nedeniyle indirme eklenirken oluşan `SQLite Error 1: 'table DownloadTasks has no column named FormatSpecifier'` hatası giderildi.
  - Veritabanı şema migrasyonu (V2) doğrudan uygulanarak eksik kolon geriye dönük uyumlu olarak otomatik eklendi.
