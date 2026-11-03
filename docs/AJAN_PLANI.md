# FDownloader — Ajan Uygulama Planı

> **Bu dosya kimin için?** Kodu yazacak yapay zeka ajanı (Antigravity, Gemini, Claude veya başka bir model) ve proje sahibi için.
> **Amaç:** FDownloader'ı eksiksiz, sağlam, güvenli ve herkesin kullanabileceği bir indirme yöneticisi haline getirmek.
> **Önemli:** Bu planı hazırlayan model, repo kodunun içini görmedi; sadece README ve repo ana sayfasını okudu. Bu yüzden plan **önce denetimle** başlar. Koda dair her varsayım denetimde doğrulanmalıdır.

---

## 0. Bu Dosya Nasıl Kullanılır

### 0.1 Proje sahibi için
1. Bu dosyayı repo köküne `docs/AJAN_PLANI.md` olarak koy.
2. Ayrıca boş bir `docs/ILERLEME.md` ve `docs/DEVIR_TESLIM.md` dosyası oluştur (ajan ilk oturumda zaten oluşturacak).
3. Her yeni oturumda ajana **Bölüm 9'daki hazır komutlardan** birini yapıştır.
4. Kota biterse veya model değiştirirsen: ajana "devir teslim yaz" dedirt, sonra yeni modele "devam et" komutunu ver (Bölüm 8).
5. Güçlü modelin (ör. Claude/Gemini Pro) kotası kısıtlıysa onu **zor görevlere** sakla (işaretli: 🔴). Zayıf/hızlı model (ör. Flash) **kolay görevlere** yetsin (işaretli: 🟢). Orta görevler 🟡.

### 0.2 Ajan için (zorunlu okuma)
Bu dosyayı, `docs/ILERLEME.md` ve `docs/DEVIR_TESLIM.md` dosyalarını **her oturumun başında oku.** Sonra Bölüm 2'deki çalışma kurallarına uy.

### 0.3 Görev zorluk etiketleri
- 🟢 **Kolay:** Küçük, tek dosyalık veya mekanik iş. Hızlı/zayıf model yapabilir.
- 🟡 **Orta:** Birkaç dosya, dikkatli okuma gerekir.
- 🔴 **Zor:** Mimari karar, eşzamanlılık, güvenlik veya karmaşık hata ayıklama. Güçlü model kullan.

---

## 1. Proje Bağlamı (README'den doğrulanmış bilgiler)

| Konu | Bilgi |
|---|---|
| Ad | FDownloader |
| Tür | Masaüstü indirme yöneticisi |
| Platform | Şu an Windows (Avalonia UI sayesinde çapraz platform potansiyeli var) |
| Teknoloji | .NET 9, Avalonia UI |
| Çözüm dosyası | `FDownloader.sln` |
| Ana uygulama | `src/FDownloader.App/FDownloader.App.csproj` |
| Başlatıcı | `Baslat.bat` |
| Tarayıcı eklentisi | `tools/browser-extension` (Chromium; "paketlenmemiş öğe" olarak kuruluyor) |
| CI | `.github/workflows` altında iş akışları var (içeriği denetlenecek) |
| Güncelleme/Kurulum | Velopack |
| Torrent | MonoTorrent |
| Medya | yt-dlp |
| Arşiv | SharpCompress + gömülü 7-Zip |
| Lisans | MIT |
| Sürüm | 1.5.0 |

**Bildiğimiz özellikler:** Parçalı HTTP indirme, resume, video köşe yakalayıcı, sistem tepsisi, başlangıçta açılma, otomatik arşiv çıkarma (şifreli arşiv dahil), ağ adaptörü kilidi (VPN kill-switch), site/galeri yakalayıcı, BitTorrent, toast bildirimleri, otomatik güncelleme, temalar.

**Bilmediğimiz şeyler (denetimde öğrenilecek):** Klasör yapısı, test var mı, eklenti ile uygulama nasıl haberleşiyor, veriler nerede ve nasıl saklanıyor, çoklu dil var mı, hata yönetimi nasıl, loglama var mı.

---

## 2. Ajan Çalışma Kuralları (Değişmez)

1. **Önce oku, sonra yaz.** Bir dosyayı değiştirmeden önce o dosyayı ve onu kullanan yerleri oku. Dosyanın içeriğini tahmin etme.
2. **Bir seferde tek görev.** Görev ID'sini (örn. `P2-03`) belirle, sadece onu yap. Kapsam dışı "güzelleştirme" yapma.
3. **Küçük adımlar, sık commit.** Her görev bittiğinde ayrı commit. Commit mesajı: `P2-03: Resume sonrası dosya değişim kontrolü eklendi`.
4. **Her değişiklikten sonra derle.** Derleme hatası varsa görevi bitmiş sayma.
5. **Test varsa çalıştır, yoksa yaz.** Davranış değiştiren her görev için test ekle (özellikle motor, dosya adı, güvenlik).
6. **Mevcut davranışı bozma.** Çalışan bir özelliği değiştirirsen önce o özelliğin mevcut davranışını bir testle sabitle.
7. **Emin değilsen sor.** Mimari karar, isim değişikliği, veri formatı değişikliği, bağımlılık ekleme/çıkarma gibi konularda dur ve proje sahibine kısa bir soru sor (seçenekleriyle birlikte).
8. **Uydurma.** Bir kütüphanenin API'sini hatırlamıyorsan tahmin etme; resmi dokümana veya paketin kaynağına bak. Var olmayan fonksiyon çağırma.
9. **Sır yok.** Şifre, token, imzalama anahtarı, kişisel yol bilgisi kodlara, loglara, commit'lere girmesin. `.gitignore` kontrol et.
10. **Yıkıcı işlem yapma.** Dosya silme, geçmişi yeniden yazma (force push), kullanıcı verisini sıfırlama gibi işlemleri onay almadan yapma.
11. **Geriye dönük uyumluluk.** Kullanıcıların mevcut veritabanı/ayar dosyaları bozulmamalı. Şema değişiyorsa migration yaz.
12. **UI iş parçacığını bloklama.** Ağ ve disk işleri arka planda, async olsun.
13. **Her oturum sonunda:** `docs/ILERLEME.md` güncelle ve `docs/DEVIR_TESLIM.md` yaz (Bölüm 8).
14. **Kendini doğrula.** "Bitti" demeden önce o görevin "Bitti sayılır" kriterlerini tek tek kontrol et ve sonucu raporla.
15. **Dürüst ol.** Yapamadığını, test edemediğini veya emin olmadığını açıkça yaz. "Çalışıyor olmalı" demek yerine "şu testle doğruladım" veya "doğrulayamadım" de.

### Zayıf modeller için ek kurallar
- Bir görev 5'ten fazla dosyaya dokunuyorsa dur, görevi alt görevlere böl ve `ILERLEME.md`'ye yaz.
- Büyük dosyaları baştan yazma; küçük, hedefli düzenlemeler yap.
- Hata mesajını **olduğu gibi** oku; yorumlamadan önce hangi satır ve hangi dosyada olduğunu belirt.
- Aynı hatayı üç kez düzeltemediysen dur ve durumu `DEVIR_TESLIM.md`'ye yaz; güçlü modele bırak.

---

## 3. Faz 0 — Denetim ve Başlangıç Durumu

**Amaç:** Projeyi tanımak, durumu belgelemek, güvenli bir başlangıç noktası kurmak.

### P0-01 🟡 Repo haritasını çıkar
- Tüm klasör ve proje yapısını listele (`src` altındaki her proje, `tools`, `.github`).
- Her projenin görevini 1–2 cümleyle yaz.
- Çıktı: `docs/MIMARI.md` (proje bağımlılık şeması + her bileşenin sorumluluğu).
- **Bitti sayılır:** Başka biri `MIMARI.md`'yi okuyup hangi kodun nerede olduğunu bulabilir.

