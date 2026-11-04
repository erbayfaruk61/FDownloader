# FDownloader Tarayıcı Eklentisi — Mağaza Yayını Hazırlık Rehberi

> **Kapsam:** Chrome Web Store, Microsoft Edge Add-ons ve Firefox AMO (Add-ons)  
> **İlgili Görev:** `P4-06` (Faz 4 — Tarayıcı Eklentisi ve Entegrasyon)  
> **Eklenti Sürümü:** 1.5.0 (Manifest V3)

---

## 1. Mağaza Listeleme Metinleri

### 1.1 Başlık ve Kısa Açıklama

- **Eklenti Adı (Türkçe):** FDownloader Entegrasyonu
- **Extension Name (English):** FDownloader Integration
- **Kısa Açıklama (Türkçe - Maks. 132 karakter):**  
  `FDownloader indirme yöneticisi için kesintisiz ve akıllı otomatik tarayıcı indirme yakalama eklentisi.`
- **Short Description (English - Max 132 chars):**  
  `Seamless and intelligent automatic download capture extension for FDownloader download manager.`

---

### 1.2 Detaylı Açıklama (Detailed Store Description)

#### Türkçe Açıklama:
```markdown
⚡ FDownloader Entegrasyonu — Tarayıcınız için Yüksek Hızlı İndirme Asistanı

FDownloader tarayıcı eklentisi, internette gezinirken indirmek istediğiniz tüm büyük dosyaları, arşivleri (.rar, .zip, .7z, .iso) ve medya dosyalarını (.mp4, .mkv, .mp3) otomatik olarak algılar ve yüksek hızlı çok parçalı indirme motoruna (FDownloader Masaüstü Uygulaması) aktarır.

Öne Çıkan Özellikler:
• Otomatik İndirme Yakalama: Web sayfalarında tıkladığınız indirme bağlantıları tarayıcı yerine anında FDownloader'a devredilir.
• IDM Stili Alt Tuşu ile Atlama: Bir dosyayı FDownloader yerine doğrudan tarayıcının kendi indirmesini isterseniz, indirme linkine tıklarken Alt tuşuna basmanız yeterlidir.
• Akıllı Video & Medya Yakalayıcı: Web sayfalarındaki HTML5 videoların ve yayın akışlarının köşesinde beliren hafif butonla videoları tek tıkla yakalayın.
• HLS (.m3u8) ve DASH (.mpd) Desteği: Canlı ve parçalı video akışlarını otomatik tespit eder.
• DRM Koruması Saygısı: Telif hakları ve DRM ile korunan içerikler algılandığında gereksiz indirme denemesi yapılmaz, kullanıcı açıkça bilgilendirilir.
• Hariç Tutulan Siteler: Dilediğiniz web sitelerini veya şirket içi ağları otomatik yakalama dışında tutabilirsiniz.
• Çerez ve Oturum Desteği: Google Drive ve üyelik gerektiren sitelerdeki oturum çerezlerinizi yerel olarak FDownloader'a aktararak kesintisiz indirme sağlar.
• Sıfır Veri Toplama: Hiçbir kişisel veri, indirme bağlantısı veya tarama geçmişi dış sunuculara aktarılmaz. Tüm iletişim yalnızca kendi bilgisayarınızda (127.0.0.1) gerçekleşir.

Not: Bu eklentinin çalışabilmesi için FDownloader masaüstü uygulamasının bilgisayarınızda yüklü olması gerekir.
```

#### English Description:
```markdown
⚡ FDownloader Integration — High-Speed Download Assistant for Your Browser

The FDownloader browser extension automatically captures your downloads, archives (.rar, .zip, .7z, .iso), and media streams, handing them off to the high-performance multi-segment FDownloader Desktop Application.

Key Features:
• Automatic Download Capture: Intercepts browser downloads seamlessly.
• IDM-Style Alt Key Bypass: Hold the Alt key while clicking any link to let your browser handle the download directly.
• Video Grabber: Download web videos and streams via a discreet overlay button.
• HLS (.m3u8) & DASH (.mpd) Detection: Automatically discovers modern streaming manifests.
• DRM Protection Awareness: Detects DRM-protected content and informs the user transparently.
• Excluded Sites List: Bypass capture for specific websites or internal intranets.
• Cookie & Authentication Support: Retains session credentials for protected downloads.
• Zero Data Collection: 100% private. Operates strictly over local loopback (127.0.0.1).
```

