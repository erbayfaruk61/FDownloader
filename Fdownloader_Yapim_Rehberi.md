# Kendi Internet Download Manager'ımı (IDM) Yapma Rehberi

> Bu doküman kod içermez. Amaç: Bir indirme yöneticisi yapmak için **neyin, neden ve hangi sırayla** yapılması gerektiğini eksiksiz anlatmaktır.

---

## İçindekiler

1. [Projenin Tanımı ve Hedefleri](#1-projenin-tanımı-ve-hedefleri)
2. [Temel Kavramlar (Önce Bunları Öğren)](#2-temel-kavramlar)
3. [Özellik Listesi (MVP → Gelişmiş)](#3-özellik-listesi)
4. [Teknoloji Seçimi](#4-teknoloji-seçimi)
5. [Genel Mimari](#5-genel-mimari)
6. [İndirme Motoru (Çekirdek)](#6-i̇ndirme-motoru-çekirdek)
7. [Kuyruk, Zamanlayıcı ve Hız Yönetimi](#7-kuyruk-zamanlayıcı-ve-hız-yönetimi)
8. [Veri Saklama ve Kalıcılık](#8-veri-saklama-ve-kalıcılık)
9. [Tarayıcı Entegrasyonu](#9-tarayıcı-entegrasyonu)
10. [Video / Medya Yakalama](#10-video--medya-yakalama)
11. [Kullanıcı Arayüzü](#11-kullanıcı-arayüzü)
12. [Ağ, Kimlik Doğrulama ve Proxy](#12-ağ-kimlik-doğrulama-ve-proxy)
13. [Hata Yönetimi ve Dayanıklılık](#13-hata-yönetimi-ve-dayanıklılık)
14. [Güvenlik](#14-güvenlik)
15. [Dosya Sistemi Konuları](#15-dosya-sistemi-konuları)
16. [Performans ve Kaynak Yönetimi](#16-performans-ve-kaynak-yönetimi)
17. [Test Stratejisi](#17-test-stratejisi)
18. [Paketleme, Dağıtım ve Güncelleme](#18-paketleme-dağıtım-ve-güncelleme)
19. [Yasal ve Etik Konular](#19-yasal-ve-etik-konular)
20. [Aşama Aşama Yol Haritası](#20-aşama-aşama-yol-haritası)
21. [Sık Yapılan Hatalar](#21-sık-yapılan-hatalar)
22. [Kontrol Listesi](#22-kontrol-listesi)

---

## 1. Projenin Tanımı ve Hedefleri

### 1.1 Bir indirme yöneticisi ne yapar?
Tarayıcının kendi indirme özelliğinden farkı şudur:

- Dosyayı **tek bağlantı yerine birden fazla paralel bağlantıyla** parçalara bölerek indirir (hız artışı).
- İndirme **yarıda kesilirse kaldığı yerden devam eder**.
- İndirmeleri **sıraya koyar, duraklatır, zamanlar**.
- Tarayıcıdaki indirme ve video bağlantılarını **otomatik yakalar**.
- İndirilen dosyaları **kategorize eder**, düzenler.

### 1.2 Proje hedeflerini baştan netleştir
Kendine şu soruları sor ve yazılı cevapla:

| Soru | Seçenekler |
|---|---|
| Hangi işletim sistemi? | Sadece Windows / Windows + macOS + Linux |
| Kimin için? | Sadece kendim / Açık kaynak / Ticari |
| Hangi protokoller? | Sadece HTTP(S) / + FTP / + Torrent / + HLS-DASH |
| Tarayıcı entegrasyonu şart mı? | Evet / Daha sonra |
| Arayüz tipi? | Masaüstü GUI / Web arayüzü / CLI |

> **Tavsiye:** Önce *sadece HTTP(S) + tek platform + basit arayüz* ile başla. Gerisi sonradan eklenir.

---

## 2. Temel Kavramlar

Koda başlamadan önce bunları iyi anlamalısın. Projenin %80'i bu bilgilere dayanır.

### 2.1 HTTP temelleri
- **İstek/yanıt modeli:** İstemci istek atar, sunucu durum kodu + başlıklar + gövde döner.
- **Önemli durum kodları:**
  - `200` – Tam içerik
  - `206` – Kısmi içerik (parçalı indirmenin kalbi)
  - `301/302/307/308` – Yönlendirme (takip etmelisin)
  - `401/407` – Kimlik doğrulama gerekli
  - `403` – Yasak (çoğu zaman eksik header/cookie)
  - `404` – Bulunamadı
  - `416` – İstenen aralık geçersiz
  - `429` – Çok fazla istek (yavaşlaman gerekir)
  - `5xx` – Sunucu hatası (yeniden denemelisin)
- **Önemli başlıklar (headers):**
  - `Content-Length` – Dosya boyutu
  - `Accept-Ranges` – Sunucu parçalı indirmeyi destekliyor mu (`bytes` ise evet)
  - `Content-Range` – Gelen parçanın hangi aralık olduğu
  - `Content-Disposition` – Sunucunun önerdiği dosya adı
  - `Content-Type` – Dosya türü
  - `ETag` / `Last-Modified` – Dosyanın değişip değişmediğini anlamak için
  - `Content-Encoding` – Sıkıştırma (gzip vb.), indirmelerde dikkat gerektirir
  - `User-Agent`, `Referer`, `Cookie` – Bazı sitelerde indirme için zorunlu
  - `Transfer-Encoding: chunked` – Boyutu önceden bilinmeyen akış

### 2.2 Parçalı (segmented) indirme mantığı
1. Dosya boyutunu öğren (HEAD isteği veya küçük bir Range isteği ile).
2. Sunucu aralık desteği veriyor mu kontrol et.
3. Dosyayı N eşit parçaya böl (ör. 8 parça).
4. Her parça için ayrı bir bağlantı aç, sadece o byte aralığını iste.
5. Gelen veriyi doğru konuma yaz.
6. Tüm parçalar bitince dosyanın bütünlüğünü doğrula.

### 2.3 Devam ettirme (resume) mantığı
- Her parçanın **ne kadarının indiği** kalıcı olarak saklanır.
- Uygulama kapanıp açılsa bile bu bilgi sayesinde kalınan yerden devam edilir.
- Devam etmeden önce **dosyanın sunucuda değişmediğini** (ETag / Last-Modified / boyut) doğrulamak gerekir.

### 2.4 Diğer kavramlar
- **Eşzamanlılık (concurrency):** Thread, async/await, event loop, worker pool.
- **Geri basınç (backpressure):** Ağ hızlı, disk yavaşsa belleğin şişmemesi için akışı yönetmek.
- **Checksum/Hash:** MD5, SHA-1, SHA-256 ile dosya doğrulama.
- **MIME türleri:** Dosyayı tanımak ve kategorize etmek için.
- **TLS/SSL:** HTTPS bağlantılarında sertifika doğrulama.

---

## 3. Özellik Listesi

### 3.1 MVP (Minimum Çalışan Ürün) – Zorunlu
- [ ] URL girip indirme başlatma
- [ ] Dosya adını ve boyutunu otomatik algılama
- [ ] Tek bağlantılı indirme (önce bunu sağlamlaştır)
- [ ] İlerleme çubuğu, hız, kalan süre
- [ ] Duraklat / Devam et / İptal
- [ ] Kaydedilecek klasör seçme
- [ ] İndirme listesi (geçmiş)

### 3.2 Çekirdek Özellikler
- [ ] Çok parçalı (segmented) paralel indirme
- [ ] Uygulama kapanıp açılınca devam etme (kalıcı resume)
- [ ] Yönlendirmeleri takip etme
- [ ] Otomatik yeniden deneme (retry)
- [ ] İndirme kuyruğu ve eşzamanlı indirme limiti
- [ ] Hız sınırlama (bandwidth limiter)
- [ ] Dosya türüne göre otomatik kategori / klasör
- [ ] Aynı isimli dosya çakışma yönetimi

### 3.3 Gelişmiş Özellikler
- [ ] Tarayıcı eklentisi ile otomatik yakalama
- [ ] Panodaki (clipboard) bağlantıyı algılama
- [ ] Zamanlayıcı (belirli saatte başlat/durdur)
- [ ] İndirme bitince bilgisayarı kapat / uyut / uygulama çalıştır
- [ ] Virüs tarayıcı entegrasyonu
- [ ] Toplu (batch) indirme: sayı aralığı, link listesi
- [ ] Site giriş bilgileri yöneticisi
- [ ] Proxy desteği
- [ ] HLS / DASH video indirme
- [ ] FTP / SFTP desteği
- [ ] Bildirimler ve ses
- [ ] Çoklu dil desteği
- [ ] Karanlık / açık tema

### 3.4 İleri Seviye (İsteğe Bağlı)
- [ ] Mirror/yedek kaynak kullanma (aynı dosya için birden fazla URL)
- [ ] Torrent / Magnet desteği
- [ ] Uzaktan kontrol (web arayüzü veya mobil)
- [ ] Bulut senkronizasyonu (liste ve ayarlar)
- [ ] Site grabber (bir sitedeki tüm dosyaları tarama)

---

## 4. Teknoloji Seçimi

Dil/çatı seçimi projeyi çok etkiler. Karar verirken şunlara bak: **ağ performansı, GUI kolaylığı, platform desteği, tarayıcı entegrasyon imkânı, kendi tecrüben.**

### 4.1 Dil seçenekleri

| Dil | Artıları | Eksileri |
|---|---|---|
| **Python** | Hızlı geliştirme, zengin kütüphane, öğrenmesi kolay | GUI paketleme zor, saf hız düşük (ama ağ bağımlı işte yeterli) |
| **C# (.NET)** | Windows'ta mükemmel GUI (WPF/WinUI), güçlü async, kolay paketleme | Cross-platform için ek çaba (Avalonia/MAUI) |
| **Rust** | Çok hızlı, güvenli, düşük bellek | Öğrenme eğrisi dik |
| **Go** | Eşzamanlılık çok kolay, tek binary | GUI ekosistemi zayıf (çekirdek için harika) |
| **Electron/Node.js** | Web teknolojisiyle GUI, tarayıcı eklentisiyle aynı dil | Bellek tüketimi yüksek |
| **Java/Kotlin** | Cross-platform, olgun ekosistem | Ağır his |
| **C++ (Qt)** | Performans + güçlü GUI | Geliştirme yavaş, karmaşık |

### 4.2 Önerilen kombinasyonlar
- **Yeni başlayan / hızlı prototip:** Python çekirdek + PySide/Qt arayüz
- **Windows odaklı ve profesyonel:** C# + WPF/WinUI
- **Çekirdek ayrı, arayüz ayrı (modern yaklaşım):** Rust veya Go çekirdek (arka plan servisi) + Electron/Tauri arayüz
- **En az dert:** Electron + Node.js (eklenti ile aynı dil olduğu için)

### 4.3 Hazır kütüphane mi, sıfırdan mı?
- HTTP istemcisi için **olgun bir kütüphane** kullan; ham soketlerle HTTP yazma.
- Ama **parça yönetimi, resume, kuyruk mantığını kendin yaz** — projenin özü bu.
- Alternatif: arka planda `aria2` gibi bir motor kullanıp sadece arayüz yazmak (hızlı yol, ama "kendi motorun" olmaz).

---

## 5. Genel Mimari

### 5.1 Katmanlı yapı

```
┌─────────────────────────────────────────┐
│  Arayüz (GUI / Tarayıcı Eklentisi)      │
├─────────────────────────────────────────┤
│  Uygulama Servisi (komutlar, olaylar)   │
├─────────────────────────────────────────┤
│  İndirme Yöneticisi (kuyruk, zamanlama) │
├─────────────────────────────────────────┤
│  İndirme Motoru (parçalar, bağlantılar) │
├─────────────────────────────────────────┤
│  Ağ Katmanı (HTTP, proxy, TLS, cookie)  │
├─────────────────────────────────────────┤
│  Depolama (dosya yazımı + veritabanı)   │
└─────────────────────────────────────────┘
```

### 5.2 Ana bileşenler ve sorumlulukları

| Bileşen | Sorumluluk |
|---|---|
| **Download Task** | Tek bir indirmeyi temsil eder: URL, durum, parçalar, ilerleme |
| **Segment (Parça)** | Dosyanın bir aralığı: başlangıç, bitiş, indirilen miktar, durum |
| **Segment Worker** | Bir parçayı indiren çalışan birim (thread/görev) |
| **Task Manager** | Tüm görevleri yönetir, kuyruk ve eşzamanlılık limitini uygular |
| **Scheduler** | Zamana bağlı başlat/durdur |
| **Speed Limiter** | Hız sınırlamasını tüm bağlantılar arasında dağıtır |
| **Storage / Repository** | Görev ve parça bilgilerini kalıcı saklar |
| **File Writer** | Gelen veriyi diske doğru konuma yazar |
| **Event Bus** | Bileşenler arası olay iletimi (ilerleme, hata, bitti) |
| **Config Manager** | Ayarları okur/yazar |
| **Browser Bridge** | Tarayıcı eklentisiyle haberleşir |
| **Notification Service** | Bildirimler |

### 5.3 Olay tabanlı (event-driven) tasarım
Arayüz, motoru doğrudan "yoklamamalı". Bunun yerine motor şu olayları yayınlamalı:
- Görev eklendi / başladı / duraklatıldı / devam etti
- İlerleme güncellendi (sıklığı sınırlı: saniyede 2-5 kez yeterli)
- Parça tamamlandı
- Hata oluştu
- Görev tamamlandı / iptal edildi

### 5.4 Görev durum makinesi (State Machine)
Her görevin net bir yaşam döngüsü olmalı:

```
Kuyrukta → Bağlanıyor → İndiriyor → (Duraklatıldı) → Birleştiriliyor → Doğrulanıyor → Tamamlandı
                 ↓           ↓
               Hata  ←――――――┘   → Yeniden deneniyor → İndiriyor
                 ↓
             Başarısız / İptal
```

Hangi durumdan hangi duruma geçilebileceğini **önceden tablo halinde yaz**. Birçok hata, yanlış durum geçişlerinden kaynaklanır.

### 5.5 İşlem modeli kararı
İki yaklaşım var:
1. **Tek süreç:** Arayüz ve motor aynı uygulamada. Basit.
2. **Servis + istemci:** Motor arka planda servis olarak çalışır; arayüz ve tarayıcı eklentisi ona yerel bir API ile bağlanır. Arayüz kapansa bile indirmeler sürer, tarayıcı entegrasyonu da kolaylaşır. **Uzun vadede bu daha iyidir.**

---

## 6. İndirme Motoru (Çekirdek)

Projenin kalbi. Adım adım ne olması gerektiği:

### 6.1 Ön inceleme (Probe) aşaması
Bir URL verildiğinde indirmeye başlamadan önce sunucudan bilgi topla:

1. Yönlendirmeleri takip et, **nihai URL'yi** bul.
2. Dosya boyutunu öğren.
3. Sunucu aralık (range) desteği veriyor mu kontrol et.
4. Dosya adını belirle (öncelik sırası):
   - `Content-Disposition` başlığı
   - Nihai URL'nin son kısmı
   - URL'deki sorgu parametreleri
   - Hiçbiri yoksa Content-Type'a göre uzantı üret
5. Dosya adını **temizle** (yasak karakterler, çok uzun isim, ayrılmış Windows isimleri).
6. ETag / Last-Modified değerlerini kaydet.
7. Bazı sunucular HEAD isteğini reddeder; **yedek yöntem:** küçük bir aralık isteği ile boyutu çıkarmak.

### 6.2 Parçalama stratejisi
- **Parça sayısı:** Genelde 4–16 arası. Çok fazla parça sunucuyu rahatsız eder ve bazı siteler engeller.
- **Küçük dosyalarda** parçalamaya değmez (örn. birkaç MB altı için tek bağlantı).
- **Boyut bilinmiyorsa** (chunked, canlı akış) parçalama yapılamaz → tek bağlantı.
- **Aralık desteği yoksa** → tek bağlantı, devam ettirme de mümkün değil (kullanıcıyı uyar).
- **Dinamik bölme (gelişmiş):** Bir parça işini bitirirse, hâlâ süren en büyük parçayı ikiye bölüp yardım et. Bu, "son %5'te bekleme" sorununu çözer.

### 6.3 Parça veri yapısı
Her parça için saklanacaklar:
- Parça numarası
- Başlangıç byte'ı, bitiş byte'ı
- Şimdiye kadar indirilen byte
- Durum (bekliyor / indiriyor / bitti / hata)
- Deneme sayısı

### 6.4 Veri yazma stratejileri
Üç ana yaklaşım var:

| Yaklaşım | Açıklama | Not |
|---|---|---|
| **Önceden yer ayırma + doğrudan konuma yazma** | Dosya tam boyutta oluşturulur, her worker kendi offset'ine yazar | En yaygın, en verimli |
| **Parça dosyaları + sonda birleştirme** | Her parça ayrı geçici dosya, sonunda birleştirilir | Basit, ama ek disk I/O ve ek yer ister |
| **Sıralı bellek tamponu** | Veriler sırayla yazılır | Paralellikle uyumsuz |

> **Öneri:** Önceden yer ayırma + offset'e yazma. Geçici uzantıyla (ör. `.part` / `.tmp`) çalış, bitince asıl isme taşı.

### 6.5 Okuma/yazma tamponlama
- Ağdan gelen veriyi **küçük parçalar (blok) halinde** oku, belleği şişirme.
- Diske yazarken çok küçük yazmalar yapma; **tampon biriktirip toplu yaz.**
- Disk yavaşsa ağ okumasını yavaşlat (geri basınç).

### 6.6 İlerleme takibi
- Toplam ilerleme = tüm parçalardaki indirilen byte toplamı.
- **Hız hesabı:** Anlık değil, **hareketli ortalama** (örn. son 3–5 saniye) kullan; yoksa ekran sürekli zıplar.
- **Kalan süre (ETA):** Kalan byte / yumuşatılmış hız.
- İlerleme güncellemesini sınırla (UI'yi boğmamak için).

### 6.7 Kesinti kaydı (checkpoint)
- İlerleme bilgisini **belirli aralıklarla** (örn. her birkaç saniyede veya her X MB'da) kalıcı depolamaya yaz.
- Uygulama çökerse en fazla birkaç saniyelik veri kaybı olmalı.
- Kaydedilen "indirildi" değeri, diske **gerçekten yazılmış** veriden fazla olmamalı (bu çok kritik; yoksa bozuk dosya oluşur).

### 6.8 Devam ettirme (resume) akışı
1. Kayıtlı görev bilgisini oku.
2. Geçici dosyanın hâlâ var ve boyutunun tutarlı olduğunu kontrol et.
3. Sunucuya yeniden ulaş; ETag/Last-Modified/boyut değişti mi bak.
4. Değişmişse: kullanıcıya sor veya baştan başla.
5. Değişmemişse: her parçayı kaldığı byte'tan itibaren yeniden iste.
6. Sunucu aralığı artık desteklemiyorsa: durumu bildir.

### 6.9 Birleştirme ve doğrulama
- Tüm parçalar bitince toplam byte'ın beklenen boyutla eşleştiğini doğrula.
- Kullanıcı hash (MD5/SHA) verdiyse karşılaştır.
- Geçici dosyayı hedef isme yeniden adlandır.
- Başarısızsa kullanıcıya net uyarı ver, dosyayı "bozuk" diye işaretle.

### 6.10 Sıkıştırma (Content-Encoding) tuzağı
Sunucu içeriği gzip ile sıkıştırarak gönderebilir. Bu, `Content-Length` ile gerçek dosya boyutunun uyuşmamasına yol açabilir. İndirme yöneticilerinde genelde **sıkıştırma istenmez** (dosyalar zaten sıkıştırılmıştır), böylece boyut ve aralık hesapları güvenilir kalır.

### 6.11 Bağlantı yönetimi
- Bağlantıları yeniden kullan (keep-alive) ama parçalar için ayrı bağlantılar şart.
- Bağlantı zaman aşımı, okuma zaman aşımı ve "veri akışı durdu" (stall) zaman aşımı **ayrı ayrı** ele alınmalı.
- Bir parça uzun süre veri alamıyorsa (stall) bağlantıyı kes ve yeniden dene.
- Sunucunun kişi başı bağlantı limiti olabilir; çok fazla bağlantı `429` veya `403`'e yol açar → parça sayısını otomatik düşüren **uyarlamalı** bir mantık düşün.

---

## 7. Kuyruk, Zamanlayıcı ve Hız Yönetimi

### 7.1 İndirme kuyruğu
- **Eşzamanlı indirme limiti** (örn. aynı anda 3 dosya).
- Fazlası "Kuyrukta" bekler; biri bitince sıradaki başlar.
- **Öncelik** sistemi: yüksek / normal / düşük.
- Kullanıcı sırayı sürükle-bırak ile değiştirebilsin.
- Birden fazla kuyruk (ör. "Ana kuyruk", "Gece kuyruğu") desteği opsiyonel.

### 7.2 Zamanlayıcı
- Belirli saatte başlat, belirli saatte durdur.
- Haftanın günlerine göre tekrarlı kurallar.
- Kuyruk bazlı zamanlama.
- Bitince eylem: kapat / uyut / hazırda beklet / programı çalıştır / hiçbir şey.
- Bilgisayar uykuya geçerse görevlerin durumunu doğru ele al (uyanınca devam).

### 7.3 Hız sınırlayıcı
- **Genel limit** (tüm indirmeler toplamı) ve **görev başı limit**.
- Yaygın algoritma mantığı: *token bucket* veya *leaky bucket* — belirli hızda "izin jetonu" üretilir, veri okumak için jeton gerekir.
- Limiti tüm parçalar/bağlantılar arasında **adil dağıt**.
- Çalışırken limiti değiştirme imkânı.

### 7.4 Ağ değişiklikleri
- İnternet kesilince görevleri otomatik duraklat ve bağlantı gelince devam ettir.
- Wi-Fi'dan kabloluya geçiş gibi durumlarda bağlantılar kopar; yeniden bağlanma mantığı olmalı.
- Ölçülü bağlantı (mobil hotspot) algılanırsa kullanıcıya sor veya otomatik duraklat (opsiyonel).

---

## 8. Veri Saklama ve Kalıcılık

### 8.1 Neyi saklamalısın?
- **Görevler:** URL, nihai URL, dosya adı, hedef klasör, boyut, durum, tarih, kategori, açıklama.
- **Parçalar:** Her görevin parça listesi ve ilerlemesi.
- **Header/Cookie bilgileri:** Referer, User-Agent, bazı sitelerde cookie (güvenli sakla).
- **Ayarlar:** Kullanıcı tercihleri.
- **Kimlik bilgileri:** Site giriş bilgileri (şifrelenmiş).
- **Geçmiş:** Tamamlanan indirmeler, istatistikler.
- **Zamanlayıcı kuralları ve kategoriler.**

### 8.2 Depolama seçenekleri

| Seçenek | Ne zaman? |
|---|---|
| **SQLite** | En iyi genel seçim: tek dosya, güvenilir, sorgulanabilir, çökmeye dayanıklı |
| **JSON dosyaları** | Çok basit prototip için; büyüyünce sorun çıkarır |
| **Gömülü anahtar-değer (LevelDB vb.)** | Özel durumlarda |

> **Öneri:** SQLite. Çökme güvenliği (transaction) sayesinde resume bilgisi bozulmaz.

### 8.3 Dikkat edilecekler
- Sık ilerleme güncellemelerini her seferinde diske yazma; **toplu/aralıklı** yaz.
- Veritabanı şeması için **sürümleme (migration)** planla; ileride alan eklemek zorunda kalırsın.
- Bozuk/yarım kayıtlara karşı açılışta **tutarlılık kontrolü** yap.
- Hassas verileri (şifre, cookie) işletim sisteminin güvenli deposunu (Credential Manager / Keychain / Secret Service) kullanarak sakla; düz metin tutma.

### 8.4 Ayarlar (Settings) listesi
- Varsayılan indirme klasörü, kategori klasörleri
- Parça sayısı (varsayılan, maksimum)
- Eşzamanlı indirme sayısı
- Hız limitleri
- Proxy ayarları
- Yeniden deneme sayısı ve aralığı
- Zaman aşımı değerleri
- Dosya çakışma davranışı (üzerine yaz / numaralandır / sor)
- Bildirim tercihleri
- Başlangıçta otomatik açılma
- Tema ve dil
- Tarayıcı yakalama kuralları (hangi dosya türleri, minimum boyut)

---

## 9. Tarayıcı Entegrasyonu

IDM'i "IDM yapan" şey bu entegrasyondur. En zor ama en değerli kısımlardan biri.

### 9.1 Neden gerekli?
Kullanıcı tarayıcıda bir bağlantıya tıkladığında indirmeyi senin uygulamanın devralması gerekir; üstelik oturum çerezleri, referer gibi bilgilerle birlikte.

### 9.2 Yaklaşım: Tarayıcı eklentisi (extension)
Modern ve doğru yöntem budur. Eklentinin yapacakları:

1. **İndirme olaylarını yakalamak:** Tarayıcı bir indirme başlatacağı an eklenti bunu fark eder.
2. **Tarayıcının kendi indirmesini iptal etmek.**
3. **Bilgileri toplamak:** URL, Referer, çerezler, User-Agent, dosya adı, boyut, tür.
4. **Bu bilgileri senin uygulamana iletmek.**
5. **Sağ tık menüsüne** "Bu bağlantıyı indir" eklemek.
6. **Sayfadaki medya/bağlantıları listelemek** (video yakalama için).
7. **Araç çubuğu simgesi** ve açılır panel.
8. **Yakalama kuralları:** Hangi uzantılar/boyutlar yakalansın, hangi siteler hariç.
9. **Kullanıcı Alt tuşu vb. ile yakalamayı atlayabilsin.**

### 9.3 Eklenti ↔ uygulama haberleşme yöntemleri

| Yöntem | Açıklama | Değerlendirme |
|---|---|---|
| **Native Messaging** | Tarayıcının resmi mekanizması: eklenti, kayıtlı yerel bir programla konuşur | **En güvenli ve önerilen** |
| **Yerel HTTP/WebSocket sunucusu** | Uygulama localhost'ta dinler, eklenti oraya istek atar | Kolay ama güvenlik önlemi şart (token, origin kontrolü) |
| **Özel protokol (URI scheme)** | `myidm://...` bağlantıları | Tek yönlü, sınırlı |

> **Öneri:** Native Messaging. Kullanıyorsan "native host" kayıt dosyası, izin verilen eklenti kimliği ve kurulum sırasında kayıt (Windows'ta kayıt defteri) gibi işleri kurulum aşamasında planla.

### 9.4 Desteklenecek tarayıcılar
- **Chromium tabanlılar** (Chrome, Edge, Brave, Opera, Vivaldi) — aynı eklenti mimarisi, tek geliştirme
- **Firefox** — benzer ama bazı farklar var (manifest ve API uyumluluğu)
- **Safari** — ayrı ve zor; en sona bırak

### 9.5 Eklenti yayını
- Chrome Web Store / Edge Add-ons / Firefox Add-ons için **hesap, inceleme süreci, gizlilik politikası** gerekir.
- Geliştirme sırasında "paketlenmemiş eklenti" olarak yükleyip test edebilirsin.
- İzinleri (permissions) **minimumda tut**; gereksiz geniş izin, inceleme reddine ve güven kaybına yol açar.

### 9.6 Alternatif/ek yöntemler
- **Pano izleme:** Kopyalanan URL otomatik algılanıp "indirilsin mi?" diye sorulur.
- **Sürükle-bırak:** Bağlantıyı uygulamaya/yüzen "bırakma kutusuna" bırakmak.
- **Dosya ilişkilendirme / komut satırı argümanı:** Dışarıdan URL vererek uygulamayı çağırma.
- **Sistem genelinde ağ yakalama (proxy/sürücü seviyesi):** Çok karmaşık, güvenlik/AV sorunları var; **önermem.**

### 9.7 Çerez ve oturum devri
Birçok indirme, giriş yapılmış oturum gerektirir. Eklenti ilgili siteye ait çerezleri ve başlıkları uygulamaya iletmeli; uygulama aynı kimlikle indirmeyi yapmalı. Bu verilerin güvenli taşınması ve saklanması çok önemli.

---

## 10. Video / Medya Yakalama

### 10.1 Düz dosya videolar
Doğrudan bir `.mp4`, `.webm` bağlantısı varsa normal indirme gibidir; sadece yakalama işi gerekir.

### 10.2 Akış (streaming) protokolleri
Çoğu modern site video sunmak için akış kullanır:

- **HLS (`.m3u8`):** Bir oynatma listesi (playlist) birçok küçük video parçasına işaret eder. Yapılacaklar:
  1. Ana playlist'i indir ve çözümle.
  2. Kalite seçenekleri (varyantlar) varsa kullanıcıya sun.
  3. Seçilen kalitenin parça listesini çöz.
  4. Tüm parçaları (genelde yüzlerce) paralel indir.
  5. Şifreli ise (AES-128 gibi) anahtarı al ve çöz.
  6. Parçaları sırayla birleştir.
  7. Gerekirse standart bir video kapsayıcıya (mp4) dönüştür.
- **DASH (`.mpd`):** Benzer mantık; ayrı video ve ses akışları olur, **sonunda birleştirmek (muxing)** gerekir.
- **Canlı yayınlar:** Playlist sürekli güncellenir; kayıt mantığı ayrı ele alınır.

### 10.3 Birleştirme/dönüştürme için araç
Video/ses birleştirme ve format dönüştürme işinde **ffmpeg** gibi olgun bir araç kullanmak mantıklıdır (sıfırdan yazmak gereksiz). Lisansını ve dağıtım şeklini (ayrı kurulum / paketle birlikte verme) önceden değerlendir.

### 10.4 Medya algılama yöntemi
Eklenti, sayfanın yaptığı **ağ isteklerini izleyerek** medya türündeki yanıtları (video/ses Content-Type'ı, `.m3u8`, `.mpd` uzantıları) tespit eder ve kullanıcıya "Bu videoyu indir" düğmesi sunar.

### 10.5 Dikkat edilecek zorluklar
- **DRM korumalı içerikler** (Widevine, FairPlay vb.) indirilemez ve indirilmeye çalışılmamalıdır. Hem teknik hem hukuki olarak sınırın dışındadır.
- Süreli/imzalı URL'ler: Bağlantı kısa sürede geçersiz olabilir; hızlı davranmak gerekir.
- Siteye özel koruma önlemleri (referer, token) olabilir.
- Platformların kullanım şartları farklı olabilir (Bölüm 19'a bak).

---

## 11. Kullanıcı Arayüzü

### 11.1 Ana pencere bileşenleri
- **Araç çubuğu:** Ekle, Başlat, Duraklat, Sil, Ayarlar, Zamanlayıcı.
- **Sol panel (kategoriler):** Tümü, Devam eden, Tamamlanan, Kuyruktakiler, Hatalılar; dosya türü kategorileri (Video, Müzik, Belge, Sıkıştırılmış, Program).
- **İndirme listesi (tablo):** Ad, boyut, ilerleme, hız, kalan süre, durum, tarih, ekleme zamanı, URL.
- **Alt bilgi çubuğu:** Toplam hız, aktif indirme sayısı.

### 11.2 Ekranlar/pencereler
1. **Yeni indirme ekle:** URL kutusu, kayıt yeri, dosya adı, kategori, açıklama, kimlik bilgisi, "şimdi başlat / kuyruğa ekle".
2. **İndirme detayı:** İlerleme çubuğu, **parça haritası** (her parçanın durumu görsel olarak), hız grafiği, bağlantı bilgileri, hata günlüğü.
3. **Toplu indirme:** Link listesi yapıştırma, desen/aralık oluşturma.
4. **Ayarlar:** Bölüm 8.4'teki başlıklar sekmeler halinde.
5. **Zamanlayıcı penceresi.**
6. **Site giriş bilgileri yöneticisi.**
7. **Hakkında / günlük (log) görüntüleyici.**
8. **İlk kurulum sihirbazı:** Klasör seçimi, tarayıcı entegrasyonu kurulumu.

### 11.3 Sistem tepsisi (System Tray)
- Pencere kapatılınca arka planda çalışmaya devam etsin.
- Tepsi menüsü: Pencereyi aç, Tümünü duraklat/devam, Hız limiti, Çıkış.
- İndirme bitince bildirim (tıklayınca dosyayı/klasörü aç).

### 11.4 Kullanıcı deneyimi ilkeleri
- İlerleme güncellemelerini **akıcı ama hafif** tut.
- Hataları **anlaşılır mesajlarla** göster (teknik kod + insan diliyle açıklama + öneri).
- Geri alınamaz eylemlerde (silme) onay sor; "dosyayı da sil" seçeneği ayrı olsun.
- Büyük listelerde (binlerce kayıt) **sanallaştırılmış liste** kullan; yoksa arayüz yavaşlar.
- Klavye kısayolları, sürükle-bırak, çoklu seçim.
- Erişilebilirlik: Yüksek kontrast, ekran okuyucu uyumu, ölçeklenebilir yazı tipi.
- Karanlık/açık tema, çoklu dil altyapısını **baştan** kur (sonradan eklemek zordur).

### 11.5 Arayüz yaklaşımı seçenekleri
- **Yerel masaüstü GUI** (Qt, WPF, WinUI, GTK)
- **Web teknolojili masaüstü** (Electron, Tauri)
- **Arka plan servisi + web arayüzü** (tarayıcıdan yönetim; uzaktan erişim kolay)

---

## 12. Ağ, Kimlik Doğrulama ve Proxy

### 12.1 Protokol desteği
- **HTTP/1.1** (zorunlu), **HTTP/2** (faydalı; ama çoklu bağlantı mantığı farklı çalışır, dikkat), HTTP/3 (opsiyonel).
- **HTTPS/TLS:** Sertifika doğrulama **varsayılan açık** olmalı. Kullanıcı bilinçli şekilde "güvensiz sertifikayı kabul et" diyebilir ama bu açıkça uyarılmalı.
- **FTP/FTPS/SFTP:** Ayrı protokol mantığı; eklenecekse ayrı modül olarak tasarla.

### 12.2 Kimlik doğrulama
- **Basic / Digest / NTLM / Bearer** gibi şemalar.
- Sunucu `401` döndüğünde kullanıcıdan bilgi isteme ve **site bazlı** saklama.
- Form tabanlı girişler (çerez gerektiren siteler) için eklenti üzerinden oturum devri.

### 12.3 Proxy desteği
- HTTP, HTTPS, SOCKS4/5 proxy.
- Sistem proxy ayarını otomatik kullanma + manuel proxy.
- Proxy kimlik doğrulaması.
- Site/alan adına göre proxy kuralları (opsiyonel).
- Proxy ile TLS (CONNECT) davranışlarına dikkat.

### 12.4 İstek başlıkları yönetimi
- **User-Agent:** Varsayılan bir tarayıcı gibi; site bazlı özelleştirme.
- **Referer:** Eklentiden gelen değer korunmalı; birçok site Referer'sız indirmeyi reddeder.
- **Cookie:** Güvenli şekilde sakla ve ilgili alan adına gönder.
- Gereksiz kişisel veri sızdırma.

### 12.5 DNS ve IPv6
- DNS çözümleme önbelleği ve hataya dayanıklılık.
- IPv4/IPv6 birlikte çalışma (Happy Eyeballs mantığı).
- Bir alan adının birden fazla IP'si varsa bağlantıları farklı IP'lere dağıtma (gelişmiş).

### 12.6 Mirror (yedek kaynak) desteği
Aynı dosya için birden fazla URL verilirse parçaları farklı kaynaklardan çekmek veya biri çökünce diğerine geçmek mümkün. Doğrulama için hash şart olur.

---

## 13. Hata Yönetimi ve Dayanıklılık

Gerçek dünyada indirme yöneticisini değerli kılan şey, **hatalarla başa çıkabilmesi**dir.

### 13.1 Hata sınıfları ve tepkiler

| Hata | Örnek | Tepki |
|---|---|---|
| **Geçici ağ hatası** | Bağlantı koptu, zaman aşımı, DNS hatası | Bekleyip yeniden dene (artan bekleme süresiyle) |
| **Sunucu geçici hata** | 500, 502, 503, 504 | Yeniden dene |
| **Hız sınırı** | 429 | `Retry-After`'a uy; parça sayısını düşür |
| **Yetki/oturum** | 401, 403 | Kimlik bilgisi iste; URL süresi dolmuş olabilir → yenile |
| **Kalıcı hata** | 404, 410 | Başarısız say, kullanıcıya bildir |
| **Aralık hatası** | 416, beklenmeyen 200 | Aralık desteğini yeniden değerlendir, gerekirse tek bağlantıya düş |
| **Dosya değişti** | ETag farkı | Kullanıcıya sor / baştan başla |
| **Disk hatası** | Disk dolu, izin yok, yol bulunamadı | Duraklat, net mesaj ver |
| **Bütünlük hatası** | Boyut/hash uyuşmuyor | Etkilenen parçaları yeniden indir veya başarısız say |

### 13.2 Yeniden deneme stratejisi
- **Üstel geri çekilme (exponential backoff)** + rastgele sapma (jitter): bekleme süresi her denemede artar.
- Maksimum deneme sayısı ve toplam süre sınırı.
- Her parça için **ayrı** sayaç.
- Sürekli aynı hatayı veren parçada parça sayısını/ayarları düşür.

### 13.3 Kendini iyileştirme
- Takılan (stall) bağlantıyı tespit et ve yenile.
- Çok yavaş parçayı daha hızlı bir bağlantıyla değiştir (dinamik bölme).
- Uygulama beklenmedik kapansa bile açılışta tüm "yarım" görevleri güvenli duruma getir.

### 13.4 Günlükleme (Logging)
- Seviyeli log (hata, uyarı, bilgi, ayrıntı).
- Görev bazlı log: kullanıcı "neden başarısız oldu" diye görebilsin.
- Log dosyalarını döndür (rotation), boyutunu sınırla.
- Gizli verileri (şifre, token, cookie) loglara **yazma** veya maskele.

### 13.5 Kenar durumlar (edge cases) listesi
- Dosya adı çakışması ve çok uzun dosya adı
- Yasak karakter, Unicode ve emoji içeren adlar
- 4 GB üstü dosyalar (FAT32 sınırı), çok büyük dosyalar (100 GB+)
- Boyutu belirsiz dosyalar
- Sunucu `Content-Length` yalan söylüyor
- Yönlendirme döngüleri
- Aynı URL'nin ikinci kez eklenmesi (tekrar kontrolü)
- Hedef klasör silindi / sürücü çıkarıldı
- Dosya indirilirken kullanıcı tarafından silindi/taşındı
- Bilgisayar uykuya girdi / kapandı / pil bitti
- Antivirüs yazılımının geçici dosyayı kilitlemesi
- Sistem saati değişti

---

## 14. Güvenlik

İndirme yöneticisi, internetten dosya alan ve tarayıcıyla konuşan bir yazılımdır; **güvenlik çok önemlidir.**

### 14.1 Dosya güvenliği
- **Yol geçişi (path traversal):** Sunucudan gelen dosya adı `../` gibi ifadeler içerebilir; hedef klasörün dışına yazmaya çalışabilir. **Her zaman temizle ve doğrula.**
- **Tehlikeli uzantılar:** `.exe`, `.bat`, `.scr`, `.js` vb. için kullanıcıyı uyar; otomatik çalıştırma yapma.
- **Mark of the Web / karantina işareti:** İşletim sisteminin "internetten indirildi" işaretini (Windows'ta Zone Identifier) dosyaya ekle; böylece SmartScreen/AV koruması devreye girer.
- **Virüs tarama entegrasyonu:** İndirme sonrası yüklü AV'yi tetikleyebilme (opsiyonel).
- **Disk dolma saldırıları:** Sunucu sonsuz veri gönderebilir; beklenen boyutu aşan veriyi kes.

### 14.2 Ağ güvenliği
- TLS sertifika doğrulaması açık olsun.
- HTTPS'ten HTTP'ye düşüşe (downgrade) dikkat.
- Yönlendirmelerde kimlik bilgisi/çerezlerin **başka alan adına sızmaması**.
- Yönlendirme sayısına üst sınır.

### 14.3 Tarayıcı köprüsü güvenliği
- Yerel sunucu kullanıyorsan: sadece `127.0.0.1`'e bağlan, rastgele bir **gizli anahtar/token** iste, `Origin` başlığını doğrula, rastgele web sitelerinin uygulamana komut göndermesini engelle.
- Native Messaging'de sadece belirli eklenti kimliğine izin ver.
- Eklentiden gelen **her veriyi güvenilmez kabul et** ve doğrula (URL şeması, dosya adı, yol).
- Yalnızca `http/https/ftp` gibi beklenen şemaları kabul et; `file://` gibi şemaları engelle.

### 14.4 Gizli veri saklama
- Şifre ve token'ları OS'in güvenli deposunda tut.
- Çerezleri uzun süre saklama; gerekli süre bitince sil.
- Ayar/DB dosyalarında hassas veriyi düz metin bırakma.

### 14.5 Güncelleme güvenliği
- Güncellemeleri **imzalı** ve **HTTPS** üzerinden dağıt.
- Yükleyiciyi/uygulamayı **kod imzalama sertifikasıyla** imzala (aksi halde SmartScreen/AV uyarıları verir).

### 14.6 Gizlilik
- Kullanıcının indirme geçmişini üçüncü taraflara gönderme.
- Telemetri varsa açıkça bildir ve **kapatılabilir** yap.
- Gizlilik politikası hazırla (özellikle eklenti mağazaları için zorunlu).

---

## 15. Dosya Sistemi Konuları

### 15.1 Dosya adı işlemleri
- Yasak karakterleri (Windows: `\ / : * ? " < > |`) değiştir.
- Ayrılmış isimleri (CON, PRN, AUX, NUL, COM1…) engelle.
- Sonundaki nokta/boşlukları temizle.
- Maksimum yol uzunluğu (Windows'ta klasik 260 karakter sınırı) için önlem.
- Unicode normalleştirme (özellikle macOS ile uyumluluk).
- Dosya adı URL kodlamasından (%20 gibi) çözülmeli; karakter kodlaması (UTF-8 vb.) doğru işlenmeli.

### 15.2 Çakışma politikası
Aynı isimde dosya varsa kullanıcı tercihi: **üzerine yaz / otomatik numaralandır (dosya (1).zip) / sor / atla.**

### 15.3 Disk alanı
- İndirmeye başlamadan **boş alan kontrolü**.
- Önceden yer ayırma (preallocation) yapıyorsan, ayırma başarısız olursa hata ver.
- İndirme sırasında boş alan azalırsa duraklat ve uyar.
- Geçici dosyalar için ayrı klasör seçeneği (örn. hızlı SSD).

### 15.4 Dosya sistemi özellikleri
- FAT32'de 4 GB sınırı; kullanıcıyı uyar.
- Ağ sürücüleri ve harici disklerin davranışı farklı olabilir (yavaş, kopabilir).
- Seyrek dosya (sparse file) ve önceden ayırma farkını bil: Önceden ayırma, parçalı yazma sırasında parçalanmayı (fragmentation) azaltır.

### 15.5 Tamamlanınca
- Geçici dosyayı **atomik** şekilde yeniden adlandır.
- Dosya tarihini sunucudaki `Last-Modified` değerine ayarlama seçeneği.
- "Dosyayı aç / klasörde göster" eylemleri.
- Tamamlanan dosyayı kategoriye göre otomatik klasöre taşıma.

---

## 16. Performans ve Kaynak Yönetimi

### 16.1 Hedefler
- Bant genişliğini **doldurabilmek** (ağ darboğazı, uygulama değil).
- Düşük CPU ve bellek kullanımı.
- Çok sayıda eşzamanlı bağlantıda kararlılık.
- Arayüzün donmaması.

### 16.2 Bellek
- Dosya içeriğini asla belleğe komple yükleme; **akış (stream)** halinde işle.
- Tampon boyutlarını ve parça başına tampon sayısını sınırla.
- Arayüzde büyük listeler için sanallaştırma.

### 16.3 Eşzamanlılık modeli
- **Async I/O** (olay döngüsü) veya **worker havuzu** — ağ ağırlıklı işler için async genelde daha ölçeklenir.
- Disk yazma işlemini ağ okumadan ayır (ayrı kuyruk/iş parçacığı).
- Arayüz iş parçacığını asla bloklama.

### 16.4 Optimum parça sayısı
- Daha çok parça her zaman daha hızlı değil: sunucu limiti, ISS kısıtı, disk aramaları (özellikle HDD'de) performansı düşürebilir.
- Varsayılanı 8 civarı tut; kullanıcı ayarlasın; **uyarlamalı** öğrenme opsiyonel.

### 16.5 Güç yönetimi
- İndirme sürerken bilgisayarın uykuya geçmesini engelleme seçeneği.
- Dizüstüde pil durumuna göre davranış (opsiyonel).

### 16.6 Ölçümleme
- Kendi motorunu `aria2`, tarayıcı indirmesi ve diğer yöneticilerle aynı dosya/ağda **karşılaştır.**
- Hız, CPU, bellek, bağlantı sayısını raporlayan bir tanılama ekranı yap.

---

## 17. Test Stratejisi

### 17.1 Birim testleri
- Dosya adı temizleme / çıkarma mantığı
- Parça bölme hesapları (başlangıç/bitiş aralıkları, son parça, tek byte'lık dosya, tam bölünmeyen boyutlar)
- Durum makinesi geçişleri
- Yeniden deneme/bekleme hesapları
- Hız sınırlayıcı mantığı
- URL ve başlık ayrıştırma

### 17.2 Entegrasyon testleri
- **Kendi test sunucunu kur:** Aşağıdaki davranışları taklit edebilen basit bir yerel sunucu:
  - Normal ve aralık destekli/desteksiz yanıtlar
  - Yavaş yanıt, yarıda kesilen bağlantı
  - Yönlendirmeler (zincir ve döngü)
  - 4xx/5xx hataları, rastgele hata enjeksiyonu
  - Yanlış `Content-Length`
  - Çok büyük dosya akışı
  - `Content-Disposition` farklı biçimleri
  - Kimlik doğrulama isteyen uç noktalar
- Aynı dosyanın farklı parça sayılarıyla indirilip **hash karşılaştırması** yapılması en önemli doğrulamadır.

### 17.3 Dayanıklılık/kaos testleri
- İndirme ortasında uygulamayı **zorla kapat**, aç, devam ettir; hash doğru mu?
- Ağı kes/bağla, Wi-Fi değiştir.
- Diski doldur.
- Bilgisayarı uyut/uyandır.
- Binlerce görevli listeyle dene.

### 17.4 Arayüz ve eklenti testleri
- Farklı tarayıcı ve sürümlerde eklenti testi.
- Farklı ekran ölçekleri (DPI) ve diller.
- Yüksek kontrast, klavye ile kullanım.
- Native messaging kurulum/kaldırma senaryoları.

### 17.5 Gerçek dünya testleri
- Farklı büyüklük ve türde gerçek dosyalar (küçük, 1 GB+, 10 GB+).
- Çeşitli CDN'ler ve hosting servisleri.
- Çerez gerektiren, yönlendirmeli, süreli bağlantılı siteler.
- Farklı işletim sistemi sürümleri ve antivirüs yazılımlarıyla.

### 17.6 Sürekli entegrasyon (CI)
- Her değişiklikte otomatik testleri çalıştır.
- Çoklu platform derlemeleri.
- Kod kalitesi ve güvenlik taramaları (bağımlılık açıkları).

---

## 18. Paketleme, Dağıtım ve Güncelleme

### 18.1 Yükleyici
- Windows: MSI / MSIX / Inno Setup / NSIS gibi araçlar.
- macOS: `.dmg` / `.pkg`, **noter onayı (notarization)** ve imzalama.
- Linux: AppImage, `.deb`, `.rpm`, Flatpak, Snap.
- Yükleyicinin yapacakları: Dosyaları kopyala, kısayollar oluştur, **native messaging kaydını yap**, başlangıçta çalıştırma seçeneği, kaldırırken temizlik.

### 18.2 Kod imzalama
İmzasız yazılım "tanınmayan yayıncı" uyarıları alır ve antivirüs tarafından şüpheli görülür. **Kod imzalama sertifikası** edinmeyi bütçene ekle.

### 18.3 Otomatik güncelleme
- Sürüm kontrol mekanizması (arka planda, kullanıcıyı rahatsız etmeden).
- İmzalı güncelleme paketleri.
- Delta güncelleme (opsiyonel).
- Geri alma planı: Kötü güncelleme çıkarsa önceki sürüme dönüş.
- Veritabanı/ayar uyumluluğu (migration).

### 18.4 Sürümleme
- Anlamsal sürümleme (örn. 1.2.3).
- Değişiklik günlüğü (changelog).
- Kararlı / beta kanalları (opsiyonel).

### 18.5 Antivirüs yanlış alarmları
Yeni ve imzasız indirme/ağ yazılımları sık sık yanlış pozitif alır. Önlemler: imzalama, bilinen AV firmalarına yanlış pozitif bildirimi, açık kaynak şeffaflığı, temiz yükleyici (gizli ek yazılım içermeyen).

### 18.6 Dokümantasyon ve destek
- Kullanıcı kılavuzu, SSS.
- Hata raporlama kanalı (GitHub Issues, e-posta).
- Tanılama paketini (log) kullanıcının kolayca dışa aktarabilmesi.

---

## 19. Yasal ve Etik Konular

> Bu bölüm hukuki tavsiye değildir; sadece dikkat edilmesi gereken başlıkları listeler. Ciddi bir dağıtım planlıyorsan bir uzmana danış.

- **Telif hakkı:** Aracın kendisi yasaldır, ama kullanıcıların izinsiz içerik indirmesi onları ilgilendirir. Yine de, **korumalı içeriklerin (DRM) aşılmasına yönelik özellik eklememeyi** kesin ilke edin.
- **Platform kullanım şartları:** Bazı sitelerin/servislerin (özellikle video platformlarının) şartları indirmeyi yasaklayabilir. Bu tür platformlara özel "indirici" özellikleri hukuki risk doğurabilir; özellikle ticari dağıtımda dikkat.
- **Üçüncü taraf bileşen lisansları:** Kullandığın kütüphanelerin (ffmpeg dahil) lisanslarına uy (GPL/LGPL/MIT/Apache farkları).
- **Marka:** "IDM" / "Internet Download Manager" tescilli bir markadır. **Kendi ürününe farklı bir isim ver**; logo ve arayüz taklidinden kaçın.
- **Gizlilik mevzuatı:** KVKK / GDPR; kullanıcı verisi (indirme geçmişi, çerezler) toplanıyorsa ve bulutta tutuluyorsa gereklilikler doğar.
- **Eklenti mağazası politikaları:** Veri toplama, izinler ve tek amaç ilkeleri.
- **Lisans seçimi:** Açık kaynak yapacaksan lisans seç (MIT, Apache-2.0, GPL…). Ticari yapacaksan kullanıcı sözleşmesi hazırla.

---

## 20. Aşama Aşama Yol Haritası

### Aşama 0 — Hazırlık (1 hafta)
- Hedef, platform ve teknoloji kararları (Bölüm 1 ve 4).
- Sürüm kontrol sistemi (Git) ve proje yapısı.
- HTTP, Range istekleri ve eşzamanlılık konusunda araştırma/okuma.
- Küçük deneyler: Bir dosyayı indir, bir aralığı indir, hız ölç.
- Yerel test sunucusunu kurmaya başla (Bölüm 17.2).

### Aşama 1 — Temel Motor (2–3 hafta)
- URL ayrıştırma, yönlendirme takibi, probe aşaması.
- Tek bağlantılı indirme + ilerleme olayları.
- Dosya adı belirleme/temizleme.
- Geçici dosya + atomik yeniden adlandırma.
- Duraklat/iptal.
- Basit komut satırı arayüzü ile uçtan uca çalıştır.
- **Çıktı:** Güvenilir tek bağlantılı indirici.

### Aşama 2 — Çok Parçalı İndirme ve Resume (3–4 hafta)
- Parça veri modeli, bölme algoritması.
- Paralel worker'lar, offset'e yazma.
- İlerleme birleştirme ve hız hesabı.
- Kalıcı depolama (SQLite) ve checkpoint.
- Resume + dosya değişim kontrolü.
- Hata sınıflandırma ve yeniden deneme.
- Hash doğrulama testleri.
- **Çıktı:** Çökmeye dayanıklı, devam ettirebilen çok parçalı motor.

### Aşama 3 — Yönetici Katmanı (2 hafta)
- Görev durum makinesi.
- Kuyruk, eşzamanlılık limiti, öncelik.
- Hız sınırlayıcı.
- Ayarlar altyapısı.
- Olay yayını (event bus).
- **Çıktı:** Birden fazla indirmeyi yöneten servis.

### Aşama 4 — Masaüstü Arayüzü (3–4 hafta)
- Ana pencere, liste, araç çubuğu, kategoriler.
- Ekle/ayrıntı/ayarlar pencereleri.
- Sistem tepsisi, bildirimler.
- Parça haritası ve hız grafiği.
- Tema ve çoklu dil altyapısı.
- **Çıktı:** Günlük kullanılabilir uygulama (v0.1 – kendi kullanımın için yeter).

### Aşama 5 — Tarayıcı Entegrasyonu (3–4 hafta)
- Eklenti iskeleti (Chromium).
- İndirme yakalama + bilgi aktarımı.
- Native Messaging kurulumu.
- Sağ tık menüsü ve yakalama kuralları.
- Çerez/Referer aktarımı.
- Pano izleme.
- Firefox uyarlaması.
- **Çıktı:** Tarayıcıdan tıklayınca devralan IDM deneyimi.

### Aşama 6 — Gelişmiş Özellikler (4+ hafta)
- Zamanlayıcı ve bitiş eylemleri.
- Proxy, kimlik doğrulama, site giriş yöneticisi.
- Toplu indirme.
- HLS/DASH video yakalama ve birleştirme.
- Dinamik bölme, uyarlamalı parça sayısı.
- Mirror desteği, FTP/SFTP (isteğe bağlı).

### Aşama 7 — Sağlamlaştırma ve Yayın (3–4 hafta)
- Kapsamlı test ve kaos testleri.
- Güvenlik gözden geçirmesi (Bölüm 14).
- Performans iyileştirme.
- Yükleyici, kod imzalama, otomatik güncelleme.
- Dokümantasyon, web sayfası.
- Beta kullanıcılarıyla test.
- **Çıktı:** v1.0 yayını.

### Aşama 8 — Sonrası
- Geri bildirim toplama, hata düzeltme.
- Yeni platformlar (macOS/Linux), mobil yardımcı.
- Uzaktan kontrol/web arayüzü.
- Torrent, bulut senkronizasyonu gibi büyük özellikler.

> **Toplam tahmini süre:** Tek geliştirici, yarı zamanlı için 4–8 ay; tam zamanlı için 2–4 ay. Süreler tecrübe ve kapsama göre büyük oranda değişir.

---

## 21. Sık Yapılan Hatalar

1. **Her şeyi bir anda yapmaya çalışmak.** Önce tek bağlantı, sonra parçalama, sonra eklenti.
2. **Resume'u sona bırakmak.** Veri modelini baştan resume'a göre tasarla.
3. **"İndirildi" bilgisini diske yazılmadan kaydetmek.** Çökmede bozuk dosya üretir.
4. **Hash doğrulaması yapmadan "çalışıyor" demek.** Bozuk dosyalar sessizce oluşabilir.
5. **Çok fazla parça açmak.** Sunucu engeller, performans düşer.
6. **Arayüz iş parçacığında ağ/disk işi yapmak.** Arayüz donar.
7. **Dosya adını temizlememek.** Güvenlik açığı ve çökmeler.
8. **Hataları tek tipmiş gibi ele almak.** Her hata türü için farklı tepki gerekir.
9. **Eklenti–uygulama köprüsünü güvensiz bırakmak.** Her web sitesi bilgisayarına komut gönderebilir.
10. **İzinleri geniş tutmak (eklenti).** Mağaza reddi ve güven kaybı.
11. **Test sunucusu olmadan geliştirmek.** Gerçek sitelere bağımlı, tekrarlanamaz testler.
12. **Sıkıştırma (Content-Encoding) ve `Content-Length` uyumsuzluğunu unutmak.**
13. **Sunucunun Range desteğini varsaymak.** Her zaman doğrula.
14. **DRM'li içeriği indirmeye çalışmak.** Teknik olarak imkânsız ve hukuken sorunlu.
15. **Başka bir ürünün adını/arayüzünü kopyalamak.** Marka ihlali.
16. **İlerleme güncellemelerini sınırlamamak.** Arayüz ve veritabanı boğulur.
17. **Windows yol uzunluğu ve ayrılmış isimleri unutmak.**
18. **Kod imzalamayı düşünmemek.** Kullanıcıya "virüs" gibi görünür.

---

## 22. Kontrol Listesi

### Çekirdek
- [ ] URL probe (boyut, range, ad, ETag)
- [ ] Yönlendirme takibi (döngü koruması)
- [ ] Tek bağlantılı indirme
- [ ] Çok parçalı indirme
- [ ] Offset'e güvenli yazma, önceden yer ayırma
- [ ] Kalıcı checkpoint ve resume
- [ ] Dosya değişim kontrolü
- [ ] Hash/boyut doğrulama
- [ ] Hata sınıflandırma + backoff'lu yeniden deneme
- [ ] Stall (takılma) tespiti
- [ ] Dinamik parça bölme

### Yönetim
- [ ] Durum makinesi
- [ ] Kuyruk + öncelik + eşzamanlılık limiti
- [ ] Hız sınırlayıcı
- [ ] Zamanlayıcı + bitiş eylemleri
- [ ] Ağ kesintisi/uyku yönetimi
- [ ] Ayarlar sistemi
- [ ] Veritabanı + migration

### Arayüz
- [ ] Ana liste, kategoriler, araç çubuğu
- [ ] Ekle/Ayrıntı/Ayarlar pencereleri
- [ ] Parça haritası, hız grafiği
- [ ] Sistem tepsisi + bildirimler
- [ ] Çoklu dil + tema
- [ ] Erişilebilirlik
- [ ] Sanallaştırılmış liste

### Entegrasyon
- [ ] Tarayıcı eklentisi (Chromium + Firefox)
- [ ] Native Messaging / güvenli köprü
- [ ] Çerez/Referer/UA aktarımı
- [ ] Sağ tık, pano izleme, sürükle-bırak
- [ ] Medya algılama (HLS/DASH)
- [ ] Birleştirme (ffmpeg vb.)

### Ağ
- [ ] HTTP/HTTPS (TLS doğrulama)
- [ ] Proxy (HTTP/SOCKS)
- [ ] Basic/Digest/NTLM kimlik doğrulama
- [ ] Site giriş yöneticisi
- [ ] FTP/SFTP (opsiyonel)
- [ ] Mirror desteği (opsiyonel)

### Güvenlik
- [ ] Dosya adı/yol temizleme (path traversal)
- [ ] Tehlikeli uzantı uyarısı
- [ ] Mark of the Web
- [ ] Yerel köprü kimlik doğrulaması
- [ ] Gizli verilerin güvenli saklanması
- [ ] İmzalı güncellemeler ve kod imzalama
- [ ] Log'larda hassas veri maskeleme

### Kalite ve Yayın
- [ ] Birim + entegrasyon + kaos testleri
- [ ] Yerel test sunucusu
- [ ] CI
- [ ] Yükleyici + kaldırıcı
- [ ] Otomatik güncelleme
- [ ] Dokümantasyon ve SSS
- [ ] Gizlilik politikası ve lisanslar
- [ ] Özgün isim/logo (marka ihlali yok)

---

## Son Tavsiyeler

1. **Önce motoru, sonra arayüzü yap.** Motor güvenilir değilse güzel arayüz işe yaramaz.
2. **Motoru arayüzden bağımsız tasarla.** Bir gün arayüzü değiştirmek veya servis haline getirmek isteyeceksin.
3. **Her aşamayı gerçek dosyalarla ve hash doğrulamasıyla test et.**
4. **Küçük başla, sürekli çalışır durumda tut.** Her aşama sonunda kullanabildiğin bir sürüm olsun.
5. **Açık kaynak benzerlerini incele** (örn. aria2, yt-dlp mantığı, çeşitli açık kaynak indirme yöneticileri): mimari kararlarından ve karşılaştıkları sorunlardan çok şey öğrenirsin.
6. **Hedefin "IDM'in kopyası" değil, "sana en iyi çalışan indirme yöneticisi" olsun.**

Başarılar! 🚀
