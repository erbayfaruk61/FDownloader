# FDownloader — Hata Kodları ve Sorun Giderme Rehberi

Bu belge, FDownloader indirme motoru ve kullanıcı arayüzü tarafından üretilen standart hata kodlarını, kullanıcı dostu açıklamalarını ve çözüm yollarını içerir.

Her hata mesajı kullanıcıya **Ne Oldu / Neden Olmuş Olabilir / Ne Yapabilirsin** formatında sunulur.

---

## 1. Ağ ve HTTP Hataları

### `ERR_HTTP_404` — Dosya Sunucuda Bulunamadı
- **Ne Oldu:** İndirilmek istenen dosya sunucuda bulunamadı.
- **Neden Olmuş Olabilir:**
  - Dosya sunucu yöneticisi tarafından silinmiş veya başka bir adrese taşınmış.
  - İndirme bağlantısı geçersiz veya eksik kopyalanmış.
  - Tek kullanımlık oturum bağlantısının geçerlilik süresi dolmuş.
- **Ne Yapabilirsin:**
  - İndirme bağlantısını tarayıcınızda açmayı deneyin.
  - Siteden taze bir indirme bağlantısı alıp FDownloader'da **"İndirme Adresini Yenile"** seçeneğini kullanın.

### `ERR_HTTP_401` & `ERR_HTTP_403` — Yetkisiz Erişim / Yasak
- **Ne Oldu:** Sunucu indirme isteğini yetkisiz veya yasak olarak reddetti.
- **Neden Olmuş Olabilir:**
  - Siteye giriş (kullanıcı adı ve parola) yapılması gerekiyor.
  - İndirme bağlantısı sadece belirli çerezler (cookies) veya Referer başlığı ile çalışıyor.
  - IP adresiniz sunucu güvenlik duvarı (Cloudflare, firewall vb.) tarafından geçici olarak engellenmiş.
- **Ne Yapabilirsin:**
  - **Ayarlar > Site Şifreleri** menüsünden ilgili site için kullanıcı adı ve parolanızı kaydedin.
  - FDownloader tarayıcı eklentisini kullanarak bağlantıyı tarayıcı oturumunuz üzerinden yakalatın.
  - VPN veya vekil sunucu (Proxy) ayarlarınızı kontrol edin.

### `ERR_HTTP_429` — İstek Sınırı Aşıldı (Too Many Requests)
- **Ne Oldu:** Sunucu çok fazla istek yapıldığı gerekçesiyle indirmeyi geçici olarak durdurdu.
- **Neden Olmuş Olabilir:**
  - Sunucunun aynı IP'den izin verdiği anlık bağlantı veya istek limiti aşıldı.
  - Dosya için çok yüksek sayıda eşzamanlı segment (parça) bağlantısı açıldı.
- **Ne Yapabilirsin:**
  - Sunucunun belirttiği bekleme süresi (Retry-After) dolana kadar bekleyin. FDownloader süresi dolunca otomatik olarak devam edecektir.
  - İndirme ayarlarından segment sayısını (örneğin 8'den 2 veya 4'e) düşürün.

### `ERR_HTTP_416` — Parçalı İndirme Desteklenmiyor
- **Ne Oldu:** Sunucu istenen parça bayt aralığını (Range request) desteklemiyor.
- **Neden Olmuş Olabilir:**
  - Sunucu dinamik üretilen veya akış halindeki dosyalar için duraklatma/devam etme desteği sunmuyor.
- **Ne Yapabilirsin:**
  - İndirmeyi 1 parça (tek bağlantı) olarak sıfırdan başlatın.

### `ERR_NETWORK_TRANSIENT` — Geçici Ağ Kopması veya Zaman Aşımı
- **Ne Oldu:** Ağ bağlantısında geçici bir kopma, zaman aşımı veya paket kaybı yaşandı.
- **Neden Olmuş Olabilir:**
  - Wi-Fi sinyali koptu, kablo bağlantısı kesildi veya internet servis sağlayıcısında dalgalanma oldu.
  - Hedef web sunucusu aşırı yük altında olduğundan bağlantı zaman aşımına uğradı.
