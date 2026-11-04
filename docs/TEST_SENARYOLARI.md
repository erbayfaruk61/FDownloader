# Test Senaryoları ve Doğrulama Kılavuzu

Bu belge, FDownloader indirme motoru ve disk/dosya sistemi için hem otomatik hem de elle yürütülebilir doğrulama senaryolarını içerir.

---

## 1. Disk ve Dosya Sistemi Senaryoları (P2-08)

### 1.1 Önceden Yer Ayırma (Preallocation)
- **Amaç:** Çok parçalı indirmelerde disk parçalanmasını (fragmentation) önlemek ve boş alan yetersizliğini indirme başında tespit etmek.
- **Otomatik Test:** `FileWriter_Initialize_PreallocatesSpecifiedSize` (5 MB dosya için `SetLength` doğrulaması).
- **Elle Test:**
  1. 1 GB'lık bir indirme başlatın.
  2. İndirme başlar başlamaz `.fdown` uzantılı dosyanın disk boyutunu özelliklerden inceleyin.
  3. Boyutun anında 1 GB'a genişletildiğini doğrulayın.

### 1.2 FAT32 4 GB Sınırı
- **Amaç:** FAT32 dosya sistemlerinde 4 GB üzeri dosyaların bozulmasını engellemek.
- **Kural:** 4 GB - 1 byte (4.294.967.295 byte) üzerindeki indirmeler derhal engellenmeli ve kullanıcı uyarılmalıdır.
- **Otomatik Test:** `DiskSpaceChecker_Fat32Limit_HandlesSizeThresholds`.
- **Elle Test:**
  1. FAT32 biçimli bir USB bellek takın veya hedef klasör olarak seçin.
  2. 4.5 GB'lık bir video/arşiv indirmesi başlatın.
  3. Uygulamanın indirmeyi başlatmadan önce net bir FAT32 dosya sistemi uyarısı verdiğini doğrulayın.

### 1.3 Atomik Taşıma (.fdown -> Nihai Dosya Adı)
- **Amaç:** İndirme tamamlanmadan dosyanın başka uygulamalar tarafından eksik okunmasını engellemek.
- **Otomatik Test:** `FileWriter_CommitAsync_AtomicallyRenamesTempToFinal`.
- **Elle Test:**
  1. İndirme sürerken hedef klasörde yalnızca `.fdown` uzantılı dosyanın olduğunu görün.
  2. İndirme %100 olduğunda dosyanın atomik olarak orijinal adına dönüştüğünü ve `.fdown` dosyasının kalmadığını doğrulayın.

### 1.4 Hedef Klasörün Silinmesi / Sürücünün Çıkarılması
- **Amaç:** İndirme sırasında harici disk çıkarılırsa veya hedef klasör silinirse motorun çökmeyip güvenli duraklatılması.
- **Otomatik Test:** `FileWriter_CommitAsync_WhenDestinationDirectoryMissing_ThrowsDirectoryNotFound`.
- **Elle Test:**
  1. USB belleğe bir indirme başlatın.
  2. İndirme ortasında belleği çıkartın.
  3. Uygulamanın çökmeyip görevi duraklattığını (`Paused`) ve "Hedef klasör veya sürücü bulunamadı" uyarısı verdiğini doğrulayın.

---

## 2. Tarayıcı Eklentisi ve Yakalama Senaryoları (P4-02)

### 2.1 Uygulama Açıkken Otomatik Yakalama
- **Amaç:** Desteklenen uzantı veya MIME türündeki bir dosya indirilirken tarayıcının kendi indirmesini iptal edip tüm parametreleri (URL, dosya adı, çerez, Referer, User-Agent, boyut) FDownloader'a eksiksiz devretmesi.
- **Otomatik Test:** `IndirmeIstegiTumParametrelerleYakalanir`, `PingIstegiExtensionHeartbeatEventYayinlar`.
- **Elle Test:**
  1. FDownloader masaüstü uygulamasını çalıştırın.
  2. Tarayıcıda bir `.zip` veya `.iso` indirme bağlantısına tıklayın.
  3. Tarayıcının kendi indirme çubuğunda görevin anında temizlendiğini (iptal edildiğini) ve FDownloader üzerinde indirme onay/başlatma penceresinin açıldığını doğrulayın.

