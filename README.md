# FDownloader - İndirme Yöneticisi

<p align="center">
  <img src="src/FDownloader.App/Assets/logo.png" alt="FDownloader Logo" width="128" height="128" />
</p>

<p align="center">
  <strong>Yüksek performanslı, modern ve çok yönlü masaüstü indirme yöneticisi.</strong>
</p>

<p align="center">
  <a href="https://github.com/erbayfaruk61/FDownloader/releases/latest">
    <img src="https://img.shields.io/github/v/release/erbayfaruk61/FDownloader?style=for-the-badge&logo=github&color=10B981" alt="Latest Release" />
  </a>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 9" />
  <img src="https://img.shields.io/badge/UI-Avalonia-8A2BE2?style=for-the-badge" alt="Avalonia UI" />
  <img src="https://img.shields.io/badge/Lisans-MIT-blue?style=for-the-badge" alt="MIT License" />
</p>

<p align="center">
  <a href="https://github.com/erbayfaruk61/FDownloader/releases/latest/download/FDownloader-win-Setup.exe">
    <img src="https://img.shields.io/badge/İndir-FDownloader--win--Setup.exe-success?style=for-the-badge&logo=windows&logoColor=white" height="36" alt="Windows İndir" />
  </a>
</p>

---

## 🚀 Öne Çıkan Özellikler

* ⚡ **Çok Kanallı & Parçalı İndirme:** Dosyaları dinamik parçalara bölerek bağlantı hızınızı maksimum seviyede kullanır; kesilen indirmeleri kaldığı yerden devam ettirir.
* 🌊 **Torrent Desteği:** Magnet bağlantılarını ve `.torrent` dosyalarını doğrudan uygulama içerisinden indirip yönetebilme.
* 🎬 **Akıllı Medya Yakalama:** Video ve ses bağlantılarından doğrudan meta veri alıp MP4 video veya MP3 ses olarak kaydedebilme.
* 📈 **Canlı Hız Grafiği:** Gerçek zamanlı indirme hızını ve istatistikleri görselleştiren pürüzsüz grafik paneli.
* 🧩 **Tarayıcı Köprüsü & Eklenti:** Chrome, Edge, Brave ve Opera üzerinden web indirmelerini otomatik olarak yakalayan yerel köprü desteği.
* 🔄 **Otomatik Güncelleme:** Velopack altyapısı sayesinde yeni sürümleri arka planda otomatik algılar ve tek tıkla günceller.
* 🎨 **Gelişmiş Tema & Görünüm:** Koyu Grafit, Okyanus Mavisi, Gece Siyahı gibi zengin renk temaları ve kişiselleştirilebilir arayüz.

---

## 📥 Kurulum

1. [En Son Sürümü İndirin](https://github.com/erbayfaruk61/FDownloader/releases/latest/download/FDownloader-win-Setup.exe).
2. `FDownloader-win-Setup.exe` dosyasını çalıştırın.
3. Masaüstü ve Başlat Menüsü kısayollarınız otomatik olarak oluşturulacaktır.

---

## 🧩 Tarayıcı Eklentisi Kurulumu

FDownloader'ın tarayıcınızdaki indirmeleri otomatik yakalayabilmesi için:

1. [Releases](https://github.com/erbayfaruk61/FDownloader/releases/latest) sayfasından `FDownloader-Browser-Extension.zip` dosyasını indirin ve bir klasöre çıkartın *(veya FDownloader Ayarlar -> Tarayıcı Köprüsü sekmesindeki "Klasörü Aç" butonunu kullanın)*.
2. Tarayıcınızda `chrome://extensions` (Edge için `edge://extensions`) sayfasına gidin.
3. Sağ üst köşedeki **Geliştirici Modu (Developer Mode)** anahtarını açın.
4. Çıkarttığınız klasörü sayfaya sürükleyip bırakın veya **Paketlenmemiş Öğe Yükle** butonuna tıklayarak seçin.

---

## 🛠️ Kaynak Koddan Derleme

```powershell
# Depoyu klonlayın
git clone https://github.com/erbayfaruk61/FDownloader.git
cd FDownloader

# Derleyin ve çalıştırın
dotnet run --project src/FDownloader.App/FDownloader.App.csproj
```

---

## 📜 Krediler & Teşekkürler

FDownloader aşağıdaki açık kaynak teknolojiler ve topluluk projeleri üzerine inşa edilmiştir:

* [Avalonia UI](https://avaloniaui.net/) - Çapraz platform XAML arayüz çatısı.
* [MonoTorrent](https://github.com/alanmcgovern/monotorrent) - Güçlü BitTorrent protokol kütüphanesi.
* [yt-dlp](https://github.com/yt-dlp/yt-dlp) - Medya indirme ve ayrıştırma motoru.
* [Velopack](https://velopack.io) - Modern masaüstü yükleyici ve güncelleme dağıtım altyapısı.

---

## 📄 Lisans

Bu proje [MIT Lisansı](LICENSE) altında lisanslanmıştır.
