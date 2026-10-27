# Proje Analizi: Yeni Nesil Birleşik İndirme Yöneticisi (Ultimate Downloader)

Bu doküman; **Internet Download Manager (IDM)**, **JDownloader** ve **qBittorrent** yazılımlarının güçlü yönlerini tek bir mimaride bir araya getirecek yeni nesil indirme yöneticisi için gerekli fonksiyonel, teknik ve operasyonel özellikleri listeler.

---

## 1. Çekirdek İndirme Motoru (Core Engine)

* [ ] **Dinamik HTTP/HTTPS/FTP Parçalama (Dynamic Segmentation):** 
  * Dosyayı baştan eşit parçalara bölmek yerine, transfer sırasında hızlı segmentleri dinamik olarak ikiye bölen agresif bağlantı algoritması.
  * İndirme başına 1-64 arası yapılandırılabilir eşzamanlı soket/thread desteği.
* [ ] **Tam BitTorrent / Libtorrent Entegrasyonu:**
  * `.torrent` meta dosyaları ve Magnet link çözümleme.
  * DHT, PeX (Peer Exchange), LSD (Local Peer Discovery), Tracker değişimi desteği.
  * uTP (Micro Transport Protocol) ve standart TCP bağlantıları.
  * Sıralı İndirme (*Sequential Download*): Video dosyaları inerken önizleme/oynatma imkânı.
  * Süper-Seeding, seed oranı ve zamanına göre otomatik tohumlama durdurma kuralları.
* [ ] **Kalıcı Durum ve Hata Kurtarma:**
  * Ağ kopması veya sistem çökmesi durumunda sıfır veri kaybı ile kaldığı bayttan devam etme (Range requests).
  * CRC32, MD5, SHA-1 ve SHA-256 sağlama toplamı (checksum) ile otomatik dosya doğrulama ve bozuk parçaları tekil olarak tekrar indirme.
  * Süresi dolan indirme oturumlarını otomatik yenileme (Link Refresh / Session Refresh).
* [ ] **Bellek ve Disk G/Ç (I/O) Optimizasyonu:**
  * 1 Gbps+ yüksek hızlı hatlarda disk darboğazını önlemek için dinamik RAM tamponlama (Memory Caching).
  * Disk alanı tahsisi (Sparse files / Pre-allocation) ile disk parçalanmasını (fragmentation) engelleme.

---

## 2. Medya Yakalama ve Tarayıcı Entegrasyonu (Sniffer & Grabber)

* [ ] **Hafif Tarayıcı Eklentisi (Browser Integration):**
  * Chrome, Firefox, Edge, Brave, Opera ve Vivaldi için Native Messaging protokolüyle çalışan hafif uzantı.
  * Tarayıcıdaki tüm indirme tıklamalarını şeffaf bir şekilde yakalama ve yönlendirme.
* [ ] **Gelişmiş Medya Akış Yakalayıcı (HLS / DASH / MPD Sniffer):**
  * Web sayfalarındaki gömülü video ve ses akışlarını (`.m3u8`, `.mpd`, fragmented `.mp4`) otomatik tespit etme.
  * Parçalı `.ts`/`.m4s` segmentlerini çoklu kanaldan indirip `ffmpeg` kütüphanesiyle kayıpsız `.mp4`/`.mkv` konteynerine birleştirme.
* [ ] **Pano Dinleyici (Clipboard Watcher) & LinkGrabber:**
  * Panoya kopyalanan URL'leri anında analiz etme; toplu link listelerinden geçerli indirme adreslerini otomatik ayrıştırma.
  * İndirme öncesi linklerin çevrimiçi/çevrimdışı (link availability) durumunu arka planda sorgulama.
* [ ] **Konteyner ve Format Desteği:**
  * DLC, CCF, RSDF, Metalink (`.meta4`, `.metalink`) ve cURL komut formatlarını içe/dışa aktarabilme.

---

## 3. Barındırıcı (Hoster), Debrid ve Hesap Yönetimi

* [ ] **Modüler Eklenti (Plugin) Mimarisi:**
  * Rapidgator, Mega, Google Drive, Mediafire vb. yüzlerce dosya barındırıcı için bağımsız script mimarisi (Python, Lua veya Wasm/JS).
  * Barındırıcı sitelerin HTML yapısı değiştiğinde ana uygulamayı yeniden derlemeden çalışan uzaktan eklenti güncelleme mekanizması.
* [ ] **Debrid Servis Entegrasyonu:**
  * Real-Debrid, AllDebrid, Premiumize ve Debrid-Link API desteği.
  * Torrent linklerini doğrudan Debrid sunucularına aktarıp, önbellekten yüksek hızlı HTTP olarak doğrudan istemciye çekme.
* [ ] **Captcha Aşma ve Doğrulama Desteği:**
  * ReCaptcha, hCaptcha, Cloudflare Turnstile ve Cloudflare WAF algılama.
  * Dahili OCR / küçük parametreli lokal yapay zeka modelleri ile basit captchaları otomatik çözme.
  * Üçüncü taraf API çözücüleri (2Captcha, Anti-Captcha, DeathByCaptcha) desteği ve gerektiğinde manuel çözme penceresi.
* [ ] **IP Yenileme (Reconnection) Motoru:**
  * Ücretsiz barındırıcı kotalarını aşmak için UPnP, Telnet, SSH veya HTTP Router Scriptleri aracılığıyla modeme otomatik reset atıp yeni dinamik WAN IP alma.

---

## 4. Ağ Güvenliği, Proxy ve Trafik Yönetimi