### 2.2 Uygulama Kapalıyken Kesintisiz İndirme
- **Amaç:** FDownloader kapalıyken veya ulaşılamazken kullanıcının hiçbir indirmeyi kaybetmemesi, tarayıcının standart indirme yöneticisinin normal devam etmesi.
- **Kural:** `isBridgeAlive()` yanıt vermezse `suggest()` çağrılır; tarayıcı indirmesi asla engellenmez.
- **Elle Test:**
  1. FDownloader masaüstü uygulamasını tamamen kapatın.
  2. Tarayıcıda herhangi bir dosya indirme bağlantısına tıklayın.
  3. Tarayıcının indirmeyi normal bir şekilde kendi içinde tamamladığını, hata veya iptal vermediğini doğrulayın.

### 2.3 Alt Tuşu ile Atlama (IDM Stili Baypas)
- **Amaç:** Kullanıcı belirli bir dosyayı FDownloader yerine doğrudan tarayıcı ile indirmek istediğinde tek tuşla geçiş yapabilmesi.
- **Kural:** `Alt` tuşuna basılı tutularak tıklandığında veya son 5 saniye içinde basılmışsa eklenti indirmeyi yakalamaz.
- **Elle Test:**
  1. Klavyede `Alt` tuşuna basılı tutun.
  2. Büyük bir arşiv (.rar/.zip) indirme linkine tıklayın.
  3. FDownloader'ın açılmadığını ve dosyanın doğrudan tarayıcı tarafından indirildiğini doğrulayın.

### 2.4 Hariç Tutulan Siteler (Excluded Domains)
- **Amaç:** Belirli sitelerden yapılan indirmelerin otomatik yakalama dışında tutulması (örn: iç ağ siteleri, GitHub vb.).
- **Elle Test:**
  1. Eklenti ayarlarına `github.com` domainini hariç tutulanlar arasına ekleyin.
  2. GitHub üzerinden bir release asset indirin.
  3. İndirmenin yakalanmadığını ve doğrudan tarayıcıda indiğini doğrulayın.

### 2.5 Minimum Boyut Eşiği (Min Size Threshold)
- **Amaç:** Küçük boyutlu dosyaların (örn. 500 KB altı) FDownloader'a aktarılmadan tarayıcıda hızlıca inmesi.
- **Elle Test:**
  1. Minimum boyutu 1 MB (1.048.576 byte) olarak ayarlayın.
  2. 200 KB boyutunda bir PDF veya küçük zip indirin.
  3. Dosyanın doğrudan tarayıcı tarafından indirildiğini doğrulayın.

### 2.6 HLS / DASH Akış Tespiti (P4-03)
- **Amaç:** Sayfada oynatılan `.m3u8` (HLS) veya `.mpd` (DASH) akış manifestlerinin sayfa kaynaklarından ve `<video>` etiketlerinden otomatik algılanması.
- **Otomatik Test:** `ErrorClassificationTests` ve `content.js:resolveVideoTarget`.
- **Elle Test:**
  1. HLS akışı oynatan bir video sayfasına gidin (örn. test HLS player).
  2. Video üzerine fareyi getirin; FDownloader video yakalama butonunun belirdiğini doğrulayın.
  3. Butona tıklandığında stream URL'sinin (`.m3u8` / `.mpd`) FDownloader'a aktarıldığını doğrulayın.

### 2.7 DRM Korumalı İçerik Tespiti ve Engellemesi (P4-03)
- **Amaç:** DRM (Widevine, PlayReady, FairPlay) ile şifrelenmiş içeriklerde indirme başlatılmaması; kullanıcının anlaşılır şekilde uyarılması.
- **Otomatik Test:** `ClassifyException_DrmProtectedMessage_ReturnsDrmProtectedKind`, `IsDrmProtectedMessage_IdentifiesDrmSignaturesCorrectly`.
- **Elle Test:**
  1. DRM korumalı bir video oynatıcı sayfasına girin (örn. Shaka Player DRM test sayfası).
  2. `<video>` üzerinde DRM algılandığında eklenti butonunun `🔒 Korumalı İçerik (DRM İndirilemez)` uyarısına geçtiğini doğrulayın.
  3. Butona tıklandığında indirme isteği atılmadığını, koruma uyarısı verildiğini doğrulayın.

