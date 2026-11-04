# FDownloader — Üçüncü Taraf Açık Kaynak Lisans ve Bildirimleri (Third-Party Notices)

Bu yazılım, bağımsız geliştiriciler ve açık kaynak topluluğu tarafından oluşturulan çeşitli açık kaynaklı kütüphaneleri, araçları ve bileşenleri kullanmaktadır. FDownloader projesine güç veren tüm açık kaynak projelerine ve geliştiricilerine teşekkür ederiz.

---

## 1. Dahil Edilen Harici Araçlar ve İkili Dosyalar (External Binaries)

### 1.1 yt-dlp
- **Proje:** yt-dlp (Komut satırı medya indirme ve akış yakalama aracı)
- **Web Sitesi:** [https://github.com/yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp)
- **Lisans:** The Unlicense (Kamu Malı / Public Domain)
- **Bildirim:** yt-dlp, herhangi bir kısıtlama olmaksızın kamu malı lisansıyla dağıtılmaktadır.

### 1.2 7-Zip Standalone Console (7za.exe)
- **Proje:** 7-Zip (Igor Pavlov)
- **Web Sitesi:** [https://www.7-zip.org/](https://www.7-zip.org/)
- **Lisans:** GNU LGPL v2.1 (ve unRAR kısıtlaması ile unRAR lisansı)
- **Bildirim:** 7-Zip kodunun büyük bir kısmı GNU LGPL altındadır. unRAR kodu kısıtlı lisans taşır. Kaynak kodu `www.7-zip.org` adresinden indirilebilir. FDownloader, 7-Zip'i harici bir CLI prosesi olarak kullanmakta ve kaynak kodunu değiştirmemektedir.

### 1.3 FFmpeg & FFprobe (İsteğe Bağlı Araç)
- **Proje:** FFmpeg (Ses ve video dönüştürücü/birleştirici)
- **Web Sitesi:** [https://ffmpeg.org/](https://ffmpeg.org/)
- **Lisans:** GNU LGPL v2.1+ / GPL v2+ (derleme bayraklarına bağlı)
- **Bildirim:** FFmpeg, dinamik CLI prosesi olarak çağrılır. Kaynak kodu `ffmpeg.org` adresinde mevcuttur.

---

## 2. NuGet Paket Bağımlılıkları ve Lisansları

| Paket Adı | Sürüm | Lisans | Telif Hakkı / Bağlantı |
|---|---|---|---|
| **Avalonia** | 12.1.4 | MIT | Copyright (c) .NET Foundation and Contributors, [AvaloniaUI](https://github.com/AvaloniaUI/Avalonia) |
| **Avalonia.Desktop** | 12.1.4 | MIT | Copyright (c) .NET Foundation and Contributors |
| **Avalonia.Themes.Fluent** | 12.1.4 | MIT | Copyright (c) .NET Foundation and Contributors |
| **Avalonia.Fonts.Inter** | 12.1.4 | MIT / SIL OFL | Copyright (c) Rasmus Andersson |
| **CommunityToolkit.Mvvm** | 8.4.2 | MIT | Copyright (c) .NET Foundation and Contributors, Microsoft |
| **Microsoft.Data.Sqlite** | 9.0.0 | MIT | Copyright (c) .NET Foundation and Contributors, Microsoft |
| **Microsoft.Extensions.Logging** | 9.0.0 | MIT | Copyright (c) .NET Foundation and Microsoft |
| **Microsoft.Extensions.DependencyInjection** | 9.0.0 | MIT | Copyright (c) .NET Foundation and Microsoft |
| **System.Security.Cryptography.ProtectedData** | 10.0.14 | MIT | Copyright (c) .NET Foundation and Microsoft |
| **MonoTorrent** | 3.0.2 | MIT | Copyright (c) Alan McGovern, James Turner, [MonoTorrent](https://github.com/alanmcgovern/monotorrent) |
| **SharpCompress** | 0.50.7 | MIT | Copyright (c) Adam Hathcock, [SharpCompress](https://github.com/adamhathcock/sharpcompress) |
| **Velopack** | 1.2.161 | MIT | Copyright (c) Caelan Sayler and contributors, [Velopack](https://github.com/velopack/velopack) |
| **Serilog** | 4.2.0 | Apache-2.0 | Copyright (c) Serilog Contributors |
| **Serilog.Sinks.File** | 6.0.0 | Apache-2.0 | Copyright (c) Serilog Contributors |
| **Sentry** | 6.12.1 | MIT / Apache-2.0 | Copyright (c) Sentry Team and Contributors |

---

## 3. Standart MIT Lisans Metni (Genel Özet)

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 4. Apache License 2.0 (Özet Bildirim)

Serilog ve ilişkili paketler Apache Lisansı 2.0 altında dağıtılmaktadır. Lisans metninin tam haline [https://www.apache.org/licenses/LICENSE-2.0](https://www.apache.org/licenses/LICENSE-2.0) adresinden ulaşılabilir.

