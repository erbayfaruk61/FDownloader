# FDownloader - Sürüm Güncelleme ve Dağıtım Rehberi

Bu belge, projede geliştirme veya hata düzeltmesi yapıldığında yeni sürümün kullanıcılara nasıl dağıtılacağını özetler.

---

## 3 Adımda Yeni Sürüm Yayınlama İş Akışı

### 1. Adım: Değişiklikleri Commit ve Push Edin
Kodunuzu yazıp test ettikten sonra ana dala (`main`) gönderin:

```powershell
git add .
git commit -m "fix: YouTube ses dönüştürme hatası düzeltildi"
git push
```

---

### 2. Adım: Yeni Sürüm Etiketi (Tag) Ekleyin ve Gönderin
Kullanıcılara yeni bir sürüm dağıtmak istediğinizde yeni sürüm etiketini belirleyin (örneğin `v1.0.1`):

```powershell
git tag v1.0.1
git push origin v1.0.1
```

---

### 3. Adım: Otomatik Gerçekleşen Dağıtım Süreci
Etiketi (`v1.0.1`) push ettiğiniz anda herhangi bir manuel işlem yapmanıza gerek kalmaz:

1. **GitHub Actions (.github/workflows/release.yml):** Otomatik olarak tetiklenir.
2. Windows ortamında projeyi derler, gerekli araçları (`yt-dlp`, `ffmpeg`) içine ekler.
3. **Velopack CLI (`vpk pack`):** Kurulum dosyasını (`FDownloader-Setup.exe`) ve delta/full güncelleme paketlerini üretir.
4. Paketleri GitHub Releases sayfasına yeni sürüm olarak yayınlar.
5. **İstemciler (Kullanıcılar):** Uygulamayı açtıklarında veya arayüzdeki rozete tıkladıklarında güncelleme arka planda sessizce indirilir ve tek tıkla yeni sürüme geçerler.

---

## Sürüm Numaralandırma Standartları (SemVer)

* **Hata Düzeltmeleri / Küçük Yamalar (Patch):** `v1.0.1`, `v1.0.2`, `v1.0.3`
* **Yeni Özellikler / Geliştirmeler (Minor):** `v1.1.0`, `v1.2.0`
* **Büyük Arayüz / Mimari Değişiklikleri (Major):** `v2.0.0`