---

## 3. Medya ve yt-dlp Senaryoları (P5-01)

### 3.1 yt-dlp Sürüm Güncelleme
- **Amaç:** Video siteleri sık sık algoritma değiştirdiğinden, yt-dlp binary'sinin uygulamadan tek tıkla veya arka planda en güncel sürüme yükseltilebilmesi.
- **Otomatik Test:** `YtDlpMediaService.GetInstalledVersionAsync`, `YtDlpMediaService.UpdateYtDlpAsync`.
- **Elle Test:**
  1. `tools/yt-dlp/yt-dlp.exe` dosyasını kontrol edin.
  2. Uygulama içerisinden "yt-dlp Güncelle" seçeneğini tetikleyin.
  3. Sürümün en son GitHub yayınına (`YYYY.MM.DD`) yükseldiğini doğrulayın.

### 3.2 İptal ve Yetim Süreç (Orphan Process) Temizliği
- **Amaç:** Medya indirmesi iptal edildiğinde veya uygulama kapatıldığında alt süreçlerin (`yt-dlp.exe` ve `ffmpeg.exe`) arkada çalışmaya devam etmesini (kaynak tüketimini) önlemek.
- **Otomatik Test:** `YtDlpMediaServiceTests.StopDownload_WhenTaskNotRunning_DoesNotThrow`, `YtDlpMediaServiceTests.Dispose_WhenCalledMultipleTimes_CleansUpSafely`.
- **Elle Test:**
  1. Uzun bir YouTube videosu indirmesi başlatın.
  2. Görev Yöneticisi'ni (Task Manager) açıp `yt-dlp.exe` ve `ffmpeg.exe` süreçlerinin çalıştığını görün.
  3. FDownloader üzerinden indirmeyi "İptal Et" (Cancel) butonuna basın.
  4. Görev Yöneticisi'nde `yt-dlp.exe` ve `ffmpeg.exe` süreçlerinin anında sonlandığını, yetim süreç kalmadığını doğrulayın.

### 3.3 Hata Çıktısı Anlaşılırlığı ve Format Seçimi
- **Amaç:** Video hatalarının (yaş kısıtlaması, özel video, bot doğrulaması, 403 vb.) anlaşılır Türkçe rehberlikle kullanıcıya iletilmesi ve çözünürlüklerin seçilebilmesi.
- **Otomatik Test:** `YtDlpMediaServiceTests.FormatTurkishErrorMessage_TranslatesCommonErrorsAccurately`, `MediaMetadata_Formats_CanBeAddedAndOrderedByHeight`.
- **Elle Test:**
  1. Yaş kısıtlamalı veya gizli bir video bağlantısı girin.
  2. Hatanın anlaşılır Türkçe mesaj olarak ("Bu video yaş kısıtlamalıdır...", "Video yayından kaldırılmış...") ekranda göründüğünü doğrulayın.

### 3.4 HLS (.m3u8) Paralel İndirme ve Sıralı Birleştirme (P5-02)
- **Amaç:** Master veya media playlist'ten parçaların paralel (eşzamanlı) indirilmesi ve byte sıralaması bozulmadan tek dosyada birleştirilmesi.
- **Otomatik Test:** `HlsStreamDownloaderTests.DownloadHlsAsync_DownloadsSegmentsInParallelAndMergesSequentially`.
- **Elle Test:**
  1. Bir `.m3u8` video akış bağlantısını FDownloader'a ekleyin.
  2. Parçaların eşzamanlı (paralel) indiğini ve ilerleme çubuğunun doğru ilerlediğini gözlemleyin.
  3. İndirme bittiğinde oluşturulan `.ts` veya `.mp4` dosyasını medya oynatıcıda (VLC vb.) açıp takılma olmadan baştan sona oynatılabildiğini doğrulayın.

### 3.5 HLS DRM Algılama ve Koruma (P5-02)
- **Amaç:** Şifreli/DRM korumalı HLS akışlarında (`#EXT-X-KEY` SAMPLE-AES vb.) indirme başlatılmayıp kullanıcının bilgilendirilmesi.
- **Otomatik Test:** `HlsStreamDownloaderTests.ParsePlaylistAsync_DrmProtectedStream_ThrowsDrmProtectedDownloadException`.
- **Elle Test:**
  1. Korumalı bir HLS akış URL'si girildiğinde uygulamanın `Bu HLS akışı DRM ile şifrelenmiştir ve indirilemez.` uyarısı verdiğini doğrulayın.

