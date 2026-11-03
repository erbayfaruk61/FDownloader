# FDownloader — Güvenlik ve Güvenilirlik Denetimi (P0-04)

> Denetim tarihi: 2026-11-03  
> Denetleyen: Pro model (P0-04 görevi)  
> Kapsam: 15 güvenlik sorusu — kaynak koddan doğrulandı  

---

## Soru 1: Eklenti ↔ Uygulama İletişim Kanalı
**Bulgu:** 
Eklenti ile masaüstü uygulama arasındaki iletişim, `System.Net.HttpListener` kullanılarak yerel bir HTTP sunucusu üzerinden sağlanmaktadır (`BrowserBridgeService.cs:43-79`).
- **Arayüz ve Port:** Sunucu `http://127.0.0.1:9666/` ve `http://localhost:9666/` adreslerini dinlemektedir (`DefaultPort = 9666`).
- **Kimlik Doğrulama:** HİÇ YOKTUR. İstemcinin kimliğini doğrulayan hiçbir API anahtarı, gizli belirteç (token), HMAC veya eşleştirme parolası bulunmamaktadır.
- **Origin ve Host Kontrolü:** Origin başlığı doğrulanmadığı gibi, tam tersine tüm isteklere kontrolsüz şekilde CORS başlıkları (`Access-Control-Allow-Origin: *`, `Access-Control-Allow-Methods: GET, POST, OPTIONS`, `Access-Control-Allow-Headers: Content-Type, Accept, X-Requested-With`) eklenmektedir (`BrowserBridgeService.cs:107-109`). Host başlığı da doğrulanmamaktadır (DNS Rebinding riski).
- **İletişim Kanalı Türü:** Chrome Native Messaging API'si kullanılmamakta; doğrudan HTTP dinleyicisi tercih edilmektedir.
- **İstismar Riski:** Kullanıcının tarayıcısında açık olan rastgele herhangi bir web sayfası (zararlı bir reklam veya site), JavaScript `fetch('http://127.0.0.1:9666/download', ...)` çağrısıyla masaüstü uygulamasına arka planda izinsiz indirme görevleri ekleyebilir veya `/show` ile pencereyi öne getirebilir (CSRF / Localhost Hijacking).

