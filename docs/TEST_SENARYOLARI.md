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