---

## 4. Torrent ve Eş Paylaşım Senaryoları (P5-03)

### 4.1 Dosya Önizleme ve Seçimi (File Selection & Priorities)
- **Amaç:** Çok dosyalı bir torrent indirilmeden önce içindeki dosyaların listelenmesi, istenmeyen dosyaların (`DoNotDownload`) devre dışı bırakılması veya önceliklerinin (`High`, `Normal`) ayarlanabilmesi.
- **Otomatik Test:** `TorrentServiceTests.InspectTorrentFilesAsync_FromLocalFile_ReturnsFileListCorrectly`, `TorrentServiceTests.FilePriorityManagement_AllowsSelectingAndIgnoringSpecificFiles`, `TorrentServiceTests.InspectTorrentFilesAsync_WithRemoteHttpTorrent_DownloadsAndListsFiles`.
- **Elle Test:**
  1. Çoklu dosya içeren bir `.torrent` dosyası veya magnet linki ekleyin.
  2. Dosya listesi ekranında bazı dosyaların işaretini kaldırın (`İndirme`), bir dosyayı ise `Yüksek Öncelik` yapın.
  3. İndirme bittiğinde işareti kaldırılan dosyaların diskte oluşturulmadığını veya indirilmediğini doğrulayın.

### 4.2 Sıralı İndirme (Sequential / Streaming Download)
- **Amaç:** Video dosyalarının parçalarının baştan sona sıralı indirilerek indirme bitmeden medya oynatıcıda (VLC vb.) izlenebilmesi.
- **Otomatik Test:** `TorrentServiceTests.ToggleSequentialDownloadAsync_TogglesSequentialStreamingState`.
- **Elle Test:**
  1. Büyük bir video torrent'i indirmesi başlatın.
  2. Görev üzerinde "Sıralı İndirme" (Streaming) seçeneğini aktif edin.
  3. Parça haritasında parçaların baştan sona doğru sıra takip ederek indiğini ve ilk %5 indikten sonra dosyanın medya oynatıcıda açılıp izlenebildiğini doğrulayın.

### 4.3 Görev Yaşam Döngüsü ve Yeniden Başlatma (Lifecycle & FastResume)
- **Amaç:** Torrent görevlerinin duraklatılması, devam ettirilmesi, uygulama kapatılıp açıldığında kaldığı yerden devam etmesi (`RestoreTorrentTaskAsync`) ve diskten zorla yeniden doğrulanması (`ForceRecheckAsync`).
- **Otomatik Test:** `TorrentServiceTests.PauseResumeAndRemoveLifecycle_TransitionsEventsProperly`, `TorrentServiceTests.RestoreTorrentTaskAsync_WhenCalled_RestoresManagerWithoutStartingUnlessRequested`, `TorrentServiceTests.ForceRecheckAsync_InitiatesHashCheckSuccessfully`.
- **Elle Test:**
  1. Aktif bir torrent indirmesi sırasında uygulamayı tamamen kapatın.
  2. Uygulamayı yeniden açın.
  3. Görevin diskteki dosyaları baştan indirmek yerine kaldığı yüzdeyi koruyarak sorunsuz devam ettiğini doğrulayın.
  4. Sağ tıklayıp "Zorla Yeniden Doğrula" seçtiğinizde hash kontrolü yapıldığını gözlemleyin.

### 4.4 Magnet Link ve Fallback İzleyiciler (Fallback Trackers & DHT)
- **Amaç:** Tracker içermeyen magnet linklerine yüksek hızlı eş bulma amacıyla varsayılan açık izleyicilerin (`DefaultFallbackTrackers`) otomatik eklenmesi ve motorun DHT/UPnP yapılandırması.
- **Otomatik Test:** `TorrentServiceTests.AddAndStartTorrentAsync_WithMagnetLink_EnrichesWithFallbackTrackers`, `TorrentServiceTests.AddTrackersAsync_And_ForceReannounce_AddCustomTrackers`.
- **Elle Test:**
  1. Yasal bir dağıtım magnet linki ekleyin (ör. Ubuntu ISO magnet linki).
  2. FDownloader'ın izleyici listesine otomatik olarak açık tracker havuzunu eklediğini ve eşlerle bağlantı kurduğunu doğrulayın.

