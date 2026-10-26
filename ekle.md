# FDownloader - Geliştirmeler ve Hata Çözümleri Günlüğü

## [24 Ekim 2026] YouTube Çocuk / Mini Oynatıcı Kısıtlı Videolar ve yt-dlp "Video Not Available" Çözümü

### 1. Karşılaşılan Sorun ve Hata Belirtileri
YouTube Music, çocuklara özel ("Made for Kids" / COPPA) veya mini oynatıcıda oynatılması kısıtlanan bazı videolar indirilirken aşağıdaki hata alınmaktaydı ve indirme 0 B'de kalıyordu:

```text
yt-dlp hata kodu: 1. WARNING: [youtube] No supported JavaScript runtime could be found. Only deno is enabled by default; to use another runtime add --js-runtimes RUNTIME[:PATH] to your command/config. YouTube extraction without a JS runtime has been deprecated, and some formats may be missing.
ERROR: [youtube] 4ztAyph9Zys: This video is not available
```

**Örnek Sorunlu URL:**
`https://music.youtube.com/watch?v=4ztAyph9Zys` (*Помогатор - Çocuk Şarkısı*)

---

### 2. Kök Neden Analizi

1. **YouTube'un Katı Bot ve İmza Korumaları:**
   * YouTube; çocuklara özel, mini oynatıcı kısıtlı ve YouTube Music etiketli içeriklerde standart videolardan farklı olarak daha agresif JavaScript imza/şifreleme (**`n-challenge`** ve **`sig`**) kuralları uygulamaktadır.
2. **`yt-dlp` EJS (External JavaScript) Mimarisi:**
   * `yt-dlp`, bu JS şifrelemelerini çözmek için Python tabanlı eski yorumlayıcı yerine harici bir JS motoru (Node.js, Deno, QuickJS, Bun) kullanmaktadır.
   * Güvenlik nedeniyle varsayılan olarak **yalnızca `deno`** etkindir; sistemde kurulu olsa bile `node` varsayılan olarak kapalı tutulur.
3. **`visionos` İstemcisinin Engellenmesi:**
   * Bir JS motoru bulunamadığında `yt-dlp` imza çözümü gerektirmeyen `visionos` istemcisine fallback yapar.
   * Ancak YouTube, çocuklara özel ve telif/müzik korumalı videolarda `visionos` istemcisini **`UNPLAYABLE`** (oynatılamaz) olarak işaretler; bu da `ERROR: This video is not available` hatasına yol açar.

---

### 3. Yapılan Geliştirmeler ve Kod Değişiklikleri

Yapılan değişiklikler [YtDlpMediaService.cs](file:///c:/FDownloader/src/FDownloader.Core/Video/YtDlpMediaService.cs) dosyasına entegre edilmiştir:

#### A) Otomatik JavaScript Çalışma Zamanı Tespiti (`GetCommonArguments`)
Sistemde kurulu olan Node.js binary'si otomatik olarak tespit edilir ve `yt-dlp`'ye harici JS çözücü olarak tanıtılır:
* `C:\Program Files\nodejs\node.exe`
* `C:\Program Files (x86)\nodejs\node.exe`
* `%LOCALAPPDATA%\Programs\node\node.exe`

Bulunan yola göre veya PATH üzerinden şu bayraklar eklenir:
```bash
--js-runtimes node:"C:\Program Files\nodejs\node.exe" --js-runtimes quickjs --js-runtimes bun --js-runtimes deno
```

#### B) İstemci Geri Dönüş Zinciri (`player_client`)
Kısıtlı videolarda ve JS motoru bulunmayan ortamlarda indirmelerin başarısız olmasını önlemek için istemci öncelik sırası belirlendi:
```bash
--extractor-args "youtube:player_client=web,default,android"
```
* **`web` / `default`:** Yüksek çözünürlükler (1080p, 2K, 4K) ve yüksek kaliteli ses (Opus) için önceliklidir.
* **`android`:** Kısıtlı veya JS motorsuz durumlarda devreye giren ve YouTube'un `UNPLAYABLE` engeline takılmayan yedek istemcidir.

#### C) Esnek Format Seçimi ve MP4 Birleştirme
YouTube Music ve çocuk videolarında ses akışı bazen yalnızca WebM/Opus formatında sunulabilir. Sabit `m4a` zorunluluğu yerine en iyi ses ve görüntüyü seçip MP4 içinde birleştiren esnek kural uygulandı:
```bash
-f "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best[ext=mp4]/best" --merge-output-format mp4
```

#### D) Güncel Binary Sürümü
Uygulama ile birlikte gelen `yt-dlp.exe` resmi kaynaktan en son kararlı sürüme (`2026.10.21`) yükseltildi.

---

### 4. Test ve Doğrulama
* **Test Edilen URL:** `https://music.youtube.com/watch?v=4ztAyph9Zys&si=ssl-AGO2ZVcVDs8K`
* **Meta Veri:** Başlık (*Помогатор*), süre (02:11) ve kapak görseli başarıyla JSON olarak alındı.
* **JS Çözümü:** Node.js ile meydan okuma çözüldü (`[jsc:node] Solving JS challenges using node`).
* **İndirme & Dönüştürme:** Hem MP3 (320 kbps ses) hem de MP4 (1080p video) akışları sıfır hata ile indirildi ve tamamlandı.
