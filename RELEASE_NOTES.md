### 🚀 FDownloader v2.0.9 Yenilikleri & Düzeltmeleri:

• 📸 **README Görsel Güncellemesi:**
  - `README.md` ana sayfasındaki ekran görüntüsü güncel **v2.0.8** arayüzü, indirme listesi ve sürüm rozetleri ile yenilendi.
• 🌐 **Facebook & Sosyal Medya İndirmelerinde Bağlantı Kopma Koruması:**
  - Facebook, Instagram vb. platformlarda CDN'lerin geçici token süre sonu veya ağ kesintilerine karşı otomatik yeniden deneme (retries: 15, fragment-retries: 15, socket-timeout: 30s) yapılandırması eklendi.
  - Tarayıcı eklentisinde Facebook videoları için geçici blob/fbcdn parçaları yerine öncelikle kalıcı Watch / Reel / Post URL'sinin yakalanması sağlanarak indirme motoruyla kayıpsız, tam çözünürlüklü ve kopmayan indirme güvence altına alındı.