- **Ne Yapabilirsin:**
  - İnternet bağlantınızı kontrol edin.
  - FDownloader akıllı yeniden deneme (exponential backoff) mekanizması ile indirmeyi otomatik olarak sürdürecektir.

---

## 2. Disk ve Dosya Sistemi Hataları

### `ERR_DISK_FULL` — Yetersiz Disk Alanı
- **Ne Oldu:** Hedef sürücüde indirmeyi tamamlayacak yeterli boş alan yok.
- **Neden Olmuş Olabilir:**
  - Sabit diskiniz veya bağlı harici USB depolama biriminiz doldu.
  - FDownloader'ın disk parçalanmasını önleyen önceden yer ayırma (preallocation) işlemi sırasında alan yetmedi.
- **Ne Yapabilirsin:**
  - Sürücüdeki gereksiz dosyaları veya çöp kutusunu temizleyerek yer açın.
  - **Araçlar > Artık Dosyaları Temizle** penceresinden eski yarım kalmış geçici dosyaları temizleyin.
  - Görev özelliklerinden indirme klasörünü daha geniş alana sahip başka bir sürücüye taşıyın.

### `ERR_FILE_PERMISSION` — Erişim Engellendi / Dosya İzni Hatası
- **Ne Oldu:** Dosyaya yazma veya klasöre erişim izni engellendi.
- **Neden Olmuş Olabilir:**
  - Hedef klasör Windows tarafından salt okunur veya yönetici izinlerine kilitlenmiş.
  - İndirmenin yapıldığı harici USB disk veya ağ sürücüsü (NAS) fiziksel olarak çıkarılmış/kopmuş.
  - Bilgisayardaki bir antivirüs veya güvenlik yazılımı geçici `.fdown` dosyasını tarama için kilitlemiş.
- **Ne Yapabilirsin:**
  - Hedef klasörün yazılabilir olduğunu kontrol edin.
  - Uygulamayı "Yönetici Olarak Çalıştır" seçeneğiyle açmayı deneyin.
  - Antivirüs yazılımınızın geçici indirme klasörünü engellemediğinden emin olun.

---

## 3. Güvenlik ve Telif Hakları

### `ERR_DRM_PROTECTED` — Dijital Hak Yönetimi (DRM) Koruması
- **Ne Oldu:** Bu medya içeriği DRM (Dijital Hak Yönetimi) ile şifrelenmiştir ve indirilemez.
- **Neden Olmuş Olabilir:**
  - İçerik sağlayıcısı video akışını Widevine, PlayReady veya FairPlay şifreleme anahtarları ile kilitlemiştir.
- **Ne Yapabilirsin:**
  - DRM korumalı şifreli ticari yayınlar yasal kurallar ve güvenlik protokolleri gereği indirilemez.
  - İçeriği orijinal resmi web sitesi veya lisanslı platformu üzerinden izleyebilirsiniz.

---

## 4. Dosya Bütünlüğü ve Hash Hataları

### `ERR_CHECKSUM_MISMATCH` — Dosya Doğrulama / Bütünlük Uyuşmazlığı
- **Ne Oldu:** İndirilen dosyanın hesaplanan hash değeri (MD5/SHA-256) beklenen değerle eşleşmedi.
- **Neden Olmuş Olabilir:**
  - İndirme sırasında ağ paketlerinde bozulma meydana gelmiş olabilir.
  - Sunucudaki dosya güncellenmiş ve yeni bir sürüme geçmiş olabilir.
- **Ne Yapabilirsin:**
  - Göreve sağ tıklayıp "Yeniden Kontrol Et" veya "Sıfırdan İndir" seçeneğini kullanın.