* [ ] **Ağ Arayüzü Bağlama (Network Interface Binding / Kill-Switch):**
  * Trafiği yalnızca seçilen sanal ağ adaptörüne (`tun0`, WireGuard, OpenVPN, Tailscale vb.) kilitleme.
  * VPN bağlantısı kesildiği anda indirme ve seeding trafiğini anında durdurarak gerçek IP sızıntısını kesin olarak engelleme.
* [ ] **Gelişmiş Proxy ve Güvenlik Protokolleri:**
  * Görev veya protokol bazında (HTTP için ayrı, Torrent için ayrı) SOCKS5, HTTP/HTTPS proxy yapılandırması.
  * Torrent için tam şifreleme (Protocol Encryption / PE) zorlama.
* [ ] **Bant Genişliği ve Hız Kısıtlama:**
  * Küresel ve görev bazlı yükleme/indirme hız limitleri.
  * Alternatif hız kipi (tek tuşla veya belirli saatlerde devreye giren düşük hız/arka plan modu).
* [ ] **Gelişmiş Zamanlayıcı (Scheduler):**
  * Haftanın günlerine ve saat dilimlerine göre indirme limitlerini veya durdur/başlat komutlarını otomatize etme (örneğin: Gece 01:00 - 07:00 arası tam hız, gündüz düşük hız).

---

## 5. Otomasyon ve İndirme Sonrası İşlemler (Post-Processing)

* [ ] **Dahili Arşiv Çözücü (Auto-Extractor):**
  * Çok parçalı arşivleri (`.part01.rar`, `.7z.001`, `.zip`, `.tar.gz`) indirme tamamlandığı an otomatik çıkarma.
  * Arşiv başarıyla açıldıktan sonra sıkıştırılmış kaynak dosyaları otomatik silme seçeneği.
  * Parola listesi (Wordlist) desteği ile şifreli arşivleri kayıtlı şifrelerle otomatik deneme.
* [ ] **Kategori ve Akıllı Dosya Yönetimi:**
  * Dosya formatına (Video, Müzik, Program, Belge) veya kaynağa göre otomatik klasörleme.
  * Dosya adı temizleme kuralları (Regex tabanlı filtreleme ile reklam/site etiketlerini temizleme).
* [ ] **Komut Satırı Kancaları (CLI Hooks) ve Webhook:**
  * İndirme bittiğinde özel script çalıştırma (`.bat`, `.sh`, `.ps1`).
  * Telegram botu, Discord Webhook veya mobil push bildirimleri (Gotify, Pushover, Pushbullet) gönderme.
  * İndirme bittiğinde bilgisayarı kapatma, uykuya alma veya hazırda bekletme.

---

## 6. Mimari, Kullanıcı Arayüzü ve Uzaktan Kontrol

* [ ] **Düşük Kaynak Tüketimi (Modern Stack):**
  * Java (JDownloader) ve Electron hantallığından uzak, düşük RAM ve CPU tüketen yerel derleme (Örn: Rust/Tauri, C++/Qt veya Go/Wails).
* [ ] **Çok Katmanlı Mimari (Core Daemon + UI):**
  * Çekirdek motorun GUI'den bağımsız "headless" servis/daemon olarak çalışabilmesi.
  * Docker konteyneri olarak sunucularda veya NAS cihazlarında (TrueNAS, Synology, Unraid) çalışabilme yeteneği.
* [ ] **Uzaktan Erişim ve WebUI:**
  * Masaüstü GUI'ye ek olarak mobil uyumlu, şifre korumalı dahili Web Arayüzü.
  * Harici entegrasyonlar için kapsamlı REST API ve WebSocket arayüzü.
* [ ] **Entegre Arama Motoru:**
  * Eklenti tabanlı torrent ve açık dizin arama motoru (qBittorrent search eklentileri benzeri).

---

## Karşılaştırma Matrisi

| Kriter | Internet Download Manager (IDM) | JDownloader 2 | qBittorrent | Hedeflenen Yeni Downloader |
| :--- | :--- | :--- | :--- | :--- |
| **HTTP Dinamik Parçalama** | Mükemmel (32 thread) | İyi (Sabit parça) | Desteklemiyor | **Mükemmel (Dinamik adaptif)** |
| **BitTorrent / P2P Desteği** | Desteklemiyor | Zayıf/Kullanışsız | Mükemmel (Libtorrent) | **Mükemmel (Libtorrent çekirdeği)** |
| **Filehoster & Debrid Desteği** | Zayıf / Manuel | Mükemmel | Desteklemiyor | **Mükemmel (Modüler eklentiler)** |
| **Tarayıcı Medya Yakalama (Sniffer)** | Mükemmel | Kısmi (LinkGrabber) | Desteklemiyor | **Mükemmel (HLS/DASH/FFmpeg)** |
| **Ağ Arayüzü Kilidi (VPN Kill-Switch)**| Desteklemiyor | Desteklemiyor | Mükemmel | **Mükemmel (Adaptör bağlama)** |
| **Otomatik Arşiv Açma & Parola** | Desteklemiyor | Mükemmel | Desteklemiyor | **Mükemmel (7z/Unrar kütüphanesi)** |
| **Kaynak / Bellek Tüketimi** | Çok Düşük (Native C++) | Yüksek (Java Runtime) | Düşük (C++/Qt) | **Çok Düşük (Rust / C++ hedefi)** |
| **Platform Bağımsızlığı & WebUI** | Yalnızca Windows | Win / macOS / Linux | Win / macOS / Linux + WebUI | **Çapraz Platform + Headless/WebUI** |