### P0-02 🟢 Derleme ve çalıştırma doğrulaması
- Temiz klonda derle. Uyarıları ve hataları not et.
- Uygulamayı çalıştırıp temel akışı dene: link ekle → indir → bitir.
- **Bitti sayılır:** `docs/ILERLEME.md`'ye "derleme: başarılı/başarısız, uyarı sayısı" yazıldı.

### P0-03 🟡 Bağımlılık envanteri
- Tüm NuGet paketleri, sürümleri, lisansları, bilinen güvenlik açıkları (`dotnet list package --vulnerable` ve `--outdated` benzeri kontrol).
- gömülü ikili dosyalar (yt-dlp, 7-Zip, ffmpeg varsa) hangi sürüm, nereden geliyor, nasıl güncelleniyor.
- Çıktı: `docs/BAGIMLILIKLAR.md`.
- **Bitti sayılır:** Her bağımlılığın lisansı ve kaynağı tabloda var.

### P0-04 🔴 Güvenlik ve güvenilirlik denetimi
Aşağıdaki sorulara **koddan** cevap bul, tahmin etme:
1. Eklenti uygulamayla nasıl konuşuyor (yerel HTTP/WebSocket, Native Messaging, başka)? Kimlik doğrulama var mı? Origin kontrolü var mı? Hangi porta, hangi arayüze bağlanıyor?
2. Sunucudan gelen dosya adı nasıl temizleniyor? `..`, mutlak yol, yasak karakter, ayrılmış Windows adları, çok uzun ad nasıl ele alınıyor?
3. Resume bilgisi nerede, hangi formatta, ne sıklıkla yazılıyor? "İndirildi" değeri diske yazılmadan mı kaydediliyor?
4. Dosya yazımı nasıl (önceden yer ayırma + offset mi, parça dosyaları mı)? Birleştirme nasıl?
5. Sunucu dosyası değişirse (ETag/Last-Modified) ne oluyor?
6. Hata sınıflandırma ve yeniden deneme var mı (backoff, stall tespiti)?
7. Arşiv çıkarırken hedef yol doğrulaması (zip-slip), toplam boyut/oran sınırı var mı?
8. Çerezler, şifreler, proxy bilgileri nerede ve nasıl saklanıyor (düz metin mi)?
9. TLS sertifika doğrulaması herhangi bir yerde kapatılmış mı?
10. Yönlendirme sayısı sınırlı mı? Yönlendirmede çerez/kimlik başka alan adına sızıyor mu?
11. Güncelleme paketleri imzalı/doğrulanıyor mu?
12. Loglara hassas veri yazılıyor mu?
13. İndirilen dosyaya "internetten indirildi" işareti ekleniyor mu?
14. Yt-dlp ve diğer dış süreçler çağrılırken komut satırı argümanları nasıl oluşturuluyor (kullanıcı girdisiyle komut enjeksiyonu riski)?
15. Torrent: varsayılan indirme klasörü dışına yazma, dosya adı doğrulama, port yönlendirme/UPnP durumu?

- Çıktı: `docs/DENETIM.md`. Her bulgu için: **Bulgu / Dosya ve satır / Risk (Kritik-Yüksek-Orta-Düşük) / Önerilen düzeltme / Hangi görev (ID) ile düzeltilecek.**
- **Bitti sayılır:** 15 sorunun hepsi cevaplandı; her bulgu bir göreve bağlandı veya "sorun yok" diye gerekçelendirildi.

### P0-05 🟢 Git hijyeni
- `.gitignore` kontrol: `bin/`, `obj/`, kullanıcı verileri, imzalama anahtarları, `.vs/`, IDE dosyaları.
- Repoda yanlışlıkla commit edilmiş ikili/gizli dosya var mı bak (varsa proje sahibine bildir, silme).
- Dal stratejisi: `main` korunur; çalışma `dev` veya `feature/...` dallarında.
- **Bitti sayılır:** Temiz bir `git status`; gizli dosya yok.

### P0-06 🟢 Plan ve ilerleme dosyalarını oluştur
- `docs/ILERLEME.md` (Bölüm 7'deki şablonla) ve `docs/DEVIR_TESLIM.md` (Bölüm 8'deki şablonla) oluştur.

### 🧑 Proje sahibinin vermesi gereken karar (P0 sonunda)
**Proje adı:** "Fdownloader" adı, tarayıcıya reklam enjekte eden eski bir adware ile aynı adı taşıyor ve aynı/benzer isimli başka siteler de var. Bu, antivirüs/SmartScreen itibarını ve aranabilirliği etkileyebilir.
- Seçenek A: Adı değiştir (önerilen; herkese ulaşmak hedefiyse daha erken yapmak daha ucuz).
- Seçenek B: Adı koru, ama kod imzalama, VirusTotal ve açık iletişimle itibar yönet.
- Karar `docs/KARARLAR.md`'ye yazılsın. Karar A ise Faz 1'in başında **P1-00** uygulanır.

---

## 4. Faz 1 — Temeller: İsim, Yapı, Test Altyapısı

### P1-00 🔴 (Yalnızca ad değişirse) Yeniden adlandırma
Adım adım, her adımdan sonra derle:
1. Yeni adın uygun olduğunu kontrol et: GitHub, Chrome Web Store, winget, alan adı, ticari marka veritabanı, "ad + virus/malware" araması.
2. Çözüm, proje klasörleri, `.csproj` dosyaları, namespace'ler, assembly adı.
3. Uygulama başlığı, simgeler, "Hakkında" ekranı, bildirim başlıkları.
4. Velopack paket kimliği, yükleyici adı, kısayollar, kaldırıcı girdisi.
5. Eklenti adı, açıklaması, manifest alanları, uygulama ile konuşma kimlikleri.
6. **Kullanıcı verisi klasörü:** Eski klasördeki ayarlar/indirme geçmişi yeni sürümde **otomatik taşınmalı** (migration). Eski veri silinmemeli.
7. **Güncelleme geçişi:** Mevcut 1.x kullanıcıları yeni ada sorunsuz güncellenebiliyor mu? Gerekiyorsa köprü sürüm hazırla.
8. README, lisans başlığı, CI iş akışları, repo adı (GitHub yönlendirmesi çalışır, yine de eski bağlantıları güncelle).
- **Bitti sayılır:** Eski ada dair hiçbir kalıntı yok (kod içinde arama yapılarak doğrulandı); eski veri yeni sürümde açılıyor; temiz kurulum ve güncelleme testleri başarılı.

### P1-01 🟡 Çekirdeği arayüzden ayır (gerekiyorsa)
- Denetimde indirme motoru arayüz koduna bağımlıysa: motoru ayrı bir sınıf kütüphanesine (`...Core`) taşı. Arayüz motoru arayüzler üzerinden kullansın.
- Bu, test edilebilirliğin ön koşuludur.
- Zaten ayrıysa bu görevi atla ve `MIMARI.md`'ye not düş.
- **Bitti sayılır:** Motor, arayüz projesine referans vermeden derleniyor.

### P1-02 🟡 Test projeleri
- Birim test projesi ve entegrasyon test projesi oluştur (xUnit önerilir).
- CI'da otomatik çalışsın.
- **Bitti sayılır:** `dotnet test` yeşil; CI'da çalışıyor.

