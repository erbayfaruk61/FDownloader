# FDownloader — Yanlış Pozitif (False Positive) Bildirim Kılavuzu ve Şablonları

> Bu doküman, FDownloader kurulum dosyalarının (`Setup.exe` veya `FDownloader.App.exe`) antivirüs / EDR güvenlik yazılımları tarafından yanlış pozitif (false positive) olarak tespit edilmesi durumunda yapılacak adımları ve firmalara gönderilecek başvuru şablonlarını içerir.

---

## 1. Neden Yanlış Pozitif Olabilir?

1. **Yeni Yazılım İtibarı:** Yeni oluşturulan açık kaynak yazılımlar, yeterli sayıda kullanıcı tarafından indirilene ve kod imzalama sertifikası itibar kazanana kadar sezgisel (heuristic) algoritmalar tarafından şüpheli görülebilir.
2. **Ağ ve Soket Aktiviteleri:** Çok parçalı yüksek hızlı paralel indirme ve BitTorrent eşler arası (P2P) protokol paketleri, bazı temel sezgisel filtreleri tetikleyebilir.
3. **Velopack Kurulum Altyapısı:** Paket açma ve otomatik güncelleme mekanizmaları benzer yapıdaki yazılımlarla karıştırılabilir.

---

## 2. Sağlamlık ve Doğrulama Adımları

Kullanıcılar veya güvenlik araştırmacıları için doğrulama adımları:
1. **SHA-256 Doğrulaması:** GitHub Releases sayfasında yayınlanan resmi `SHA256SUMS.txt` dosyasındaki hash ile indirilen dosyanın hash'i karşılaştırılmalıdır:
   ```powershell
   Get-FileHash -Algorithm SHA256 .\FDownloader-Setup.exe
   ```
2. **VirusTotal Taraması:** Dosya [VirusTotal](https://www.virustotal.com/)'a yüklenerek 70+ antivirüs motorunun ortak kararı incelenmelidir.

---

## 3. Antivirüs Firmalarına İtiraz Bildirim Kanalları

| Firma / Servis | Bildirim Portalı / E-posta |
|---|---|
| **Microsoft Defender** | [Microsoft Security Intelligence Portal](https://www.microsoft.com/en-us/wdsi/filesubmission) |
| **Kaspersky** | [Kaspersky OpenTIP (Threat Intelligence Portal)](https://opentip.kaspersky.com/) |
| **Avast / AVG** | [Avast False Positive Form](https://www.avast.com/false-positive-file-form.php) |
| **Bitdefender** | [Bitdefender False Positive Submission](https://www.bitdefender.com/consumer/support/answer/29358/) |
| **Malwarebytes** | [Malwarebytes Forum False Positive Submissions](https://forums.malwarebytes.com/forum/42-file-detections/) |
| **ESET** | `samples@eset.com` |
| **Symantec / Norton** | [Broadcom / Norton False Positive Portal](https://submit.norton.com/) |

---

## 4. İtiraz Başvuru E-posta / Form Şablonu

### Türkçe Başvuru Şablonu
```
Konu: Yanlış Pozitif (False Positive) Bildirimi — FDownloader Setup

Sayın Güvenlik Ekibi,

Geliştiricisi olduğumuz açık kaynaklı "FDownloader" (Masaüstü Çok Parçalı İndirme Yöneticisi) uygulamamızın son sürüm kurulum dosyası, güvenlik motorunuz tarafından yanlış pozitif (False Positive) olarak işaretlenmektedir.

Dosya Bilgileri:
- Uygulama Adı: FDownloader
- Proje Web Sitesi / Kaynak Kod: https://github.com/erbayfaruk61/FDownloader
- Dosya Adı: FDownloader-Setup.exe
- Sürüm: [SÜRÜM_NO]
- SHA-256 Hash: [DOSYA_SHA256_HASH]
- Tespit Edilen Uyarı: [ÖR: Trojan:Win32/Wacatac... veya Heur.Bzc...]

Açıklama:
Uygulamamız MIT lisanslı, açık kaynaklı bir .NET 9 & Avalonia UI masaüstü indirme aracıdır. Kötü amaçlı hiçbir kod, telemetri veya zararlı bileşen içermemektedir. Çok parçalı HTTP indirme ve BitTorrent ağ paketleri nedeniyle sezgisel (heuristic) filtrenize takıldığı değerlendirilmektedir.

Dosyanın yeniden analiz edilerek beyaz listeye (whitelisted) eklenmesini rica ederiz.

Saygılarımızla,
FDownloader Proje Ekibi
İletişim: erbayfaruk61@gmail.com
```

### İngilizce Başvuru Şablonu (Global Vendors)
```
Subject: False Positive Detection Report — FDownloader Setup

Dear Security Team,

Our legitimate open-source desktop download manager application, "FDownloader", is currently being falsely detected by your security software as malicious.

File Details:
- Application Name: FDownloader
- Repository / Source Code: https://github.com/erbayfaruk61/FDownloader
- File Name: FDownloader-Setup.exe
- Version: [VERSION_NUMBER]
- SHA-256 Hash: [FILE_SHA256_HASH]
- Current Detection Name: [DETECTION_NAME]

Description:
FDownloader is a clean, open-source modern download manager built with .NET 9 and Avalonia UI (MIT Licensed). It contains no malware, spyware, backdoors, or telemetry tracking. The detection appears to be a false positive caused by heuristic analysis of high-concurrency network sockets or installer packaging.

We kindly request a re-evaluation and whitelisting of our installer and binary.

Thank you for your assistance.

Sincerely,
FDownloader Development Team
Repository: https://github.com/erbayfaruk61/FDownloader
Contact: erbayfaruk61@gmail.com
```