---

## 5. Site ve Galeri Yakalayıcı Senaryoları (P5-04)

### 5.1 Tarama Derinliği ve Sayfa İçi Gezinme (Crawl Depth)
- **Amaç:** Yalnızca tek sayfayı (`MaxDepth = 1`) veya sayfadaki iç bağlantıları sırayla gezerek (`MaxDepth >= 2`) alt sayfalardaki görsel, video ve belgeleri derleyebilme.
- **Otomatik Test:** `SiteGrabberServiceTests.AnalyzePageAsync_Depth2_CrawlsInternalLinksAndCollectsNestedFiles`.
- **Elle Test:**
  1. Araçlar menüsünden Site Yakalayıcı'yı açın.
  2. Bir galeri URL'si girin ve tarama derinliğini 2 yapın.
  3. Alt sayfalardaki resim ve videoların da listeye eklendiğini, derinlik sütununda (Depth) 2 olarak belirtildiğini doğrulayın.

### 5.2 Aynı Alan Adı Sınırı (Same Domain Only)
- **Amaç:** Sayfa içindeki reklam, sayaç veya dış site linklerine sıçramayı engelleyerek yalnızca hedef alan adını taramak.
- **Otomatik Test:** `SiteGrabberService.IsSameDomain`, `SiteGrabberServiceTests.AnalyzePageAsync_SinglePage_ExtractsMediaFilesAndCategorizesCorrectly`.
- **Elle Test:**
  1. Dış sitelere linkler içeren bir sayfayı taratın.
  2. Yalnızca hedef domain'e ait medya dosyalarının listelendiğini doğrulayın.

### 5.3 robots.txt Nezaketi ve Hız Sınırı (Rate Limiting)
- **Amaç:** Sitenin `robots.txt` dosyasındaki `Disallow:` kurallarına uymak ve istekler arasına nezaket gecikmesi (`DelayBetweenRequestsMs`) koyarak sunucuyu boğmamak.
- **Otomatik Test:** `SiteGrabberServiceTests.AnalyzePageAsync_RespectsRobotsTxt_SkipsDisallowedPaths`, `SiteGrabberServiceTests.RobotsTxtRules_ParseAndValidatePaths`.
- **Elle Test:**
  1. `Disallow: /admin/` kuralı olan bir siteyi tarayın.
  2. Yasaklı klasör altındaki içeriklerin atlandığını doğrulayın.

### 5.4 Aşırı Büyük Sayfalarda Donmama ve İptal (Resilience & Cancellation)
- **Amaç:** Aşırı büyük HTML sayfalarında veya yavaş sunucularda arayüzün donmaması, kullanıcının istediği an taramayı iptal edebilmesi (`CancellationToken`).
- **Otomatik Test:** `SiteGrabberServiceTests.AnalyzePageAsync_Cancellation_AbortsExecutionImmediately`.
- **Elle Test:**
  1. Büyük bir web sayfasını taratırken "İptal Et" butonuna tıklayın.
  2. Taramayı yapan işlemin anında durduğunu ve arayüzün kilitlenmediğini doğrulayın.

### 5.5 Kategori Filtreleme ve Boyut Ön Sorgulama (Category Filter & Probe)
- **Amaç:** Sadece seçilen dosya türlerinin (Video, Resim, Arşiv vb.) listelenmesi ve HEAD istekleriyle dosyaların boyutlarının indirmeden önce öğrenilebilmesi.
- **Otomatik Test:** `SiteGrabberServiceTests.AnalyzePageAsync_CategoryFilter_FiltersOutUnwantedCategories`, `SiteGrabberServiceTests.ProbeFileSizesAsync_FetchesContentLengthAndTypeViaHeadRequests`.
- **Elle Test:**
  1. Sadece "Video" kategorisini seçerek bir web sayfasını tarayın.
  2. Yalnızca video dosyalarının listelendiğini ve dosya boyutlarının göründüğünü doğrulayın.

---

## 6. Proxy ve Kimlik Doğrulama Senaryoları (P5-05)