### P1-03 🔴 Yerel test sunucusu (en değerli test altyapısı)
Testlerin içinden başlatılabilen, yapılandırılabilir bir yerel HTTP sunucusu yaz. Şu davranışları **ayrı uç noktalar** olarak sunmalı:
1. Normal dosya (Range destekli, `206` yanıtları).
2. Range desteksiz dosya (`Accept-Ranges` yok, Range isteğini yok sayar ve `200` döner).
3. Boyutu bilinmeyen akış (`chunked`).
4. Yavaş yanıt (bayt başına gecikme ayarlanabilir).
5. Yarıda kopan bağlantı (belirli bayttan sonra bağlantıyı kes).
6. Yanlış `Content-Length` (gerçekten kısa/uzun).
7. Yönlendirme zinciri ve yönlendirme döngüsü.
8. Rastgele hata enjeksiyonu (`500`, `503`, `429` + `Retry-After`, `403`, `404`).
9. `Content-Disposition` biçimleri (tırnaklı, tırnaksız, `filename*=UTF-8''...`, kötü niyetli `../x`).
10. Kimlik doğrulama isteyen uç nokta (Basic).
11. Çerez veya Referer zorunlu uç nokta.
12. ETag/Last-Modified değişen dosya (iki istek arasında içerik değişir).
13. Çok büyük dosya akışı (bellekte tutmadan, deterministik üretilen içerik; hash bilinsin).
- Her uç noktanın beklenen içeriği **deterministik** olsun ki hash karşılaştırılabilsin.
- **Bitti sayılır:** Sunucu testlerden tek satırla başlatılabiliyor; 13 senaryonun hepsi için en az bir örnek test var (motor testleri sonraki görevlerde yazılacak).

### P1-04 🟢 Statik kod kalitesi
- `.editorconfig`, nullable referans denetimi, analizör uyarılarını "hata" seviyesine çıkarma (kademeli).
- Biçimlendirme denetimi CI'da.
- **Bitti sayılır:** CI biçim/analiz hatasında kırmızı oluyor.

### P1-05 🟢 Loglama altyapısı
- Seviyeli, dosyaya yazan, boyut/gün bazlı dönen log (ör. Serilog benzeri).
- Hassas veri maskeleme (URL'deki token, `Authorization`, `Cookie`).
- "Log paketini dışa aktar" düğmesi (ayarlarda) — ZIP olarak, hassas verisiz.
- **Bitti sayılır:** Logda şifre/cookie/token görünmüyor (testle doğrulandı).

---

## 5. Faz 2 — İndirme Motorunu Sağlamlaştırma

> Her görevde önce **yeni bir başarısız test** yaz, sonra düzelt (mümkünse). Test sunucusu (P1-03) kullanılır.

### P2-01 🔴 Veri bütünlüğü kanıtı (hash testleri)
- Test: 1 MB, 100 MB, 1 GB deterministik dosya → parça sayıları 1, 2, 4, 8, 16 → SHA-256 eşleşmeli.
- Parça sınırları: dosya boyutu parça sayısına tam bölünmüyor, 1 baytlık dosya, 0 baytlık dosya, parça sayısından küçük dosya.
- **Bitti sayılır:** Tüm kombinasyonlar yeşil; başarısız olan varsa motor düzeltildi.

### P2-02 🔴 Kalıcı resume'un kayıpsızlığı
- Test: İndirme ortasında süreci **zorla sonlandır** (gerçek süreç kill), yeniden başlat, devam ettir → hash eşleşmeli.
- Kural: Kayıtlı "indirildi" değeri **diske gerçekten yazılmış** (flush edilmiş) veriyi aşmamalı.
- Checkpoint sıklığı: birkaç saniyede bir veya N MB'da bir; veritabanı yazımı toplu/aralıklı.
- Bozuk/yarım checkpoint kaydı açılışta tespit edilip güvenli duruma getirilmeli.
- **Bitti sayılır:** 50 kez rastgele noktada kill → resume döngüsünde bir kez bile bozuk dosya yok.

### P2-03 🟡 Dosya değişim kontrolü
- Resume öncesi ETag/Last-Modified/boyut doğrula.
- Değişmişse: kullanıcıya "Dosya sunucuda değişmiş: baştan başla / iptal" seçeneği sun.
- **Bitti sayılır:** Test sunucusunun senaryo 12'si ile doğrulandı.

### P2-04 🟡 Probe (ön inceleme) sağlamlığı
- HEAD reddedilirse küçük Range isteğiyle yedek yöntem.
- Yönlendirme zinciri takibi, **yönlendirme sayısı üst sınırı** ve döngü tespiti.
- Dosya adı önceliği: `Content-Disposition` → nihai URL → sorgu parametresi → Content-Type'tan uzantı.
- Sıkıştırma (`Content-Encoding`) istenmesin ki boyut/Range güvenilir kalsın.
- **Bitti sayılır:** Senaryo 2, 3, 7, 9 testleri yeşil.

### P2-05 🔴 Hata sınıflandırma ve yeniden deneme
Şu tabloya göre davran:

| Durum | Tepki |
|---|---|
| Bağlantı/DNS/zaman aşımı | Üstel geri çekilme + jitter ile yeniden dene |
| 500/502/503/504 | Yeniden dene |
| 429 | `Retry-After`'a uy, parça sayısını düşür |
| 401/403 | Kimlik iste / URL süresi dolmuş olabilir bilgisini ver |
| 404/410 | Kalıcı hata, deneme |
| 416 / beklenmeyen 200 | Range desteğini yeniden değerlendir; gerekirse tek bağlantıya düş |
| Disk dolu / izin yok | Duraklat, net mesaj |
| Boyut/hash uyuşmazlığı | Etkilenen parçaları yeniden indir, olmazsa "bozuk" işaretle |

- Her parça için ayrı deneme sayacı; maksimum deneme ve toplam süre sınırı.
- **Bitti sayılır:** Senaryo 5, 6, 8 testleri yeşil; kullanıcıya gösterilen mesajlar anlaşılır.

### P2-06 🔴 Takılma (stall) tespiti ve bağlantı yönetimi
- Bağlantı zaman aşımı, okuma zaman aşımı ve "veri akışı durdu" zaman aşımı **ayrı** olsun.
- Takılan parçayı kes ve yeniden bağlan.
- Sunucu çok fazla bağlantıyı reddederse parça sayısını otomatik düşüren uyarlamalı mantık.
- **Bitti sayılır:** Senaryo 4 ve 5 ile takılan bağlantı otomatik toparlanıyor.

### P2-07 🔴 Dinamik parça bölme
- Bir worker işini bitirince, hâlâ süren en büyük parçanın kalanını ikiye böl ve yardım et.
- Bölme sırasında veri çakışması/çift yazım olmamalı.
- **Bitti sayılır:** Hash testleri hâlâ yeşil; "son %5'te bekleme" süresi ölçülüp iyileştiği doğrulandı.

### P2-08 🟡 Disk ve dosya sistemi
- Başlamadan boş alan kontrolü; indirme sırasında azalırsa duraklat ve uyar.
- Önceden yer ayırma başarısızsa hata ver.
- FAT32 4 GB sınırı uyarısı.
- Geçici dosya (`.part` gibi) → bitince **atomik** yeniden adlandırma.
- Hedef klasör silindi/sürücü çıkarıldı durumu.
- **Bitti sayılır:** Her durum için test veya elle test adımı `docs/TEST_SENARYOLARI.md`'de.

### P2-09 🟡 Dosya adı güvenliği ve çakışma politikası
- Yasak karakterler (`\ / : * ? " < > |`), ayrılmış adlar (CON, PRN, AUX, NUL, COM1–9, LPT1–9), sondaki nokta/boşluk, çok uzun ad, Unicode normalleştirme.
- `../` içeren ad hedef klasör dışına **asla** çıkamaz (testle kanıtla).
- Çakışmada kullanıcı tercihi: üzerine yaz / numaralandır / sor / atla.
- **Bitti sayılır:** Kötü niyetli ad listesiyle (en az 30 örnek) test yeşil.

### P2-10 🟡 Hız hesabı, ETA, ilerleme olayları
- Hız: hareketli ortalama (3–5 sn); ETA bu hıza göre.
- Arayüze giden ilerleme güncellemeleri saniyede 2–5 ile sınırlı.
- **Bitti sayılır:** 1000 eşzamanlı küçük indirmede arayüz akıcı.

### P2-11 🟡 Kuyruk, hız sınırlayıcı, zamanlayıcı
- Eşzamanlı indirme limiti, öncelik, sürükle-bırak sıra.
- Genel ve görev başı hız sınırı (token bucket mantığı), çalışırken değiştirilebilir.
- Zamanlayıcı: başlat/durdur saatleri, bitince eylem (kapat/uyut/hiçbir şey).
- Ağ kesintisinde otomatik duraklat, bağlantı gelince devam.
- Bilgisayar uykudan dönünce görevler doğru duruma gelsin.
- **Bitti sayılır:** Her özellik için test veya `TEST_SENARYOLARI.md` adımı.

### P2-12 🟡 Görev durum makinesi
- Geçerli durum geçişleri tablosu `MIMARI.md`'de; geçersiz geçiş denenirse hata/log.
- **Bitti sayılır:** Durum geçişleri birim testleriyle kapsanıyor.

### P2-13 🟢 Tekrar eden URL, geçmiş, silme davranışı
- Aynı URL ikinci kez eklenirse uyar.
- "Listeden kaldır" ve "dosyayı da sil" ayrı seçenekler, ikincisi onaylı.
- **Bitti sayılır:** Elle test adımları yazıldı ve geçti.

---

## 6. Faz 3 — Güvenlik Sertleştirme

> Her güvenlik düzeltmesi için: önce **istismar eden bir test** (başarısız olmalı), sonra düzeltme, sonra testin geçtiği doğrulanır.

### P3-01 🔴 Eklenti ↔ uygulama köprüsü
Denetimde (P0-04) bulunan duruma göre:
- **Tercih:** Native Messaging (yalnızca belirli eklenti kimliğine izin verir).
- **Yerel HTTP/WebSocket kullanılıyorsa zorunlu önlemler:** yalnızca `127.0.0.1`'e bağlan; eşleştirme sırasında üretilen rastgele gizli anahtar (token) iste; `Origin` ve `Host` başlıklarını doğrula (DNS rebinding'e karşı); rastgele web sitesinin istek atmasını (CORS/fetch) engelle; hız sınırı koy.
- Eklentiden gelen **tüm verileri güvenilmez** kabul et: yalnızca `http`, `https`, (`ftp`) şemaları; dosya adı/yol doğrulaması; uzunluk sınırları.
- **Bitti sayılır:** Bir test web sayfasından uygulamaya yetkisiz istek gönderme denemesi **başarısız** oluyor; yetkili eklenti çalışıyor.