---

## 2. Mağaza Formu İzin Gerekçeleri (Permission Justifications)

Chrome Web Store ve Edge Add-ons panellerinde her izin için istenen tek cümlelik gerekçeler:

- **Single Purpose Description (Tek Amaç Tanımı):**  
  `Captures user-initiated file downloads and video streams from the browser and transfers them via local HTTP loopback to the FDownloader desktop download manager.`
- **`downloads` Permission Justification:**  
  `Required to intercept browser downloads via chrome.downloads.onDeterminingFilename and cancel browser downloads once handed off to FDownloader.`
- **`cookies` Permission Justification:**  
  `Required to retrieve session cookies for the download URL so that authenticated file downloads (e.g., Google Drive, private file hosts) can proceed inside FDownloader.`
- **`contextMenus` Permission Justification:**  
  `Used to add the "Download with FDownloader" option when the user right-clicks on downloadable links, videos, or images.`
- **`storage` Permission Justification:**  
  `Used to store user preferences such as capture enabled/disabled, Alt-key bypass timeout, excluded domains, and video button position.`
- **Host Permissions (`*://*/*` and `127.0.0.1:9666`):**  
  `Needed to read cookies for download domains and communicate with the local FDownloader desktop client via localhost loopback HTTP.`

---

## 3. Ekran Görüntüsü ve Görsel Varlıklar Listesi

Mağaza panellerine yüklenecek görsellerin standart boyutları:

| Varlık Türü | Boyut (px) | Format | İçerik / Senaryo |
|---|---|---|---|
| Eklenti İkonu | 128x128 | PNG (Şeffaf) | `tools/browser-extension/icons/icon-128.png` |
| Ekran Görüntüsü 1 | 1280x800 | PNG / JPG | Tarayıcıda bir indirme tıklandığında FDownloader penceresinin açılışı |
| Ekran Görüntüsü 2 | 1280x800 | PNG / JPG | Eklenti açılır popup menüsü (bağlantı durumu ve ayarlar) |
| Ekran Görüntüsü 3 | 1280x800 | PNG / JPG | Video oynatıcısının üzerinde "Bu Videoyu İndir" butonu |
| Küçük Tanıtım Görseli | 440x280 | PNG / JPG | FDownloader logosu ve "Yüksek Hızlı İndirme Entegrasyonu" başlığı |
| Büyük Tanıtım Görseli | 920x680 / 1400x560 | PNG / JPG | Özellik maddeleri ve modern masaüstü/tarayıcı kompozisyonu |

---

## 4. Gönderim ve Yayın Adımları

### 4.1 Chrome Web Store
1. `tools/browser-extension/package.ps1` scriptini çalıştırın; `dist/FDownloader-Extension-Chrome.zip` dosyasını alın.
2. [Chrome Web Store Developer Dashboard](https://chrome.google.com/webstore/devconsole) sayfasına giriş yapın.
3. "Yeni Öğe Ekle" (New Item) butonuna basıp üretilen ZIP dosyasını yükleyin.
4. Yukarıdaki başlık, açıklama, kategori ("Üretkenlik / Araçlar") ve gizlilik politikasını (`docs/GIZLILIK.md` URL'si) girin.
5. İncelemeye gönderin (genellikle 24–48 saat sürer).

### 4.2 Microsoft Edge Add-ons
1. [Microsoft Partner Center](https://partner.microsoft.com/dashboard/microsoftedge) sayfasına gidin.
2. `dist/FDownloader-Extension-Chrome.zip` dosyasını "Yeni Eklenti" olarak yükleyin (Edge, Chrome manifestini doğrudan kabul eder).
3. Açıklama ve gizlilik bağlantısını ekleyip gönderin.

### 4.3 Mozilla Firefox (AMO)
1. `dist/FDownloader-Extension-Firefox.zip` dosyasını alın.
2. [Firefox Add-ons Developer Hub](https://addons.mozilla.org/developers/) sayfasına gidin.
3. ZIP paketini yükleyin ve doğrulamayı tamamlayıp yayınlayın.