### 6.1 Sistem Proxy ve Özel Proxy (HTTP / HTTPS / SOCKS5)
- **Amaç:** Kullanıcının işletim sistemi proxy ayarlarını doğrudan kullanabilmesi veya özel HTTP/HTTPS/SOCKS5 proxy tanımlayarak trafiği bu vekil sunucu üzerinden yönlendirebilmesi.
- **Otomatik Test:** `NetworkProxyAndCredentialTests.ProxyConfig_CreateWebProxy_ReturnsExpectedProxyType`, `NetworkProxyAndCredentialTests.NetworkHandlerFactory_CreateHandler_ConfiguresProxyAndCredentials`.
- **Elle Test:**
  1. Ayarlar > Proxy sekmesine gidin.
  2. "Özel Proxy Kullan" seçip SOCKS5 veya HTTP proxy adres ve portunu girin.
  3. Bir dosya indirin ve ağ trafiğinin girilen vekil sunucu üzerinden aktığını doğrulayın.

### 6.2 Proxy Kimlik Doğrulaması (Kullanıcı Adı & Parola)
- **Amaç:** Parolalı vekil sunucularda kimlik bilgilerinin güvenli taşınması ve yetkilendirme başlıklarının doğru üretilmesi.
- **Otomatik Test:** `NetworkProxyAndCredentialTests.ProxyConfig_WithCredentials_ConfiguresNetworkCredential`.
- **Elle Test:**
  1. Kimlik doğrulaması gerektiren bir vekil sunucu adresi, kullanıcı adı ve parolası girin.
  2. İndirme başlatarak bağlantının 407 (Proxy Authentication Required) hatasına düşmeden başarıyla gerçekleştiğini test edin.

### 6.3 Site Bazlı Kimlik Doğrulama (Site Credentials & DPAPI)
- **Amaç:** Basic, Digest veya NTLM korumalı siteler için kullanıcı adı ve parolaların DPAPI ile yerel veritabanında şifreli saklanması, indirme sırasında sunucuya otomatik sunulması.
- **Otomatik Test:** `NetworkProxyAndCredentialTests.SiteCredentialManager_AddAndRetrieve_StoresAndFindsCorrectly`, `NetworkProxyAndCredentialTests.SiteCredentialManager_DpapiEncryption_StoresPasswordsEncryptedOnDisk`, `NetworkProxyAndCredentialTests.SiteCredentials_AuthenticatesAgainstProtectedServerSuccessfully`.
- **Elle Test:**
  1. Ayarlar > Site Şifreleri menüsünden `korumali-site.com` için kullanıcı adı ve parola ekleyin.
  2. Bu siteden bir dosya indirme bağlantısı verin.
  3. FDownloader'ın 401 yetkisiz hatası almadan indirmeyi sorunsuzca tamamladığını doğrulayın.
  4. SQLite veritabanındaki `site_credentials` tablosunu inceleyerek parolanın açık metin olarak DEĞİL, DPAPI ile şifrelenmiş olarak saklandığını teyit edin.

---

## 7. Toplu İndirme Senaryoları (P5-06)

### 7.1 Çok Satırlı Link Listesi ve Yorum Satırları
- **Amaç:** Panodan veya metin dosyasından yapıştırılan çoklu URL listesinin satır satır ayrıştırılması, `#` ile başlayan yorum satırlarının ve geçersiz satırların güvenle ayıklanması.
- **Otomatik Test:** `BatchDownloadTests.BatchDownloadParser_MultilineText_ParsesAndDeduplicates`.
- **Elle Test:**
  1. İndirme > Toplu İndirme penceresini açın.
  2. Metin kutusuna aralarda geçersiz satırlar ve `# yorum` satırları bulunan 5 adet URL yapıştırın.
  3. Yalnızca geçerli URL'lerin indirme listesine aktarıldığını doğrulayın.

### 7.2 Sayısal ve Alfabetik Aralık Desenleri ([01-20], [a-z])
- **Amaç:** Tek bir URL şablonu ile ardışık dosyaların ([01-20], [1-100:2], [a-z]) otomatik olarak genişletilerek toplu indirme kuyruğuna eklenmesi.
- **Otomatik Test:** `BatchDownloadTests.BatchRangePattern_NumericPadding_ExpandsCorrectly`, `BatchDownloadTests.BatchRangePattern_StepAndDescending_ExpandsCorrectly`, `BatchDownloadTests.BatchRangePattern_Alphabetic_ExpandsLetters`.
- **Elle Test:**
  1. Toplu indirme kutusuna `https://site.org/galeri/resim_[01-05].jpg` yazın.
  2. Listeye `resim_01.jpg`'den `resim_05.jpg`'ye kadar 5 adet görevin sıfır dolgusuyla eklendiğini doğrulayın.