### P3-02 🔴 Arşiv çıkarma güvenliği
- **Zip-slip:** Çıkarılan her girdinin nihai yolu hedef klasörün içinde kalmalı (mutlak yol, `..`, sembolik bağlantı, sürücü harfi engellenir).
- **Arşiv bombası:** Toplam açılmış boyut, dosya sayısı ve sıkıştırma oranı için üst sınırlar; aşılırsa durdur ve kullanıcıya sor.
- **Şifre:** Parola düz metin loglanmaz/saklanmaz.
- **Varsayılan:** Otomatik çıkarma **kapalı** gelsin; kullanıcı ayarlardan açsın.
- Gömülü 7-Zip çağrılırken argümanlar güvenli oluşturulsun (komut enjeksiyonu yok).
- **Bitti sayılır:** Kötü niyetli örnek arşivlerle (zip-slip, bomba, sembolik bağlantı) yapılan testler yeşil.

### P3-03 🔴 Dış süreç (yt-dlp, 7-Zip, ffmpeg) güvenliği
- Kullanıcı girdisi komut satırına **argüman listesi** olarak verilmeli (tek bir string birleştirme yok).
- URL'ler doğrulansın (şema kısıtı); `--` ile seçenek enjeksiyonu engellensin.
- Çıktı klasörü sabit ve doğrulanmış olsun.
- Dış ikili dosyaların bütünlüğü (hash) doğrulansın; güncellemeleri güvenli kaynaktan.
- **Bitti sayılır:** "Kötü" URL/argüman listesiyle testler yeşil.

### P3-04 🟡 Hassas verilerin saklanması
- Şifre, cookie, token, proxy kimlik bilgisi → Windows Credential Manager / DPAPI (platform bazlı soyutlama).
- Veritabanı/ayar dosyalarında düz metin hassas veri kalmasın; mevcut veriler migration ile taşınsın.
- Cookie'ler gerekli süre sonra silinsin.
- **Bitti sayılır:** Veri klasöründe düz metin parola/token araması (grep) sonuç vermiyor.

### P3-05 🟡 Ağ güvenliği
- TLS doğrulaması varsayılan açık; kapatma seçeneği varsa açık uyarılı ve oturum bazlı olsun.
- Yönlendirmede kimlik/cookie **başka alan adına** gönderilmesin.
- HTTPS → HTTP düşürme için uyarı.
- Zaman aşımı ve boyut sınırları (sonsuz veri akışı saldırısı).
- **Bitti sayılır:** Test sunucusuyla yönlendirme senaryoları testte doğrulandı.

### P3-06 🟡 İndirilen dosyalar
- "İnternetten indirildi" işareti (Windows Zone.Identifier) eklensin.
- Tehlikeli uzantılar (`.exe`, `.bat`, `.cmd`, `.scr`, `.js`, `.vbs`, `.ps1`, `.msi` vb.) için "Aç" eyleminde uyarı.
- Otomatik çalıştırma yok.
- **Bitti sayılır:** İndirilen dosyada işaretin varlığı testle doğrulandı.

### P3-07 🟡 Torrent güvenliği
- Dosya adı/yol doğrulaması (torrent içindeki `..` ve mutlak yollar).
- Varsayılan klasör dışına yazma yok.
- Ağ adaptörü kilidi (kill-switch) davranışı test edilsin: VPN kopunca **tüm** trafik (torrent dahil) kesiliyor mu? DNS ve yerel ağ sızıntısı var mı?
- UPnP/port yönlendirme kullanıcı kontrolünde olsun.
- **Bitti sayılır:** Kill-switch testi belgelendi (`docs/TEST_SENARYOLARI.md`).

### P3-08 🟡 Güncelleme güvenliği
- Güncelleme kanalı HTTPS; paketlerin bütünlüğü doğrulanıyor mu kontrol et.
- Geri alma planı (kötü sürüm çıkarsa).
- **Bitti sayılır:** Güncelleme akışı `docs/GUNCELLEME.md`'de belgelendi ve kontrol edildi.

### P3-09 🟢 Gizlilik
- Telemetri var mı? Varsa varsayılan **kapalı**, açıkça anlatılmış ve kapatılabilir.
- Loglarda/crash raporlarda kişisel veri yok.
- **Bitti sayılır:** Ağ trafiği incelemesiyle uygulamanın beklenmeyen bir yere bağlanmadığı doğrulandı.

---

## 7. Faz 4 — Tarayıcı Eklentisi ve Entegrasyon

### P4-01 🟡 Eklenti denetimi ve temizliği
- Manifest sürümü (Manifest V3), izinlerin **minimuma** indirilmesi.
- Kullanılmayan kod ve gereksiz izinleri kaldır.
- Gizlilik politikası taslağı hazırla (`docs/GIZLILIK.md`).
- **Bitti sayılır:** Her izin için "neden gerekli" gerekçesi `docs/EKLENTI.md`'de.

