# FDownloader ve Tarayıcı Eklentisi Gizlilik Politikası

> **Yürürlük Tarihi:** 4 Kasım 2026  
> **Kapsam:** FDownloader Masaüstü Uygulaması ve FDownloader Tarayıcı Entegrasyon Eklentisi  
> **İlgili Görev:** `P4-01` (Faz 4 — Tarayıcı Eklentisi ve Entegrasyon)

---

## 1. Giriş

FDownloader ("biz", "bizim" veya "uygulama"), kullanıcı gizliliğine ve veri güvenliğine en üst düzeyde önem verir. Bu Gizlilik Politikası, FDownloader masaüstü yazılımı ve resmi tarayıcı eklentimizin (Chrome, Edge, Firefox, Brave vb.) hangi bilgileri nasıl işlediğini, sakladığını ve koruduğunu açıklar.

**Temel İlkemiz:** FDownloader, kullanıcılarının kişisel verilerini, indirme geçmişini veya tarama alışkanlıklarını **asla toplamaz, dış sunuculara göndermez ve üçüncü taraflarla paylaşmaz.**

---

## 2. Toplanan ve İşlenen Veriler

### 2.1. İndirme Bağlantıları (URL)
- **Ne Yapılır:** Tarayıcıda tıkladığınız veya tarayıcının indirmeye çalıştığı dosya bağlantıları, dosyanın çok parçalı ve yüksek hızda indirilebilmesi için masaüstü FDownloader uygulamasına aktarılır.
- **Nerede İşlenir:** Bu aktarım tamamen kullanıcının kendi bilgisayarı içerisinde, döngüsel yerel ağ arabirimi (`http://127.0.0.1:9666`) üzerinden gerçekleşir.
- **Dışa Aktarım:** İndirme bağlantıları hiçbir zaman harici bir sunucuya veya üçüncü tarafa iletilmez.

### 2.2. Kimlik Doğrulama Çerezleri (Cookies)
- **Ne Yapılır:** Yalnızca kullanıcının oturum açtığı platformlardan (örn. Google Drive, bulut depolama siteleri vb.) dosya indirirken indirme yetkisinin doğrulanabilmesi için ilgili dosya alan adına ait oturum çerezleri yerel olarak okunur ve FDownloader'a iletilir.
- **Güvenlik:** FDownloader masaüstü uygulamasında çerezler veritabanına kaydedilirken Windows DPAPI (Data Protection API) veya AES-GCM ile şifrelenir (`ENC::v1::`). Düz metin olarak saklanmaz.

### 2.3. Kullanıcı Ajanı (User-Agent) ve Referer
- Bazı sunucular indirmeleri yetkilendirmek için Referer veya standart tarayıcı User-Agent bilgisi talep eder. Bu bilgiler yalnızca indirme isteği sırasında doğrudan dosya sunucusuna gönderilmek üzere yerel uygulamaya aktarılır.

---

## 3. Toplanmayan Veriler (Sıfır Veri İlkesi)

- ❌ **Genel Tarama Geçmişi:** Ziyaret ettiğiniz web siteleri izlenmez, kaydedilmez veya analiz edilmez.
- ❌ **Kişisel Kimlik Bilgileri (PII):** Adınız, e-posta adresiniz, telefon numaranız veya IP adresiniz toplanmaz.
- ❌ **Üçüncü Taraf Analitik/İzleyici:** Google Analytics, telemetry tracker veya reklam izleyicileri kodumuzda bulunmaz.
- ❌ **Dosya İçeriği:** İndirdiğiniz dosyaların içeriği incelenmez; dosyalar doğrudan dosya sunucusundan kullanıcının yerel diskine yazılır.

---

## 4. İzinlerin Kullanımı (Permissions)

Eklentimiz yalnızca işlevini yerine getirebilmek için zorunlu olan minimum izinleri talep eder:

1. **`downloads`:** Tarayıcı indirmesini yakalamak, devralınan görevi tarayıcı geçmişinde temizlemek ve çift indirmeyi önlemek için.
2. **`contextMenus`:** Sağ tık menüsüne "⚡ FDownloader ile İndir" seçeneğini eklemek için.
3. **`storage`:** Eklenti açık/kapalı durumu ve kullanıcı tercihlerini yerel tarayıcı profilinde saklamak için.
4. **`cookies`:** Oturum gerektiren sitelerden dosya indirilirken yetkilendirme çerezlerini yerel masaüstü uygulamasına aktarmak için.
5. **`host_permissions`:** Yalnızca indirme yapılan sitenin çerezlerine erişmek ve `127.0.0.1:9666` yerel köprüsüne bağlanabilmek için.

---

## 5. Kullanıcı Kontrolleri ve Hakları

Kullanıcı, veri akışını ve eklenti davranışını dilediği zaman kontrol edebilir:

- **Geçici Baypas (Alt Tuşu):** İndirme bağlantısına tıklarken `Alt` tuşuna basılı tutulduğunda FDownloader tamamen devre dışı kalır ve indirme tarayıcının kendi indirme motoruyla yapılır.
- **Eklentiyi Kapatma:** Tarayıcı eklentisi araç çubuğu popup'ından tek tıkla pasife alınabilir.
- **Kaldırma:** Eklenti tarayıcının Eklentiler menüsünden dilediğiniz zaman tek tıkla tamamen kaldırılabilir; arkasında hiçbir sunucu kaydı bırakmaz.

---

## 6. Değişiklikler ve İletişim

Bu gizlilik politikasında yapılabilecek olası güncellemeler GitHub depomuzda (`docs/GIZLILIK.md`) yayınlanacaktır.

- **Proje Deposu:** [GitHub / FDownloader](https://github.com/faruk/FDownloader)  
- **Lisans:** Açık Kaynak

