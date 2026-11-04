# FDownloader — Tarayıcı Eklentisi Mimarisi ve İzin Gerekçeleri

> **Belge Sürümü:** 1.0  
> **Eklenti Sürümü:** 1.5.0 (Manifest V3)  
> **Desteklenen Tarayıcılar:** Google Chrome, Microsoft Edge, Brave, Opera, Vivaldi, Mozilla Firefox  
> **İlgili Görev:** `P4-01` (Faz 4 — Tarayıcı Eklentisi ve Entegrasyon)

---

## 1. Genel Mimari

FDownloader tarayıcı eklentisi, modern Chromium ve Firefox standartlarına uygun olarak **Manifest V3** mimarisiyle geliştirilmiştir.

```
┌────────────────────────────────────────────────────────┐
│                   Web Tarayıcısı                      │
│                                                        │
│  ┌────────────────┐     ┌───────────────────────────┐  │
│  │   content.js   │────>│       background.js       │  │
│  │  (Sayfa & Video│     │  (Manifest V3 Svc Worker) │  │
│  │   Yakalayıcı)  │     │                           │  │
│  └────────────────┘     └─────────────┬─────────────┘  │
│                                       │                │
└───────────────────────────────────────┼────────────────┘
                                        │ HTTP POST /download
                                        │ HTTP GET  /health
                                        ▼ (Sadece 127.0.0.1)
┌────────────────────────────────────────────────────────┐
│         FDownloader Masaüstü Uygulaması               │
│                                                        │
│  ┌──────────────────────────────────────────────────┐  │
│  │  BrowserBridgeService (Port: 9666 - Loopback)    │  │
│  │  • Origin Doğrulaması (chrome-extension://...)   │  │
│  │  • Host Doğrulaması (127.0.0.1 / localhost)       │  │
│  │  • Hassas Veri Koruması (DPAPI / AES-GCM)        │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

---

## 2. İzin Denetimi ve Gerekçeleri (Permission Justification)

Eklenti, **"En Az Yetki / Ayrıcalık" (Principle of Least Privilege)** ilkesi doğrultusunda denetlenmiş ve gereksiz tüm izinler kaldırılmıştır.

| İzin Adı | Kategori | Neden Zorunlu / Kullanım Amacı | Kaldırılabilir mi? |
|---|---|---|---|
| `downloads` | API İzni | Tarayıcının kendi başlattığı indirmeleri yakalamak (`onDeterminingFilename`), dosya FDownloader'a aktarıldığında tarayıcı indirmesini iptal etmek (`cancel`) ve indirme geçmişini temizlemek (`erase`) için zorunludur. | ❌ Hayır. Kaldırılırsa otomatik indirme yakalama çalışmaz. |
| `contextMenus` | API İzni | Web sayfalarındaki herhangi bir indirme bağlantısına, videoya, ses öğesine veya resme sağ tıklandığında "⚡ FDownloader ile İndir" menü seçeneğini sunmak için kullanılır. | ❌ Hayır. Sağ tık menüsü için zorunlu. |
| `storage` | API İzni | Eklentinin kullanıcı tercihlerini (otomatik yakalama açık/kapalı, Alt tuşu ile indirmeyi tarayıcıya bırakma, yakalanacak özel dosya uzantıları, video paneli pozisyonu) yerel tarayıcı profilinde saklar. | ❌ Hayır. Kullanıcı ayarları kalıcı olmaz. |
| `cookies` | API İzni | Kullanıcının oturum açtığı sitelerden (Google Drive, Dropbox, dosya barındırıcılar vb.) dosya indirilirken kimlik doğrulama çerezlerini FDownloader'a iletmek için zorunludur. Çerez iletilmezse indirme 401/403/Login sayfasına düşer. | ❌ Hayır. Giriş gerektiren indirmeler başarısız olur. |
| `host_permissions` (`*://*/*`) | Ağ İzni | `chrome.cookies.getAll` API'sinin kullanıcının indirme yaptığı sitedeki çerezleri okuyabilmesi ve sayfalardaki indirme bağlantılarını analiz edebilmesi için gereklidir. | ❌ Hayır. Çerez erişimi bu izin olmadan çalışmaz. |
| `host_permissions` (`127.0.0.1:9666/*`, `localhost:9666/*`) | Yerel Ağ | Eklentinin yerel makinede çalışan FDownloader masaüstü uygulamasına indirme görevini iletmesi (`fetch`) için zorunludur. | ❌ Hayır. Masaüstü uygulamasıyla iletişim kurulamaz. |

---

## 3. Yapılan Temizlikler ve Kaldırılan Kodlar

P4-01 görevi kapsamında eklenti kod tabanında aşağıdaki optimizasyonlar ve güvenlik sertleştirmeleri gerçekleştirilmiştir:

1. **`scripting` İzninin Kaldırılması:**
   - Önceki sürümde, eklenti kurulduğunda mevcut açık sekmelere `content.js`'i enjekte etmek için `scripting` izni kullanılıyordu.
   - Bu izin, güvenlik denetimlerinde (Chrome Web Store ve AMO) "sayfalarda rastgele kod çalıştırma" şüphesi doğurduğu ve gereksiz geniş bir yetki alanı sağladığı için kaldırıldı.
   - Sayfa etkileşimleri yalnızca `manifest.json` içindeki bildirimsel `content_scripts` kurallarıyla sağlanmaktadır.

2. **Gereksiz Arka Plan Polling / Alarm Döngüsünün Temizlenmesi:**
   - Eski sürümde her 1.5 - 12 saniyede bir veya 30 saniyelik alarmlarla arka planda FDownloader'a ping atan sonsuz bir `heartbeatTimer` döngüsü mevcuttu.
   - Bu döngü kaldırıldı; eklenti tamamen Manifest V3 olay güdümlü (event-driven) hale getirildi. Artık servis çalışanı (service worker) yalnızca kullanıcı indirme başlattığında, bağlantıya sağ tıkladığında veya popup açıldığında tetiklenmektedir. Bu sayede gereksiz CPU ve pil tüketimi önlenmiştir.

3. **Ölü Kodların (Dead Code) Temizlenmesi:**
   - `content.js` içerisinde kullanılmayan 1.2 KB'lık `FDOWN_LOGO_DATA_URI` base64 sabiti temizlendi (yerine hafif inline SVG kullanılmaktadır).

---

## 4. Güvenlik ve Gizlilik Garantileri

1. **Dış Sunucuya Veri Çıkışı Yoktur:** Eklenti internet üzerindeki hiçbir harici sunucuya veya telemetri servisine veri göndermez.
2. **Yalnızca Yerel İletişim (Localhost Loopback):** Eklenti yalnızca `127.0.0.1:9666` adresinde çalışan FDownloader yerel köprüsüne veri iletir.
3. **Alt Tuşu ile Anında Baypas:** İndirme linkine tıklanırken `Alt` tuşuna basılı tutulursa (veya basıldıktan sonraki 5 saniye içinde) eklenti devreye girmez; indirme tarayıcının kendi indirme yöneticisine bırakılır.
4. **Köprü Güvenlik Sertleştirmesi (P3-01 Entegrasyonu):** Masaüstü tarafındaki `BrowserBridgeService`, yalnızca izin verilen `chrome-extension://` veya `moz-extension://` Origin başlıklarına cevap vermektedir.