### P4-02 🔴 Yakalama doğruluğu
- Tarayıcının kendi indirmesini iptal edip uygulamaya devretme; çerez, Referer, User-Agent aktarımı.
- Yakalama kuralları (uzantı, minimum boyut, hariç tutulan siteler); **Alt tuşu ile atlama.**
- Uygulama kapalıyken/ulaşılamazken tarayıcı indirmesi **normal devam etmeli** (kullanıcı indirme kaybetmemeli).
- **Bitti sayılır:** Uygulama kapalıyken indirme tıklaması tarayıcıda normal çalışıyor; açıkken devrediliyor.

### P4-03 🟡 Medya algılama
- Doğrudan video dosyaları ve HLS (`.m3u8`)/DASH (`.mpd`) algılama.
- DRM korumalı içerik: algılanırsa **indirmeye çalışma**, kullanıcıya "korumalı içerik" bilgisi ver.
- **Bitti sayılır:** DRM'li içerikte uygulama indirmeyi denemiyor ve anlaşılır mesaj veriyor.

### P4-04 🟡 Firefox desteği
- Eklentiyi Firefox'a uyarla, ayrı manifest/derleme hedefi.
- **Bitti sayılır:** Firefox'ta temel yakalama çalışıyor.

### P4-05 🟢 Eklenti paketleme otomasyonu
- CI, her sürümde eklenti ZIP'ini otomatik üretsin.
- **Bitti sayılır:** Release'te eklenti ZIP'i otomatik ekleniyor.

### P4-06 🧑 Mağaza yayını (proje sahibi + ajan)
- Ajan: mağaza için gerekli metinler, ekran görüntüsü listesi, gizlilik politikası, izin gerekçeleri hazırlar.
- Proje sahibi: Chrome Web Store ve Edge Add-ons geliştirici hesabı açar, gönderir.
- **Bitti sayılır:** Eklenti en az bir mağazada yayında.

---

## 8. Faz 5 — Medya, Torrent ve Ek Özellikler

### P5-01 🟡 yt-dlp entegrasyonu sağlamlığı
- yt-dlp sürümünü uygulamadan **güncelleyebilme** (siteler sık değiştiği için kritik).
- Başarısız indirmede yt-dlp'nin hata çıktısını anlaşılır mesaja çevir.
- İptal/duraklat: alt süreç temiz sonlandırılsın (yetim süreç kalmasın).
- Kalite/format seçimi arayüzü.
- **Bitti sayılır:** İptal sonrası görev yöneticisinde yetim `yt-dlp`/`ffmpeg` süreci yok.

### P5-02 🟡 HLS/DASH ve birleştirme
- Parçaları paralel indir, sırayla birleştir, gerekirse konteynere (mp4) dönüştür.
- Ses/video ayrı akışlarsa birleştir.
- **Bitti sayılır:** Test akışıyla (kendi test sunucunda HLS örneği) uçtan uca doğrulandı.

### P5-03 🟡 Torrent sağlamlığı
- Magnet ve `.torrent` desteği, dosya seçimi, sıralı indirme, eş takibi.
- Resume (uygulama yeniden açılınca devam), hash kontrolü (yeniden kontrol).
- Bağlantı noktası, DHT, şifreleme ayarları.
- Seed davranışı: varsayılan davranış açıkça belirlensin ve ayarlanabilsin.
- **Bitti sayılır:** Yasal bir test torrent'i (ör. Linux dağıtım ISO'su) ile uçtan uca test.

### P5-04 🟡 Site/galeri yakalayıcı
- Tarama derinliği, aynı alan adıyla sınırlama, **robots/hız sınırı** nazikliği (sunucuyu boğma).
- Sonuçları filtreleme (tür, boyut) ve önizleme.
- **Bitti sayılır:** Aşırı büyük sayfada uygulama donmuyor, kullanıcı iptal edebiliyor.

### P5-05 🟡 Proxy ve kimlik doğrulama
- HTTP/HTTPS/SOCKS5 proxy, sistem proxy'sini kullanma, proxy kimlik doğrulaması.
- Site giriş bilgileri yöneticisi (Basic/Digest/NTLM), güvenli saklama (P3-04).
- **Bitti sayılır:** Test sunucusu ve yerel proxy ile doğrulandı.

### P5-06 🟢 Toplu indirme
- Link listesi yapıştırma, sayı aralığı deseni (`dosya[01-20].jpg` mantığı).
- **Bitti sayılır:** 100 linkle test edildi.

---

## 9. Faz 6 — Kullanıcı Deneyimi

### P6-01 🟡 İlk açılış sihirbazı
- Dil, indirme klasörü, eklenti kurulumu (adım adım, görsel), başlangıçta açılma, tema.
- **Bitti sayılır:** Temiz bir Windows kullanıcısı sihirbazla eklentiyi kurabiliyor.

### P6-02 🟡 Çoklu dil altyapısı
- Tüm arayüz metinleri kaynak dosyalarına taşınsın (sabit metin kalmasın).
- İlk diller: Türkçe ve İngilizce. Sistem diline göre otomatik seçim + ayarlardan değiştirme.
- **Bitti sayılır:** Kodda arayüze giden sabit string kalmadı (arama ile doğrulandı); dil değişince tüm ekranlar değişiyor.

### P6-03 🟡 Hata mesajları
- Her hata: **ne oldu / neden olmuş olabilir / ne yapabilirsin** (+ teknik ayrıntı "ayrıntıları göster" ile).
- Yaygın hataların listesi `docs/HATA_KODLARI.md`'de.
- **Bitti sayılır:** P2-05 tablosundaki her durumun kullanıcı mesajı var.

### P6-04 🟡 Arayüz iyileştirmeleri
- İndirme detay ekranı: parça haritası, hız grafiği, bağlantı bilgisi, görev logu.
- Büyük listelerde sanallaştırma.
- Klavye kısayolları, çoklu seçim, sürükle-bırak (URL ve dosya).
- Pano izleme (açılıp kapatılabilir).
- Sistem tepsisi menüsü: tümünü duraklat/devam, hız sınırı, çıkış.
- **Bitti sayılır:** 5000 kayıtlı listede arayüz akıcı.

### P6-05 🟡 Erişilebilirlik ve ekran uyumu
- Klavyeyle tam kullanım, ekran okuyucu etiketleri (AutomationProperties), yüksek kontrast teması.
- %100–%200 DPI ölçekleme testleri; çoklu monitör.
- **Bitti sayılır:** Fare kullanmadan temel akış (ekle, duraklat, sil, ayarlar) tamamlanabiliyor.

### P6-06 🟢 Ayarlar düzeni
- Ayarlar mantıklı sekmelerde ve her ayarın kısa açıklaması var.
- "Varsayılana sıfırla" seçeneği.
- Ayarları dışa/içe aktarma.
- **Bitti sayılır:** Tüm ayarların açıklaması var.

### P6-07 🟡 Güç ve sistem davranışları
- İndirme sürerken uyku engelleme seçeneği.
- Çıkışta aktif indirme varsa uyarı.
- Başlangıçta açılma: arka planda küçük başlat seçeneği.
- **Bitti sayılır:** Seçenekler çalışıyor ve elle test edildi.

---

## 10. Faz 7 — Dağıtım ve Güven

### P7-01 🧑 Kod imzalama (proje sahibi karar verir)
- Seçenekler: ücretli kod imzalama sertifikası (OV/EV), veya sertifika sağlayıcıların bulut imzalama hizmetleri, veya (uygunsa) açık kaynak projelere yönelik ücretsiz/indirimli programlar.
- Ajan: imzalama adımını CI'a entegre eder (anahtar **repoda değil**, CI gizli değişkenlerinde).
- **Bitti sayılır:** Yayınlanan `Setup.exe` ve ana `.exe` imzalı; sağ tık → Özellikler → Dijital imzalar görünüyor.

