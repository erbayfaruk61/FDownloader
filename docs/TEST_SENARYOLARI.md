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

