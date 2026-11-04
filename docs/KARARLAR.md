# Mimari Kararlar ve İlkeler (ADR)

> **Proje:** FDownloader  
> **Kapsam:** Yasal, teknik ve mimari temel kararlar  

---

## 1. DRM (Dijital Hak Yönetimi) ve Şifreli İçerik Politikası

### Durum
Kullanıcılar zaman zaman DRM (Widevine, PlayReady, FairPlay) ile şifrelenmiş video/medya akışlarını (Netflix, Spotify, korumalı canlı yayınlar vb.) indirmek isteyebilir.

### Karar
**FDownloader, hiçbir koşulda DRM aşmaya, şifre çözmeye (decrypt/crack) veya telif korumalarını baypas etmeye yönelik mekanizma İÇERMEZ ve EKLEMEZ.**

### Gerekçe
1. **Yasal Uyum ve Güven:** DMCA (Dijital Binyıl Telif Hakkı Yasası) ve uluslararası telif hakları mevzuatına tam uyum sağlanması, uygulamanın yasal risklerden ve mağaza engellemelerinden (Chrome Web Store, Windows Store, winget) korunması.
2. **Kullanıcı Deneyimi:** Şifreli bir akışı indirip oynatılamaz çöp dosya üretmek yerine, içerik DRM'li ise **en başta algılanıp hiç indirilmemesi** ve kullanıcıya açık bir bilgilendirme mesajı verilmesi sağlanır.

### Uygulama (P4-03)
- Tarayıcı eklentisi `<video>` üzerindeki `mediaKeys` ve `encrypted` olaylarını dinler.
- DRM tespit edilirse indirme butonu `🔒 Korumalı İçerik (DRM)` durumuna geçer ve kullanıcıya "Bu medya DRM ile korunmaktadır ve indirilemez" bilgisi verilir. İndirme isteği başlatılmaz.
- Core motorunda (`DownloadErrorClassifier` ve `YtDlpMediaService`) DRM kaynaklı hatalar `DownloadErrorKind.DrmProtected` olarak etiketlenir ve kullanıcıya net Türkçe hata sunulur.

---

## 2. Telemetri ve Sıfır Dış Sunucu Bağımlılığı (P3-09)

### Karar
Masaüstü uygulaması ve tarayıcı eklentisi, kullanıcının indirme bağlantılarını, çerezlerini veya tarama alışkanlıklarını harici bir sunucuya göndermez. Sentry hata raporlamasında kullanıcı adı, IP ve e-posta tamamen temizlenir (`SetBeforeSend`).

---

## 3. İndirme Motoru Doğruluk Garantisi (P2 Serisi)

### Karar
Her yeni indirme özelliği, SHA-256 hash bütünlüğü, parça birleştirme doğruluğu ve disk alanı güvenliği birim testleriyle doğrulanmadan yayınlanamaz.