### P7-02 🟡 CI/CD sürüm hattı
- Etiket (tag) atılınca: derle → test → imzala → paketle → Release oluştur → eklenti ZIP'i ekle → SHA-256 listesi üret.
- Sürüm numarası tek yerden yönetilsin.
- **Bitti sayılır:** Tek komutla/etiketle tam sürüm çıkıyor.

### P7-03 🟢 Güven artırıcılar
- Her sürümde SHA-256 hash listesi yayınla.
- VirusTotal taraması sonucunu release notuna ekle.
- Yanlış pozitif olursa ilgili AV firmalarına bildirme şablonu `docs/YANLIS_POZITIF.md`.
- **Bitti sayılır:** Son sürümün hash listesi ve tarama bağlantısı release'te.

### P7-04 🟡 Paket yöneticileri
- winget manifesti, (isteğe bağlı) Chocolatey ve Scoop.
- **Bitti sayılır:** `winget install` ile kurulum çalışıyor (veya PR gönderildi).

### P7-05 🟡 Güncelleme deneyimi
- Güncelleme bildirimi, "sürüm notlarını göster", güncellemeyi erteleme.
- Beta/kararlı kanal seçeneği.
- Kötü sürüm için geri alma yolu belgelenmiş.
- **Bitti sayılır:** Sürümler arası güncelleme (eski → yeni) temiz makinede test edildi.

### P7-06 🟡 Çapraz platform (isteğe bağlı, sonraya)
- Avalonia'nın sunduğu imkânla Linux/macOS araştırması: Windows'a özel kodlar (Zone.Identifier, tepsi, başlangıç, Credential Manager) soyutlama arkasına alınsın.
- **Bitti sayılır:** Platforma özel kod tek bir katmanda toplandı; en az bir ek platformda derleme çalışıyor.

---

## 11. Faz 8 — Hukuk, Belgeler, Topluluk

### P8-01 🟡 Lisans ve üçüncü taraf bildirimleri
- Tüm bağımlılıkların lisans listesi; uygulamada "Üçüncü taraf lisanslar" ekranı; repoda `THIRD_PARTY_NOTICES.md`.
- Gömülü yt-dlp, 7-Zip ve (varsa) ffmpeg için lisans koşullarına uyum (dağıtım şekli, kaynak kodu bildirimi).
- **Bitti sayılır:** Her paketlenen bileşenin lisansı ekranda ve dosyada.