### 7.3 Özel Dosya Adı Şablonları ({index}, {name})
- **Amaç:** Toplu indirmelerde dosyaların `{name}`, `{index:D2}`, `{ext}` gibi şablonlarla isimlendirilebilmesi.
- **Otomatik Test:** `BatchDownloadTests.BatchDownloadParser_WithCustomFileNamePattern_AppliesTemplate`, `BatchDownloadTests.DownloadManager_AddBatchDownloadsAsync_CreatesAndQueuesTasks`.
- **Elle Test:**
  1. Toplu indirmede dosya adı şablonu olarak `Bolum_{index:D2}_{name}.{ext}` belirtin.
  2. Oluşturulan görevlerin adlarının `Bolum_01_dosya.mp4`, `Bolum_02_dosya.mp4` şeklinde biçimlendirildiğini doğrulayın.

---

## 8. İlk Açılış Sihirbazı Senaryoları (P6-01)

### 8.1 İlk Kurulum Tespiti ve Karşılama Ekranı
- **Amaç:** Uygulama temiz bir sistemde ilk kez açıldığında kullanıcının doğrudan karmaşık indirme listesiyle baş başa kalmayıp, 3 adımlı kurulum sihirbazıyla karşılanması.
- **Elle Test:**
  1. SQLite veritabanındaki `App.FirstRunCompleted` ayarını temizleyin veya uygulamayı ilk kez başlatın.
  2. Açılışta doğrudan "FDownloader Kurulum Sihirbazı" modal penceresinin açıldığını ve 1. Adımda (Hoş Geldiniz & Tema Seçimi) olduğunu doğrulayın.
  3. "Okyanus Mavisi", "Siber Mor", "Platin Gri" veya "Sade & Aydınlık" temalarından birine tıklandığında uygulamanın renk temasının dinamik olarak değiştiğini görün.

### 8.2 Varsayılan Klasör ve Windows Başlangıç Tercihleri
- **Amaç:** Kullanıcının dosyaların nereye indirileceğini seçmesi, Windows açılışında otomatik başlama ve sistem tepsisine küçülme tercihlerini belirleyebilmesi.
- **Elle Test:**
  1. Sihirbazda "İleri" butonuna basarak 2. Adıma (Klasör & Başlangıç Tercihleri) geçin.
  2. "Gözat..." butonu ile özel bir indirme klasörü seçin (`D:\İndirmelerim`).
  3. "Windows açıldığında FDownloader'ı otomatik başlat" ve "Sistem tepsisinde küçült" onay kutularını işaretleyin.
  4. "Geri" butonuna basıldığında 1. Adıma dönülebildiğini, "İleri" ile tekrar 2. Adıma gelindiğinde yapılan seçimlerin korunduğunu doğrulayın.

### 8.3 Tarayıcı Eklentisi Rehberi ve Kurulumu Tamamlama
- **Amaç:** Kullanıcıya Chromium (Chrome/Edge/Brave/Opera) ve Firefox tarayıcı eklentisini nasıl yükleyeceğinin adım adım açıklanması, tek tıkla eklenti klasörünün açılması ve yolun panoya kopyalanması.
- **Elle Test:**
  1. 3. Adıma geçin (Tarayıcı Eklentisi Rehberi).
  2. "Eklenti Klasörünü Aç" butonuna tıklayın; Windows Gezgini'nde `tools/browser-extension` klasörünün açıldığını görün.
  3. "Klasör Yolunu Kopyala" butonuna tıklayın; panoya eklenti klasör yolunun kopyalandığını doğrulayın.
  4. "Tamamla" butonuna basın; sihirbazın kapandığını, hoş geldiniz bildiriminin çıktığını ve `App.FirstRunCompleted = True` olarak kaydedildiğini doğrulayın.
  5. Uygulamayı kapatıp tekrar açın; sihirbazın bir daha otomatik açılmadığını teyit edin.