**Dosya/Satır:** `src/FDownloader.Core/Network/BrowserBridgeService.cs#L22-L79`, `src/FDownloader.Core/Network/BrowserBridgeService.cs#L107-L110`, `src/FDownloader.Core/Network/BrowserBridgeService.cs#L199-L235`, `tools/browser-extension/background.js#L2`  
**Risk:** Kritik  
**Düzeltme:** 
1. Tercihen doğrudan Chrome/Edge Native Messaging protokolüne geçilmeli (bu protokolde yalnızca manifestte tanımlı extension ID'leri uygulama ile konuşabilir).
2. HTTP köprüsü korunacaksa:
   - `Access-Control-Allow-Origin: *` derhal kaldırılmalı; Origin başlığı sadece izinli tarayıcı uzantı kimliğiyle (`chrome-extension://<id>`) sınırlandırılmalı veya tarayıcı web bağlamından gelen istekler doğrudan reddedilmelidir.
   - Host başlığı katı şekilde `127.0.0.1:9666` veya `localhost:9666` olarak doğrulanmalıdır.
   - Masaüstü uygulama ilk çalıştığında rastgele 256-bit bir token üretmeli, bu token eklentiye yerel güvenli yolla aktarılmalı ve her istekte `Authorization: Bearer <token>` başlığı aranmalıdır.
   - Hız sınırlaması (rate limiting) uygulanmalıdır.  
**Görev:** P3-01

---

## Soru 2: Sunucudan Gelen Dosya Adı Temizleme
**Bulgu:** 
Dosya adı temizleme işlemi `FileNameSanitizer.Sanitize()` metodu ile yürütülmektedir (`FileNameSanitizer.cs:30-79`):
- `Path.GetFileName(fileName)` ile yol ayırıcılar (`\`, `/`) ve dizin geçişleri (`..`) temizlenmektedir.
- Windows geçersiz karakterleri (`Path.GetInvalidFileNameChars()`) alt çizgi (`_`) ile değiştirilmektedir. Baştaki ve sondaki nokta ve boşluklar `Trim('.', ' ')` ile ayıklanmaktadır.
- Windows ayrılmış cihaz adları (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`) `WindowsReservedNames` listesiyle denetlenmekte ve başına `_` eklenmektedir (`_CON.txt`).
- **Açıklar / Eksiklikler:**
  1. *Çoklu Uzantı Rezerve Ad Bypassı:* `Path.GetFileNameWithoutExtension("CON.tar.gz")` ifadesi `"CON.tar"` döneceğinden rezerve ad kontrolü tetiklenmemekte ve dosya adı `CON.tar.gz` olarak kalmaktadır. Windows dosya sistemi bunu ayrılmış aygıt adı kabul ederek hata verebilir.
  2. *Negatif Uzantı İndeksiyle Çökme (DoS):* `fileName = name[..(MaxFileNameLength - ext.Length)] + ext;` satırında eğer uzantı (`ext.Length`) 200 karakterden uzun gelirse `MaxFileNameLength - ext.Length` negatif değere düşmekte ve `ArgumentOutOfRangeException` ile uygulama çökmektedir.
  3. Unicode normalizasyonu (NFC) ve gizli biçimlendirme karakterleri (örn. Right-to-Left Override `U+202E`) filtrelenmemektedir.

**Dosya/Satır:** `src/FDownloader.Core/Utilities/FileNameSanitizer.cs#L30-L79`, `src/FDownloader.Core/Engine/Probe/DownloadProber.cs#L132-L143`  
**Risk:** Orta  
**Düzeltme:** 
1. Rezerve ad denetimi dosyanın ilk noktasından önceki ana isme göre yapılmalıdır (örn: `^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$`).
2. Uzantı uzunluğu sınırlandırılmalı ve `Math.Max(0, MaxFileNameLength - ext.Length)` koruması eklenmelidir.
3. Unicode normalizasyonu (Form C) uygulanmalı ve çift yönlü metin manipülasyon karakterleri temizlenmelidir.  
**Görev:** P2-09

---

## Soru 3: Resume Bilgisi ve Checkpoint Güvenilirliği
**Bulgu:** 
Resume verileri SQLite veritabanındaki `tasks.db` dosyasında, `DownloadTasks` ve `Segments` tablolarında saklanmaktadır (`Database.cs:83-125`).
- **Kayıt Sıklığı:** `AppSettings.cs:61` dosyasında `CheckpointIntervalSeconds = 3` ve `CheckpointIntervalMb = 10` ayarları yer almasına rağmen, `HttpDownloader.cs` çalışma döngüsünde bu ayarlara dayalı periyodik bir checkpoint mekanizması ÇALIŞMAMAKTADIR.
- `DownloadRepository.SaveSegmentProgress` metodu yalnızca indirme başarıyla tamamlandığında çağrılmaktadır (`HttpDownloader.cs:263`).
- `PauseAsync` çağrıldığında sadece görev tablosundaki durum güncellenmekte (`HttpDownloader.cs:87`), segment ilerlemeleri veritabanına kaydedilmemektedir.
- **Diske Yazılmadan Kayıt Riski:** `SegmentWorker.cs:167-170`'te `await _writer.WriteAtAsync(...)` sonrasında `_segment.DownloadedBytes += bytesRead` bellekte güncellenmektedir. Ancak `FileWriter.cs:33-40` içinde 64 KB'lık bellek içi `FileStream` tamponu kullanılmakta ve her yazmada `FlushAsync` çağrılmamaktadır.
- Uygulama aniden çökerse, işletim sistemi kapatılırsa veya süreç zorla sonlandırılırsa (SIGKILL/Crash), veritabanındaki segment bilgileri ya sıfır kalmakta ya da diske henüz yazılmamış bellek verilerini aşmaktadır. Yeniden açıldığında kaldığı yer bozuk olacaktır.

**Dosya/Satır:** `src/FDownloader.Core/Engine/HttpDownloader.cs#L86-L89`, `src/FDownloader.Core/Engine/HttpDownloader.cs#L261-L264`, `src/FDownloader.Core/Engine/Worker/SegmentWorker.cs#L167-L174`, `src/FDownloader.Core/Engine/Writer/FileWriter.cs#L33-L40`, `src/FDownloader.Core/Engine/Writer/FileWriter.cs#L87-L91`  
**Risk:** Yüksek  
**Düzeltme:** 
1. `HttpDownloader` içinde her 3 saniyede veya her 10 MB indirmede çalışan bir arka plan checkpoint zamanlayıcısı kurulmalıdır.
2. Checkpoint esnasında önce `FileWriter.FlushAsync()` çağrılarak disk tamponu boşaltılmalı, ardından onaylanmış bayt miktarı `DownloadRepository.SaveSegmentProgress` ile tek bir SQLite transaction içinde atomik olarak güncellenmelidir.
3. `PauseAsync` metodunda segmentlerin son durumu da veritabanına yazılmalıdır.  
**Görev:** P2-02

---

## Soru 4: Dosya Yazımı ve Birleştirme Mekanizması
**Bulgu:** 
Dosya yazımı tek bir geçici dosya (`.fdown`) üzerinde, önceden yer ayırma (`preallocation`) ve offset tabanlı yazma yöntemiyle yapılmaktadır (`FileWriter.cs:23-58, 68-82`).
- Dosya boyutu biliniyorsa (`totalBytes > 0`), `_stream.SetLength(totalBytes)` ile diske tam boyut baştan rezerve edilmektedir (parçalanmayı önlemek için).
- Ayrı ayrı parça dosyaları oluşturulmamaktadır. Tüm `SegmentWorker` çalışanları tek bir paylaşımlı `FileWriter` nesnesine kendi offset'lerini ileterek yazmaktadır.
- Eşzamanlılık: `SemaphoreSlim _writeLock = new(1, 1)` ile tek bir kilit altında `_stream.Seek(offset, SeekOrigin.Begin)` ve `WriteAsync` senkronize edilmektedir (`FileWriter.cs:72-81`).
- Birleştirme: Ayrı parça dosyaları olmadığı için ek bir birleştirme maliyeti yoktur. İndirme bittiğinde geçici dosya `File.Move(tempFilePath, finalPath, overwrite: true)` ile atomik olarak taşınmaktadır (`FileWriter.cs:119`).

**Dosya/Satır:** `src/FDownloader.Core/Engine/Writer/FileWriter.cs#L23-L127`, `src/FDownloader.Core/Engine/HttpDownloader.cs#L196-L201`, `src/FDownloader.Core/Engine/HttpDownloader.cs#L257`  
**Risk:** Düşük  
**Düzeltme:** 
1. Mimari yaklaşım doğrudur. Ancak yüksek hızlı bağlantılarda `SemaphoreSlim` ve `Seek` kilit çekişmesi (lock contention) oluşturabilir. .NET 6+ ile gelen `RandomAccess.WriteAsync(fileHandle, buffer, offset)` API'sine geçilerek kilit mekanizması tamamen kaldırılabilir.
2. `SetLength` sırasında yetersiz disk alanı oluşursa fırlatılan hata kullanıcı arayüzünde daha anlaşılır bir mesaja dönüştürülmelidir.  
**Görev:** P2-08

---

## Soru 5: Sunucu Dosyası Değişimi (ETag / Last-Modified Kontrolü)
**Bulgu:** 
İndirme yeniden başlatılırken (Resume), `HttpDownloader.cs:133` satırındaki:
`if (_task.TotalBytes <= 0 || string.IsNullOrEmpty(_task.FileName))`
koşulu nedeniyle, eğer indirme daha önce başlatılmışsa **DownloadProber TEKRAR ÇALIŞTIRILMAMAKTADIR**.
- Sunucudaki dosyanın değişip değişmediği (`ETag`, `Last-Modified`, `Content-Length`) kontrol edilmemektedir.
- `SegmentWorker.BuildRequest` metodunda HTTP `If-Range` veya `If-Match` başlıkları gönderilmemektedir (`SegmentWorker.cs:185-206`).
- **Risk:** Dosya sunucuda güncellendiyse, motor eski dosyanın devamı gibi yeni dosyanın baytlarını eski ofsetlerin üzerine yazacaktır. Bu durum kullanıcının fark edemeyeceği sessiz dosya bozulmasına (silent data corruption) yol açar. Sunucu 200 OK dönerse worker ofsetini sıfırlamakta (`SegmentWorker.cs:130-134`) ve diğer segmentlerin yazdığı verileri ezmektedir.

**Dosya/Satır:** `src/FDownloader.Core/Engine/HttpDownloader.cs#L133-L179`, `src/FDownloader.Core/Engine/Worker/SegmentWorker.cs#L185-L206`  
**Risk:** Yüksek  
**Düzeltme:** 
1. Resume öncesinde HEAD isteği yapılarak mevcut `ETag` ve `Last-Modified` başlıkları önceki kayıtla karşılaştırılmalıdır.
2. HTTP GET isteklerine `If-Range: <ETag>` başlığı eklenmelidir.
3. Sunucu dosyanın değiştiğini bildirirse indirme durdurulmalı ve kullanıcıya "Sunucudaki dosya değişmiş. Baştan indirmek ister misiniz?" seçeneği sunulmalıdır.  
**Görev:** P2-03

---

## Soru 6: Hata Sınıflandırma ve Yeniden Deneme (Retry / Backoff / Stall)
**Bulgu:** 
- `SegmentWorker.cs:69-95`'te görev başına `_maxRetries = 5` döngüsü uygulanmaktadır.
- Üstel geri çekilme (`ExponentialBackoff`: $2^{attempt} \times 2$ sn, maks 60 sn) ve $0-2$ sn rastgele jitter mevcuttur (`SegmentWorker.cs:209-214`).
- Stall tespiti: `_stallTimeout = TimeSpan.FromSeconds(30)` ile `CancellationTokenSource.CancelAfter` kullanılmakta ve veri akışı kesilirse `TimeoutException` fırlatılmaktadır (`SegmentWorker.cs:146-156`).
- **Eksiklikler:**
  1. HTTP yanıt kodları sınıflandırılmamaktadır (`SegmentWorker.cs:116-120`). Başarısız olan her kod genel `HttpRequestException` fırlatmaktadır.
  2. 404 (Not Found) veya 410 (Gone) gibi kalıcı hatalarda bile 5 kez üstel gecikmeyle gereksiz deneme yapılmaktadır.
  3. 429 (Too Many Requests) yanıtlarında sunucunun `Retry-After` başlığı dikkate alınmamakta, parça sayısı düşürülmemektedir.
  4. 401/403 yetkilendirme hatalarında oturum yenileme veya kullanıcıya bilgi verme mantığı yoktur.

**Dosya/Satır:** `src/FDownloader.Core/Engine/Worker/SegmentWorker.cs#L67-L127`, `src/FDownloader.Core/Engine/Worker/SegmentWorker.cs#L209-L214`  
**Risk:** Orta  
**Düzeltme:** 
1. Hatalar kalıcı (4xx serisi; 404, 403, 410) ve geçici (5xx serisi, 429, timeout, network dropped) olarak ikiye ayrılmalıdır. Kalıcı hatalarda deneme derhal sonlandırılmalıdır.
2. 429 yanıtında `Retry-After` başlığına uyulmalı ve aktif worker sayısı azaltılmalıdır.  
**Görev:** P2-05

---

## Soru 7: Arşiv Çıkarma Güvenliği (Zip-Slip, Zip Bomb, Parola)
**Bulgu:** 
- **Zip-Slip Güvenlik Açığı (Partial Path Traversal Bypass):**
  `ArchiveExtractorService.ExtractCore` metodunda (satır 388-395) yer alan denetim:
  ```csharp
  var destinationPath = Path.GetFullPath(Path.Combine(finalDirectory, relativePath));
  if (!destinationPath.StartsWith(Path.GetFullPath(finalDirectory), StringComparison.OrdinalIgnoreCase))
  {
      _logger?.LogWarning("Zip-Slip koruması: Şüpheli arşiv dosyası engellendi: {Key}", entry.Key);
      continue;
  }
  ```
  Burada `Path.GetFullPath(finalDirectory)` ifadesinin sonuna `Path.DirectorySeparatorChar` EKLENMEMİŞTİR. Eğer `finalDirectory` `"C:\Target"` ise ve arşiv içindeki yol `"../TargetEvil/malware.exe"` ise, `destinationPath` `"C:\TargetEvil\malware.exe"` olur ve `StartsWith("C:\Target")` koşulunu geçer! Bu açık CWE-22 (Partial Path Traversal) olarak bilinir.
  Ayrıca 7-Zip ile çıkarma yapılan `ExtractWith7z` metodunda (`ArchiveExtractorService.cs:286-295`) doğrudan `7z.exe x archive.zip -o<dir> -y` çalıştırılmakta, uygulama seviyesinde yol denetimi yapılmamaktadır.
- **Arşiv Bombası (Zip Bomb / Decompression Bomb):**
  Açılan toplam dosya boyutu, dosya sayısı veya sıkıştırma oranı için **HİÇBİR SINIR YOKTUR**. Küçük bir arşiv dosyası açılarak yüzlerce gigabayt veri üretebilir ve diski tamamen doldurabilir (DoS).
- **Parola Sızıntısı:**
  `ExtractWith7z` metodunda parola doğrudan komut satırı argümanı olarak verilmektedir: `psi.ArgumentList.Add($"-p{password}")` (`ArchiveExtractorService.cs:293`). Windows üzerinde çalışan herhangi bir süreç (`Process Explorer`, WMI, ETW) komut satırını inceleyerek arşivi açma parolasını düz metin olarak ele geçirebilir.

**Dosya/Satır:** `src/FDownloader.Core/Utilities/ArchiveExtractorService.cs#L286-L295`, `src/FDownloader.Core/Utilities/ArchiveExtractorService.cs#L388-L395`  
**Risk:** Yüksek  
**Düzeltme:** 
1. Zip-Slip doğrulaması güvenli hale getirilmelidir: Hedef dizin sonuna `Path.DirectorySeparatorChar` eklenmeli veya `Path.GetRelativePath` kullanılarak çıktının `..` ile başlayıp başlamadığı denetlenmelidir.
2. Arşiv bombası koruması eklenmeli: Maksimum toplam açılmış boyut (örn. 25 GB), maksimum dosya sayısı (örn. 5.000) ve sıkıştırma oranı (100:1 üstü) denetlenerek aşılması halinde işlem iptal edilmelidir.
3. 7-Zip çağrılırken parola argümanda değil, güvenli bir geçici yapılandırma veya stdin üzerinden iletilmelidir.  
**Görev:** P3-02

---

## Soru 8: Hassas Verilerin Saklanması (Çerezler, Şifreler, Proxy)
**Bulgu:** 
- **Oturum Çerezleri (Cookies):** Tarayıcı eklentisi tarafından web sitelerinden toplanan oturum çerezleri (`background.js:229-256`), yerel köprüye gönderilmekte ve SQLite veritabanındaki `DownloadTasks` tablosunda `Cookies TEXT` kolonunda **TAMAMEN ŞİFRESİZ VE DÜZ METİN (PLAIN TEXT)** olarak saklanmaktadır (`Database.cs:106`, `DownloadRepository.cs:32, 220, 261`). Kullanıcının oturum açtığı platformlara ait session çerezleri `tasks.db` dosyasına doğrudan açıkça yazılmaktadır.
- **Proxy Bilgileri:** `AppSettings.cs:81` dosyasında `ProxyUsername` düz metin saklanmaktadır.
- **Veritabanı Dosyası:** SQLite veritabanı şifrelenmemiştir (SQLCipher veya DPAPI kullanılmamaktadır). Dosyaya yerel sistemdeki herhangi bir standart kullanıcı okuma erişimi sağlayabilir.

**Dosya/Satır:** `src/FDownloader.Core/Storage/Database.cs#L106`, `src/FDownloader.Core/Storage/DownloadRepository.cs#L32, L220, L261`, `src/FDownloader.Core/Config/AppSettings.cs#L79-L81`  
**Risk:** Yüksek  
**Düzeltme:** 
1. Veritabanına kaydedilen `Cookies` ve proxy kimlik bilgileri, Windows Data Protection API (`ProtectedData.Protect` - DPAPI) kullanılarak şifrelenmeli; sadece gerektiğinde bellekte çözülmelidir.
2. İndirme işlemi bittiğinde veya iptal edildiğinde görev çerezi veritabanından silinmelidir.
3. `tasks.db` dosyasının ACL erişim izinleri yalnızca mevcut Windows kullanıcısına (`CurrentUser`) kısıtlanmalıdır.  
**Görev:** P3-04

---

## Soru 9: TLS Sertifika Doğrulaması
**Bulgu:** 
Uygulamanın kaynak kodunda (`App.axaml.cs`, `TorrentService.cs`, `DownloadProber.cs`, `HttpDownloader.cs`, `UpdateService.cs`), TLS sertifika doğrulamasını kapatan veya baypas eden (`ServerCertificateCustomValidationCallback = DangerousAcceptAnyServerCertificateValidator`, `CheckValidationResult = true` veya benzeri) **HİÇBİR KOD SATIRI BULUNMAMAKTADIR**.
- Varsayılan .NET 9 TLS/X.509 zincir doğrulaması aktiftir ve işletim sistemi sertifika deposunu doğrulamaktadır.
- Güvenlik açığı bulunmamaktadır.

**Dosya/Satır:** `src/FDownloader.App/App.axaml.cs#L168-L180`, `src/FDownloader.Core/Torrent/TorrentService.cs#L285-L290`  
**Risk:** Düşük (Güvenli)  
**Düzeltme:** Düzeltme gerekmemektedir. Mevcut güvenli yapı korunmalıdır.  
**Görev:** P3-05

---

## Soru 10: Yönlendirme (Redirect) ve Çerez/Kimlik Sızıntısı
**Bulgu:** 
- Yönlendirme sayısı: `App.axaml.cs:173` içinde `SocketsHttpHandler.MaxAutomaticRedirections = 10` ile sınırlandırılmıştır. Ancak `TorrentService.cs:287` ve `875` içinde `HttpClientHandler` için yönlendirme üst sınırı belirtilmemiştir (varsayılan 50).
- **Çapraz Alan Adı Çerez Sızıntısı (Cross-Domain Cookie Leakage):**
  - `SegmentWorker.cs:194`, `DownloadProber.cs:208` ve `TorrentService.cs:300` dosyalarında istek başlıklarına manuel olarak `Cookie` ve `Referer` başlıkları eklenmektedir:
    `request.Headers.TryAddWithoutValidation("Cookie", _cookies);`
  - .NET `HttpClient` otomatik yönlendirmeyi (`AllowAutoRedirect = true`) takip ederken, manuel eklenen `Cookie` başlığını yönlendirilen üçüncü taraf etki alanına da (cross-domain redirect) aynen iletir.
  - Örnek: `https://guvenli-site.com/indir` linki `https://saldirgan-site.com/dosya.zip` adresine yönlendirme yaptığında, `guvenli-site.com`'a ait hassas oturum çerezi saldırganın sunucusuna sızmaktadır.
- HTTPS'den HTTP'ye düşürme (downgrade) koruması bulunmamaktadır.

**Dosya/Satır:** `src/FDownloader.App/App.axaml.cs#L170-L175`, `src/FDownloader.Core/Engine/Probe/DownloadProber.cs#L207-L209`, `src/FDownloader.Core/Engine/Worker/SegmentWorker.cs#L193-L195`, `src/FDownloader.Core/Torrent/TorrentService.cs#L285-L301`  
**Risk:** Yüksek  
**Düzeltme:** 
1. Otomatik yönlendirme (`AllowAutoRedirect`) kapatılmalı veya özel bir `DelegatingHandler` yazılarak yönlendirme adımları kontrol edilmelidir.
2. Yönlendirme hedefi orijinal alan adından farklı bir alan adı (domain) ise `Cookie` ve `Authorization` başlıkları istekten temizlenmelidir.
3. HTTPS kaynağından HTTP hedefine yapılan yönlendirmeler engellenmelidir.  
**Görev:** P3-05

---

## Soru 11: Güncelleme Paketlerinin Güvenliği (İmza ve Doğrulama)
**Bulgu:** 
Güncelleme sistemi Velopack kütüphanesi ile sağlanmaktadır (`UpdateService.cs:22-27`).
- Paketler GitHub Releases üzerinden HTTPS ile çekilmektedir (`GithubSource("https://github.com/erbayfaruk61/FDownloader", ...)`).
- Velopack kendi içinde release manifestosundaki SHA-256 hash bütünlüğünü denetlemektedir.
- **Eksiklik:**
  - Dağıtım iş akışında (`.github/workflows/release.yml:68-75`), `vpk pack` adımı çalıştırılırken Authenticode kod imzalama parametreleri (`--signParams`, `--signTemplate`) kullanılmamaktadır.
  - Uygulama yükleyicisi (`Setup.exe`) ve paketler dijital olarak İMZALANMAMAKTADIR.
  - Windows SmartScreen bilinmeyen yayımcı uyarısı vermektedir. GitHub deposu veya yayın belirteci ele geçirilirse, kullanıcılara imzasız zararlı güncelleme dağıtılabilir.

**Dosya/Satır:** `src/FDownloader.App/Services/UpdateService.cs#L25-L27`, `.github/workflows/release.yml#L68-L75`  
**Risk:** Orta  
**Düzeltme:** 
1. CI/CD iş akışına Authenticode / Azure Trusted Signing adımı entegre edilmeli ve `vpk pack` ile ikili dosyalar imzalanmalıdır.
2. Velopack güncelleme akışında ikincil bir açık anahtar (public key) doğrulama adımı tanımlanmalıdır.  
**Görev:** P3-08

---

## Soru 12: Loglara Hassas Veri Yazımı
**Bulgu:** 
- Loglama yapılandırmasında (`App.axaml.cs:162-165`) hassas veri maskeleme veya filtreleme (redaction) mekanizması bulunmamaktadır.
- Loglanan hassas içerikler:
  - `BrowserBridgeService.cs:216`: `_logger.LogInformation("Tarayıcıdan indirme yakalandı: {Url} (Dosya: {FileName})", ...);`
  - `DownloadProber.cs:36`: `_logger.LogInformation("Probe başladı: {Url}", url);`
  - `TorrentService.cs:284`: `_logger.LogInformation("Uzak .torrent dosyası indiriliyor: {Url}", cleanSource);`
  - İndirme bağlantılarının sorgu parametrelerinde (query string) oturum anahtarları, JWT belirteçleri ve AWS S3 presigned signature değerleri yer alabilmektedir. Bunlar doğrudan loglara yazılmaktadır.
- Hata Raporlama ve Sentry:
  - `Program.cs:43-80`'de `crash.log` dosyasına ve `SentrySdk.CaptureException(ex)` ile Sentry servisine istisna mesajları ve çağrı yığınları (stack trace) filtrelenmeden gönderilmektedir. Bu metinler yerel dosya yollarını (kullanıcı adlarını) ve istek URL'lerini açığa çıkarmaktadır.

**Dosya/Satır:** `src/FDownloader.Core/Network/BrowserBridgeService.cs#L216`, `src/FDownloader.Core/Engine/Probe/DownloadProber.cs#L36`, `src/FDownloader.App/Program.cs#L43-L80`  
**Risk:** Orta  
**Düzeltme:** 
1. URL'lerdeki hassas sorgu parametrelerini (`token`, `auth`, `signature`, `key`, `password`) `***` şeklinde maskeleyen bir log zenginleştirici (enricher/sanitizer) yazılmalıdır.
2. Sentry için `BeforeSend` filtresi eklenerek kullanıcıya ait kişisel yollar (`C:\Users\<user>`) ve URL parametreleri temizlenmelidir.  
**Görev:** P1-05 (ve P3-09)

---

## Soru 13: İndirilen Dosyalara MOTW (Zone.Identifier) Eklenmesi
**Bulgu:** 
- İndirilen dosyalara Windows NTFS Zone Identifier (`Mark of the Web - MOTW`, `Zone.Identifier: [ZoneTransfer] ZoneId=3`) **HİÇBİR ŞEKİLDE EKLENMEMEKTEDİR**.
- `FileWriter.cs:119`'da dosya yalnızca `.fdown` uzantısından nihai adına taşınmaktadır.
- **Tehlikeli Dosya Çalıştırma Davranışı:**
  - `MainViewModel.cs:3927` ve `ToastNotificationWindow.axaml.cs:168`:
    `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });`
  - Kullanıcı listedeki indirmeye veya gelen toast bildirimine tıkladığında dosya doğrudan Windows kabuğu aracılığıyla yürütülmektedir.
  - Çalıştırılabilir uzantılar (`.exe`, `.bat`, `.cmd`, `.scr`, `.vbs`, `.js`, `.ps1`, `.msi`) için hiçbir uzantı denetimi veya onay sorusu bulunmamaktadır.
  - MOTW işareti de bulunmadığı için Windows SmartScreen uyarısı dahi tetiklenmeden zararlı dosyalar tek tıklamayla doğrudan çalıştırılabilmektedir.

**Dosya/Satır:** `src/FDownloader.Core/Engine/Writer/FileWriter.cs#L119`, `src/FDownloader.App/ViewModels/MainViewModel.cs#L3927`, `src/FDownloader.App/Views/ToastNotificationWindow.axaml.cs#L168`  
**Risk:** Yüksek  
**Düzeltme:** 
1. İndirme tamamlandığında dosyanın alternatif veri akışına (ADS) `Zone.Identifier` işareti (`[ZoneTransfer]\nZoneId=3`) yazılmalıdır.
2. Potansiyel olarak tehlikeli uzantılı dosyalar arayüzden veya bildirimden açılmak istendiğinde kullanıcıya belirgin bir güvenlik uyarı penceresi ("Bu bir çalıştırılabilir programdır. Güvenliğinden emin misiniz?") gösterilmelidir.  
**Görev:** P3-06

---

## Soru 14: Dış Süreçlerde Komut ve Argüman Enjeksiyonu Riski
**Bulgu:** 
- **KRİTİK GÜVENLİK AÇIĞI (Command / CLI Option Injection — RCE):**
  - `YtDlpMediaService.cs:288-297` ve `418-429` satırlarında yt-dlp çağrısı yapılırken komut satırı argümanları güvenli `ProcessStartInfo.ArgumentList` koleksiyonu yerine ham dizgi birleştirme (`Arguments = $"..."`) ile oluşturulmaktadır:
    ```csharp
    Arguments = $"{commonArgs} --dump-single-json --no-warnings --no-playlist \"{url}\""
    ```
    ```csharp
    args = $"{commonArgs} {playlistFlag} {ffmpegFlag} {DefaultVideoFormat} --newline -o \"{outputTemplate}\" \"{url}\"";
    var psi = new ProcessStartInfo { FileName = binary, Arguments = args, ... };
    ```
  - `url` değişkeni üzerinde şema denetimi yapılmamaktadır (örn: `http://` veya `https://` doğrulanmamaktadır).
  - Eğer kullanıcı veya kötü niyetli bir web sayfası (Soru 1'deki korumasız köprü üzerinden) tırnak işareti içeren bir URL gönderirse (örn: `https://youtube.com/watch?v=123" --exec "calc.exe" --foo="`):
    Tırnak işareti URL parametresini kapatır ve yt-dlp'nin `--exec` parametresi devreye girerek hedef sistemde keyfi komut çalıştırır (**Remote Code Execution - RCE**)!
  - Bu durum, Soru 1'deki kimlik doğrulamasız yerel HTTP dinleyicisi ile birleştiğinde **Drive-by RCE** saldırı zinciri oluşturur: Kullanıcı tarayıcıda kötü niyetli bir siteye girdiğinde, site arka planda yerel porta istek atarak kullanıcının bilgisayarında yt-dlp aracılığıyla kod çalıştırabilir!
- **İkili Dosya İndirme Güvenliği:**
  - `YtDlpMediaService.cs:211-216`'da `yt-dlp.exe` dosyası GitHub'dan indirilirken SHA-256 hash doğrulaması yapılmamaktadır.

**Dosya/Satır:** `src/FDownloader.Core/Video/YtDlpMediaService.cs#L288-L297`, `src/FDownloader.Core/Video/YtDlpMediaService.cs#L418-L435`, `src/FDownloader.Core/Video/YtDlpMediaService.cs#L211-L216`  
**Risk:** Kritik  
**Düzeltme:** 
1. `ProcessStartInfo.Arguments` dizgi birleştirme tamamen kaldırılmalı; `ProcessStartInfo.ArgumentList.Add(...)` API'si kullanılmalıdır.
2. URL girdileri katı şekilde doğrulanmalı: Sadece `http` ve `https` şemaları kabul edilmeli; tire (`--`) ile başlayan girdiler reddedilmeli; argüman ayrımından önce `--` sonlandırıcı parametresi eklenmelidir.
3. Otomatik indirilen `yt-dlp.exe` ikili dosyasının SHA-256 hash bütünlüğü doğrulanmalıdır.  
**Görev:** P3-03

---

## Soru 15: Torrent Güvenliği, Dizin Taşması ve UPnP Durumu
**Bulgu:** 
- **UPnP / Port Yönlendirme:**
  - `TorrentSettings.cs:15-18`: `ListeningPort = 55123` ve `EnableUpnp = true` olarak yapılandırılmıştır.
  - `TorrentService.cs:82`: `AllowPortForwarding = settings.EnableUpnp` ile UPnP **VARSAYILAN OLARAK AKTİF** edilmiştir.
  - Kullanıcının açık izni veya bilgilendirmesi olmadan ev/işyeri modeminde 55123 portu dış dünyaya açılmaktadır.
- **Dizin Taşması (Path Traversal):**
  - Torrent eklenirken `_engine.AddAsync(torrent, saveDirectory)` çağrılmaktadır (`TorrentService.cs:230, 270, 318`). MonoTorrent kendi içinde temel yol temizliği yapsa da, FDownloader tarafında torrent içindeki dosya yollarının `saveDirectory` dışına taşmadığını denetleyen bağımsız bir ön kontrol yapılmamaktadır.
- **VPN Kill-Switch Kapsamı:**
  - `TorrentService.cs:90-106`'da BitTorrent P2P trafiği seçilen ağ adaptörüne kilitlenmektedir. Ancak uzak `.torrent` dosyasını HTTP üzerinden çeken `HttpClient` (`TorrentService.cs:285-303`) ve uygulamanın genel indirme motoru adaptör kilidine tabi DEĞİLDİR. VPN bağlantısı koptuğunda HTTP istekleri gerçek ağ arayüzünden sızabilir.

**Dosya/Satır:** `src/FDownloader.Core/Torrent/TorrentSettings.cs#L14-L18`, `src/FDownloader.Core/Torrent/TorrentService.cs#L80-L106`, `src/FDownloader.Core/Torrent/TorrentService.cs#L285-L303`  
**Risk:** Orta  
**Düzeltme:** 
1. UPnP varsayılan olarak kapalı (false) gelmeli; kullanıcı ayarlarından açılabilmeli ve riskleri belirtilmelidir.
2. Torrent meta verisi yüklendiğinde dosya yolları denetlenmeli; `..` veya mutlak yol içeren girdiler engellenmelidir.
3. VPN kill-switch seçeneği tüm ağ motorunu (`SocketsHttpHandler.ConnectCallback`) kapsayacak şekilde genel seviyeye çıkarılmalıdır.  
**Görev:** P3-07

---

## Özet Tablo

| # | Konu | Risk Seviyesi | İlgili Görev |
|---|---|---|---|
| 1 | Eklenti ↔ Uygulama İletişim Kanalı (Kimlik doğrulama yok, CORS wildcard, Origin denetimsiz) | **Kritik** | P3-01 |
| 2 | Sunucudan Gelen Dosya Adı Temizleme (Rezerve ad çoklu uzantı bypass, negatif indeks çökme) | **Orta** | P2-09 |
| 3 | Resume Bilgisi ve Checkpoint Güvenilirliği (Periyodik checkpoint yok, Flush yapılmıyor) | **Yüksek** | P2-02 |
| 4 | Dosya Yazımı ve Birleştirme Mekanizması (Tek dosya preallocation + offset, atomic rename) | **Düşük** | P2-08 |
| 5 | Sunucu Dosyası Değişimi (Resume öncesi ETag/Last-Modified/If-Range denetimi yok) | **Yüksek** | P2-03 |
| 6 | Hata Sınıflandırma ve Yeniden Deneme (HTTP durum kodu ayrımı yok, 404'te gereksiz deneme) | **Orta** | P2-05 |
| 7 | Arşiv Çıkarma Güvenliği (Zip-Slip partial path traversal açığı, zip bomb koruması yok) | **Yüksek** | P3-02 |
| 8 | Hassas Verilerin Saklanması (Oturum çerezleri SQLite'ta düz metin, şifreleme yok) | **Yüksek** | P3-04 |
| 9 | TLS Sertifika Doğrulaması (Kapatılmamış, varsayılan .NET 9 doğrulaması aktif) | **Düşük (Güvenli)** | P3-05 |
| 10 | Yönlendirme ve Çerez/Kimlik Sızıntısı (Cross-domain yönlendirmede Cookie sızıntısı) | **Yüksek** | P3-05 |
| 11 | Güncelleme Paketlerinin Güvenliği (Velopack HTTPS/hash var, Authenticode imza yok) | **Orta** | P3-08 |
| 12 | Loglara Hassas Veri Yazımı (URL token'ları ve PII maskelenmiyor, Sentry filtresiz) | **Orta** | P1-05 / P3-09 |
| 13 | İndirilen Dosyalara MOTW / Zone.Identifier (MOTW yok, tehlikeli uzantı çalıştırma uyarısı yok) | **Yüksek** | P3-06 |
| 14 | Dış Süreçlerde Komut ve Argüman Enjeksiyonu (yt-dlp string interpolation ile RCE riski) | **Kritik** | P3-03 |
| 15 | Torrent Güvenliği, Dizin Taşması ve UPnP (UPnP varsayılan açık, HTTP kill-switch sızıntısı) | **Orta** | P3-07 |

---

## Kritik/Yüksek Bulgular Listesi (Öncelikli Sıralama)

### 🔴 1. Soru 14 & Soru 1 Birlikteliği — Drive-by Uzaktan Kod Çalıştırma (RCE) Zinciri [KRİTİK]
- **Dosyalar:** `BrowserBridgeService.cs:107-110, 218`, `YtDlpMediaService.cs:288-297, 418-429`
- **Etki:** `BrowserBridgeService` yerel HTTP sunucusunda kimlik doğrulama bulunmaması ve CORS `Access-Control-Allow-Origin: *` başlığının bulunması, herhangi bir web sayfasının yerel porta POST isteği atabilmesine imkan verir. `YtDlpMediaService` ise URL parametresini `Arguments = $"... \"{url}\""` şeklinde çift tırnak arasına doğrudan iliştirmektedir. Kötü niyetli bir web sitesi tırnak karakteri içeren bir URL gönderdiğinde (`" --exec "powershell.exe -c ..." "`), yt-dlp parametre enjeksiyonu üzerinden hedef kullanıcının bilgisayarında tek bir web sayfası ziyaretiyle tam yetkili kod çalıştırılabilir.
- **Çözüm:** `YtDlpMediaService`'te `ProcessStartInfo.ArgumentList` kullanılmalı ve URL doğrulanmalı; `BrowserBridgeService`'te CORS `*` kaldırılıp gizli token doğrulaması getirilmelidir.
- **Görevler:** P3-01, P3-03

### 🔴 2. Soru 8 — Oturum Çerezlerinin (Cookies) Veritabanında Düz Metin Saklanması [YÜKSEK]
- **Dosyalar:** `Database.cs:106`, `DownloadRepository.cs:32, 220, 261`
- **Etki:** Tarayıcı eklentisinden aktarılan hassas oturum çerezleri `tasks.db` SQLite veritabanına tamamen şifresiz düz metin olarak kaydedilmektedir. Sisteme erişen herhangi bir üçüncü parti yazılım kullanıcının hesap oturumlarını çalabilir.
- **Çözüm:** Çerezler DPAPI ile şifrelenmeli ve indirme tamamlandığında silinmelidir.
- **Görev:** P3-04

### 🔴 3. Soru 7 — Arşiv Çıkarmada Zip-Slip Bypass ve Zip Bomb Korumasızlığı [YÜKSEK]
- **Dosyalar:** `ArchiveExtractorService.cs:388-395`
- **Etki:** `destinationPath.StartsWith(finalDirectory)` kontrolünde dizin ayracı (`/` veya `\`) bulunmadığı için kısmi yol eşleşmesiyle hedef klasörün kardeş dizinlerine dosya yazılabilmektedir (Partial Path Traversal). Ayrıca arşiv bombası koruması olmadığı için disk kolaylıkla doldurulabilir.
- **Çözüm:** `Path.GetRelativePath` ile dizin sınır kontrolü yapılmalı; açılmış boyut ve dosya sayısı üst sınırı konulmalıdır.
- **Görev:** P3-02

### 🔴 4. Soru 10 — Otomatik Yönlendirmelerde Çerez Sızıntısı (Cross-Domain Leak) [YÜKSEK]
- **Dosyalar:** `SegmentWorker.cs:194`, `DownloadProber.cs:208`, `TorrentService.cs:300`
- **Etki:** .NET `HttpClient` isteklerine manuel eklenen `Cookie` başlığı, 302 yönlendirmesiyle farklı bir alan adına gidildiğinde otomatik olarak aktarılmaktadır.
- **Çözüm:** Alan adı değiştiğinde `Cookie` başlığı temizlenmelidir.
- **Görev:** P3-05

### 🔴 5. Soru 5 — Resume Öncesi Dosya Değişim Kontrolünün Bulunmaması [YÜKSEK]
- **Dosyalar:** `HttpDownloader.cs:133`, `SegmentWorker.cs:185-206`
- **Etki:** Duraklatılmış bir indirme devam ettirilirken sunucudaki dosyanın değişip değişmediği kontrol edilmemektedir. `If-Range` kullanılmadığı için sunucu dosyası güncellenmişse eski parçaların üzerine yeni dosya yazılmakta ve dosya bozulmaktadır.
- **Çözüm:** Resume öncesi HEAD ile ETag/Last-Modified doğrulaması yapılmalıdır.
- **Görev:** P2-03

### 🔴 6. Soru 13 — MOTW (Zone.Identifier) Bulunmaması ve Denetimsiz Yürütme [YÜKSEK]
- **Dosyalar:** `FileWriter.cs:119`, `MainViewModel.cs:3927`, `ToastNotificationWindow.axaml.cs:168`
- **Etki:** İndirilen dosyalara "internetten indirildi" işareti eklenmemekte ve arayüzden tıklanan `.exe`, `.bat` gibi çalıştırılabilir dosyalar hiçbir uyarı verilmeksizin doğrudan çalıştırılmaktadır.
- **Çözüm:** Tamamlanan dosyalara Zone.Identifier eklenmeli ve çalıştırılabilir dosyalarda onay diyalogu gösterilmelidir.
- **Görev:** P3-06

### 🔴 7. Soru 3 — Periyodik Checkpoint Olmaması ve Veri Kaybı Riski [YÜKSEK]
- **Dosyalar:** `HttpDownloader.cs:86-89, 261-264`, `FileWriter.cs:33-40, 87-91`
- **Etki:** İndirme esnasında periyodik checkpoint yapılmamakta; `FlushAsync` çağrılmamaktadır. Ani çökme durumunda indirilen veriler kaybedilmektedir.
- **Çözüm:** Zaman ve boyut bazlı periyodik checkpoint ve disk flush mekanizması eklenmelidir.
- **Görev:** P2-02