### P8-02 🟡 Sorumluluk reddi ve içerik politikası
- README ve uygulama ilk açılışında: "Telif haklarına ve sitelerin kullanım şartlarına uymak kullanıcının sorumluluğundadır."
- README'deki "YouTube ve popüler platformlar tam uyumlu" gibi ifadeleri hukuki risk açısından yumuşat (ör. "yt-dlp tarafından desteklenen sitelerden medya indirme").
- DRM aşmaya yönelik hiçbir özellik eklenmez (karar olarak `KARARLAR.md`'ye yaz).
- **Bitti sayılır:** Metinler güncellendi; proje sahibi onayladı.

### P8-03 🟢 Gizlilik politikası ve kullanım koşulları
- Ne toplanıyor (ideal: hiçbir şey), nerede saklanıyor, eklenti izinleri.
- **Bitti sayılır:** `docs/GIZLILIK.md` hazır; eklenti mağazası ve README'den bağlantılı.

### P8-04 🟡 README ve dokümantasyon
- Ekran görüntüleri/GIF, özellik listesi (doğru ve abartısız), kurulum, SSS, sorun giderme, bilinen sınırlamalar, karşılaştırma tablosu (isteğe bağlı).
- Türkçe ve İngilizce README.
- `docs/` altında: Mimari, Katkı rehberi (`CONTRIBUTING.md`), Davranış kuralları (`CODE_OF_CONDUCT.md`), Güvenlik politikası (`SECURITY.md` — açık bildirme yolu).
- **Bitti sayılır:** Yeni biri README ile 5 dakikada kurup ilk indirmesini yapabiliyor.

### P8-05 🟢 Topluluk altyapısı
- Issue şablonları (hata raporu: sürüm, Windows sürümü, log; özellik isteği), PR şablonu.
- CHANGELOG (Keep a Changelog biçimi) ve anlamsal sürümleme (SemVer).
- GitHub Discussions veya benzeri destek kanalı.
- **Bitti sayılır:** Şablonlar repoda aktif.

---

## 12. Faz 9 — Kararlılık ve Sürüm 2.0

### P9-01 🔴 Kaos ve dayanıklılık testleri
`docs/TEST_SENARYOLARI.md`'de elle yürütülen liste; her madde geçti/kaldı olarak işaretlenir:
- İndirme ortasında internet kes/bağla; Wi-Fi ↔ kablo geçişi; VPN düşmesi.
- Uyku/hazırda bekletme/uyanma; pil bitmesi (güç kesintisi benzetimi: süreç kill).
- Disk dolması; hedef klasör silinmesi; harici disk çıkarılması; ağ sürücüsü kopması.
- Antivirüsün geçici dosyayı kilitlemesi.
- 10 GB, 50 GB dosya; 5000+ görev listesi.
- Çok uzun/Unicode/emoji dosya adları.
- Saat değişimi, saat dilimi değişimi.
- Aynı anda 20 indirme; hızlı duraklat/devam et spam'i; kapatırken aktif indirme.
- **Bitti sayılır:** Tüm maddeler geçti; başarısızlar için hata kaydı ve düzeltme var.

### P9-02 🔴 Performans ölçümü
- Referans: aynı dosya aynı ağda — tarayıcı, `aria2` ve FDownloader hız karşılaştırması.
- Bellek ve CPU: 1 saatlik yoğun indirmede bellek sızıntısı var mı?
- Başlangıç süresi, boşta kaynak tüketimi.
- **Bitti sayılır:** Sonuçlar `docs/PERFORMANS.md`'de; ciddi darboğaz düzeltildi.

### P9-03 🟡 Beta programı
- Beta kanalı sürümü yayınla; 10–30 gerçek kullanıcıdan geri bildirim.
- Gelen hataları öncelik sırasına koy.
- **Bitti sayılır:** Kritik/Yüksek hata kalmadı.

### P9-04 🟡 Son güvenlik gözden geçirmesi
- `DENETIM.md`'deki tüm Kritik/Yüksek bulguların kapandığını doğrula.
- Yeni bağımlılık açıkları taraması.
- (Mümkünse) bağımsız bir kişiden gözden geçirme veya açık kaynak güvenlik topluluğundan geri bildirim.
- **Bitti sayılır:** Açık Kritik/Yüksek bulgu yok.

### P9-05 🟡 Sürüm 2.0 yayını
- CHANGELOG, sürüm notları, mağaza ve README güncellemeleri, duyuru metni.
- **Bitti sayılır:** İmzalı 2.0 sürümü yayında; yükseltme yolu test edildi.

---

## 13. İlerleme Takibi (`docs/ILERLEME.md` şablonu)

Ajan bu tabloyu her görev sonrası günceller. Durumlar: `⬜ Bekliyor` · `🔄 Devam` · `✅ Bitti` · `⏸ Engelli (neden)` · `⏭ Atlandı (neden)`

```
| ID     | Görev                                   | Zorluk | Durum | Commit | Not |
|--------|-----------------------------------------|--------|-------|--------|-----|
| P0-01  | Repo haritası                           | 🟡     | ⬜    |        |     |
| P0-02  | Derleme doğrulaması                     | 🟢     | ⬜    |        |     |
| P0-03  | Bağımlılık envanteri                    | 🟡     | ⬜    |        |     |
| P0-04  | Güvenlik/güvenilirlik denetimi          | 🔴     | ⬜    |        |     |
| P0-05  | Git hijyeni                             | 🟢     | ⬜    |        |     |
| P0-06  | İlerleme/devir dosyaları                | 🟢     | ⬜    |        |     |
| P1-00  | (Ad değişirse) Yeniden adlandırma       | 🔴     | ⬜    |        |     |
| P1-01  | Çekirdeği arayüzden ayır                | 🟡     | ⬜    |        |     |
| P1-02  | Test projeleri                          | 🟡     | ⬜    |        |     |
| P1-03  | Yerel test sunucusu                     | 🔴     | ⬜    |        |     |
| P1-04  | Statik kod kalitesi                     | 🟢     | ⬜    |        |     |
| P1-05  | Loglama                                 | 🟢     | ⬜    |        |     |
| P2-01  | Hash testleri                           | 🔴     | ⬜    |        |     |
| P2-02  | Kayıpsız resume                         | 🔴     | ⬜    |        |     |
| P2-03  | Dosya değişim kontrolü                  | 🟡     | ⬜    |        |     |
| P2-04  | Probe sağlamlığı                        | 🟡     | ⬜    |        |     |
| P2-05  | Hata sınıflandırma/yeniden deneme       | 🔴     | ⬜    |        |     |
| P2-06  | Stall tespiti                           | 🔴     | ⬜    |        |     |
| P2-07  | Dinamik parça bölme                     | 🔴     | ⬜    |        |     |
| P2-08  | Disk/dosya sistemi                      | 🟡     | ⬜    |        |     |
| P2-09  | Dosya adı güvenliği                     | 🟡     | ⬜    |        |     |
| P2-10  | Hız/ETA/ilerleme                        | 🟡     | ⬜    |        |     |
| P2-11  | Kuyruk/limit/zamanlayıcı                | 🟡     | ⬜    |        |     |
| P2-12  | Durum makinesi                          | 🟡     | ⬜    |        |     |
| P2-13  | Tekrar eden URL/silme                   | 🟢     | ⬜    |        |     |
| P3-01  | Eklenti köprüsü güvenliği               | 🔴     | ⬜    |        |     |
| P3-02  | Arşiv çıkarma güvenliği                 | 🔴     | ⬜    |        |     |
| P3-03  | Dış süreç güvenliği                     | 🔴     | ⬜    |        |     |
| P3-04  | Hassas veri saklama                     | 🟡     | ⬜    |        |     |
| P3-05  | Ağ güvenliği                            | 🟡     | ⬜    |        |     |
| P3-06  | İndirilen dosya güvenliği               | 🟡     | ⬜    |        |     |
| P3-07  | Torrent güvenliği/kill-switch           | 🟡     | ⬜    |        |     |
| P3-08  | Güncelleme güvenliği                    | 🟡     | ⬜    |        |     |
| P3-09  | Gizlilik                                | 🟢     | ⬜    |        |     |
| P4-01  | Eklenti izin denetimi                   | 🟡     | ⬜    |        |     |
| P4-02  | Yakalama doğruluğu                      | 🔴     | ⬜    |        |     |
| P4-03  | Medya algılama (DRM'siz)                | 🟡     | ⬜    |        |     |
| P4-04  | Firefox                                 | 🟡     | ⬜    |        |     |
| P4-05  | Eklenti paketleme otomasyonu            | 🟢     | ⬜    |        |     |
| P4-06  | Mağaza yayını                           | 🧑     | ⬜    |        |     |
| P5-01  | yt-dlp sağlamlığı                       | 🟡     | ⬜    |        |     |
| P5-02  | HLS/DASH                                | 🟡     | ⬜    |        |     |
| P5-03  | Torrent sağlamlığı                      | 🟡     | ⬜    |        |     |
| P5-04  | Site/galeri yakalayıcı                  | 🟡     | ⬜    |        |     |
| P5-05  | Proxy/kimlik doğrulama                  | 🟡     | ⬜    |        |     |
| P5-06  | Toplu indirme                           | 🟢     | ⬜    |        |     |
| P6-01  | İlk açılış sihirbazı                    | 🟡     | ⬜    |        |     |
| P6-02  | Çoklu dil                               | 🟡     | ⬜    |        |     |
| P6-03  | Hata mesajları                          | 🟡     | ⬜    |        |     |
| P6-04  | Arayüz iyileştirmeleri                  | 🟡     | ⬜    |        |     |
| P6-05  | Erişilebilirlik                         | 🟡     | ⬜    |        |     |
| P6-06  | Ayarlar düzeni                          | 🟢     | ⬜    |        |     |
| P6-07  | Güç/sistem davranışları                 | 🟡     | ⬜    |        |     |
| P7-01  | Kod imzalama                            | 🧑     | ⬜    |        |     |
| P7-02  | CI/CD sürüm hattı                       | 🟡     | ⬜    |        |     |
| P7-03  | Güven artırıcılar                       | 🟢     | ⬜    |        |     |
| P7-04  | Paket yöneticileri                      | 🟡     | ⬜    |        |     |
| P7-05  | Güncelleme deneyimi                     | 🟡     | ⬜    |        |     |
| P7-06  | Çapraz platform (ops.)                  | 🟡     | ⬜    |        |     |
| P8-01  | Lisans bildirimleri                     | 🟡     | ⬜    |        |     |
| P8-02  | Sorumluluk reddi                        | 🟡     | ⬜    |        |     |
| P8-03  | Gizlilik politikası                     | 🟢     | ⬜    |        |     |
| P8-04  | README/dokümantasyon                    | 🟡     | ⬜    |        |     |
| P8-05  | Topluluk altyapısı                      | 🟢     | ⬜    |        |     |
| P9-01  | Kaos testleri                           | 🔴     | ⬜    |        |     |
| P9-02  | Performans ölçümü                       | 🔴     | ⬜    |        |     |
| P9-03  | Beta programı                           | 🟡     | ⬜    |        |     |
| P9-04  | Son güvenlik gözden geçirmesi           | 🟡     | ⬜    |        |     |
| P9-05  | Sürüm 2.0                               | 🟡     | ⬜    |        |     |
```

### Önerilen sıra
`P0 → (ad kararı) → P1 → P2-01, P2-02 → P3-01, P3-02, P3-03 → geri kalan P2 → P3 geri kalan → P4 → P5 → P6 → P7 → P8 → P9`

**Kural:** P2-01 ve P2-02 (veri bütünlüğü ve kayıpsız resume) bitmeden yeni özellik **ekleme.**

---

## 14. Devir Teslim Protokolü (Kota bittiğinde veya model değiştiğinde)

### 14.1 `docs/DEVIR_TESLIM.md` şablonu
Ajan her oturum sonunda (veya kota bitmeden önce) bu dosyayı **baştan yazar**:

```
# Devir Teslim — <tarih ve saat>

## 1. Şu an üzerinde çalıştığım görev
ID ve ad: 
Durum: (başladı / yarıda / test aşamasında / bitti)

## 2. Yaptıklarım (bu oturumda)
- (commit hash + tek satır özet)

## 3. Yarıda kalan iş
- Tam olarak nerede kaldım:
- Değiştirdiğim ama commit etmediğim dosyalar:
- Derleme durumu: (başarılı / şu hatayla başarısız: ...)
- Test durumu: (geçen/kalan sayısı; kalan testler ve nedenleri)

## 4. Sıradaki adım (net ve küçük)
1.
2.
3.

## 5. Bilmesi gerekenler
- Keşfettiğim tuzaklar / beklenmedik durumlar:
- Aldığım kararlar ve nedenleri:
- Emin olmadığım noktalar:
- Proje sahibine sorulması gereken sorular:

## 6. Dokunulmaması gerekenler
- (kırılgan veya karar bekleyen alanlar)
```

### 14.2 Kota bitmeden önce ajan ne yapmalı?
Kota uyarısı, uzun bir görev ortası veya "az kaldı" hissi varsa:
1. Yarım bozuk kod bırakma: derlenebilir bir duruma getir **veya** değişiklikleri ayrı bir dalda `WIP:` ile commit et.
2. `ILERLEME.md`'yi güncelle.
3. `DEVIR_TESLIM.md`'yi yaz.
4. Proje sahibine "Devir teslim hazır" de.

### 14.3 Model değiştirirken
- Yeni modele **önce** `AJAN_PLANI.md` (özellikle Bölüm 2), `ILERLEME.md`, `DEVIR_TESLIM.md` okutulur.
- Yeni model, `DEVIR_TESLIM.md`'deki "Sıradaki adım"dan devam eder; **önce derleme ve testleri çalıştırıp** durumu doğrular.

---

## 15. Hazır Komutlar (Antigravity'ye yapıştırılacak metinler)

### 15.1 İlk oturum
```
Bu repo, FDownloader adlı bir .NET 9 / Avalonia indirme yöneticisidir.
docs/AJAN_PLANI.md dosyasını baştan sona oku. Özellikle Bölüm 2'deki çalışma kurallarına uy.
Şimdi Faz 0'ı başlat: P0-06, P0-01, P0-02, P0-03, P0-05 görevlerini sırayla yap.
Her görev sonunda "Bitti sayılır" kriterini kontrol et, docs/ILERLEME.md'yi güncelle ve commit at.
P0-04'ü (güvenlik denetimi) ayrı bir oturumda yapacağız, şimdi başlama.
Emin olmadığın bir şeyi tahmin etme; koddan doğrula ya da bana sor.
```

### 15.2 Denetim oturumu (güçlü model)
```
docs/AJAN_PLANI.md, docs/MIMARI.md ve docs/ILERLEME.md dosyalarını oku.
P0-04 görevini yap: Plandaki 15 soruyu doğrudan kaynak koddan cevapla.
Her bulgu için dosya yolunu ve satır numarasını, riski ve düzeltme görev ID'sini yaz.
Çıktıyı docs/DENETIM.md'ye yaz. Hiçbir kodu değiştirme, sadece denetle.
Bulguları uydurma; bir şeyi doğrulayamadıysan "doğrulanamadı" yaz.
```

### 15.3 Standart oturum (her gün)
```
docs/AJAN_PLANI.md (Bölüm 2), docs/ILERLEME.md ve docs/DEVIR_TESLIM.md dosyalarını oku.
Önce projeyi derle ve testleri çalıştır, durumu bana bildir.
Sonra DEVIR_TESLIM.md'deki "Sıradaki adım"dan veya ILERLEME.md'deki ilk ⬜ görevden devam et.
Bir seferde tek görev yap. Bitirince kriterleri kontrol et, ILERLEME.md'yi güncelle, commit at.
Oturum bitmeden veya kota azalırken DEVIR_TESLIM.md'yi güncelle.
```

### 15.4 Zayıf/hızlı model için (🟢 görevler)
```
docs/AJAN_PLANI.md Bölüm 2'yi (özellikle "Zayıf modeller için ek kurallar") oku.
ILERLEME.md'de 🟢 etiketli ve ⬜ durumundaki İLK görevi yap. Başka görev yapma.
Dosyaları baştan yazma, küçük düzenlemeler yap. Her değişiklikten sonra derle.
Aynı hatayı 3 kez düzeltemezsen dur, DEVIR_TESLIM.md'ye yaz ve bana bildir.
```

### 15.5 Güçlü model için (🔴 görevler)
```
docs/AJAN_PLANI.md, docs/MIMARI.md, docs/DENETIM.md, docs/ILERLEME.md ve DEVIR_TESLIM.md dosyalarını oku.
Görev: <GÖREV ID>. Önce ilgili kodu oku ve yaklaşımını 5-10 satırda bana açıkla; onayımdan sonra uygula.
Önce başarısız bir test yaz, sonra düzelt. Hash/bütünlük testleri yeşil kalmalı.
Bitince "Bitti sayılır" kriterlerini tek tek doğrula ve sonuçları raporla.
```

### 15.6 Kota biterken
```
Kotam bitmek üzere. Derlenebilir bir duruma gel (olmuyorsa WIP: commit'i at),
docs/ILERLEME.md'yi güncelle ve docs/DEVIR_TESLIM.md'yi şablona göre yaz.
Sıradaki adımları öyle net yaz ki başka bir model hiçbir şey sormadan devam edebilsin.
```

---

## 16. Sadece Proje Sahibinin Yapabileceği İşler

Ajan bunları yapamaz; sen yapmalısın (ajan hazırlık yapabilir):

| İş | Not |
|---|---|
| Proje adı kararı | P0 sonrası, Bölüm 3 |
| Kod imzalama sertifikası alma | Maliyet/yöntem seçimi, kimlik doğrulama |
| Chrome Web Store / Edge Add-ons hesabı | Tek seferlik geliştirici ücreti olabilir |
| GitHub gizli değişkenleri (imzalama anahtarı vb.) | Asla repoya koyma |
| Alan adı / küçük bir web sayfası (isteğe bağlı) | |
| Hukuki sorumluluk reddi onayı | Gerekirse bir uzmana danış |
| Beta kullanıcılarını bulma | Forum, Discord, Reddit, sosyal medya |
| VirusTotal / AV firması yanlış pozitif başvuruları | Ajan şablon hazırlar |
| Gerçek cihaz testleri (farklı Windows sürümleri, AV'ler) | Ajan senaryo listesini hazırlar |

---

## 17. Gerçekçi Beklentiler

- Yapay zeka ajanları kod yazabilir ama **gerçek dünyada test etmek** (farklı siteler, ağlar, AV'ler, sürümler) senin elindedir. Bu plan, testleri otomatikleştirmeyi öncelikli tutuyor ki ajanın hataları erken yakalansın.
- **Bütünlük testleri (P2-01, P2-02) senin en güçlü güvencen.** Bunlar yeşilse indirme motoruna güvenebilirsin; değilse yeni özellik eklemek kum üzerine bina yapmaktır.
- Güvenlik görevlerinde (Faz 3) ajanın çıktısını mümkünse **sen veya güvendiğin bir geliştirici** gözden geçirsin; özellikle P3-01, P3-02, P3-03.
- Kota kısıtlıysa: 🔴 görevleri güçlü modele, 🟢 görevleri hızlı modele ver; **önce plan ve denetim, sonra uygulama.** Denetimi (P0-04) en güçlü modelle bir kez iyi yaptırmak, sonraki tüm oturumlarda zaman kazandırır.
- Bu liste uzun. Hepsini bir anda yapmaya çalışma; **her faz sonunda çalışan, yayınlanabilir bir sürüm** hedefle (örn. Faz 2 sonunda 1.6, Faz 3 sonunda 1.7 …).

---

## 18. Hızlı Başlangıç Özeti (3 adım)

1. Bu dosyayı `docs/AJAN_PLANI.md` olarak repoya koy, commit at.
2. Antigravity'de **Bölüm 15.1** komutunu çalıştır → Faz 0 (denetim öncesi hazırlık).
3. Güçlü modelle **Bölüm 15.2** komutunu çalıştır → `DENETIM.md` çıksın, sonra plan gerçek bulgularla netleşsin.

Başarılar!
