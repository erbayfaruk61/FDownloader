// ⚡ FDownloader Chrome/Edge/Brave/Firefox Browser Extension (Manifest V3)
const BRIDGE_URL = "http://127.0.0.1:9666";

// Servis çalışanının güncellendiğinde beklemeden derhal devreye girmesini sağla
self.addEventListener("install", () => {
  self.skipWaiting();
});
self.addEventListener("activate", (event) => {
  event.waitUntil(clients.claim());
});

// Genel kullanımı yüksek ve FDownloader tarafından otomatik yakalanacak dosya uzantıları
const DEFAULT_EXTENSIONS = [
  // Arşivler & Sıkıştırma (7z, zip, rar vb.)
  "7z", "zip", "rar", "tar", "gz", "bz2", "xz", "zst", "zstd", "tgz", "tbz2", "txz",
  "cab", "wim", "swm", "esd", "lz", "lzma", "lzh", "arj", "ace", "cpio", "z",
  // Bölünmüş / Çok Parçalı Arşivler & Genel Veri Dosyaları
  "001", "002", "003", "004", "005", "006", "007", "008", "009", "r00", "r01", "r02",
  "dat", "bin", "dump", "raw",
  // Disk İmajları & Sanallaştırma
  "iso", "img", "dmg", "vhd", "vhdx", "vmdk", "vdi", "qcow2", "ova", "ovf", "cue", "nrg", "mdf", "toast",
  // Yürütülebilir & Kurulum Paketleri
  "exe", "msi", "msix", "appx", "appxbundle", "msixbundle", "apk", "xapk", "apkm", "ipa",
  "deb", "rpm", "pkg", "appimage", "flatpakref", "snap", "jar", "war",
  // Video & Medya
  "mp4", "mkv", "avi", "mov", "wmv", "flv", "webm", "m4v", "ts", "mts", "m2ts",
  "3gp", "3g2", "vob", "ogv", "divx", "xvid", "asf", "f4v", "rmvb", "rm",
  // Ses & Müzik
  "mp3", "flac", "wav", "aac", "m4a", "ogg", "opus", "wma", "alac", "aiff", "ape", "mid", "midi", "ac3", "dts", "mka",
  // Belgeler & E-Kitaplar
  "pdf", "epub", "mobi", "azw3", "djvu", "cbr", "cbz", "fb2",
  "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "rtf", "csv",
  // Veritabanı & İndirme Dosyaları
  "torrent", "sqlite", "db", "sql", "bak"
];

// Doğrudan yakalanacak MIME tipleri
const KNOWN_MIME_EXACT = [
  "application/x-7z-compressed",
  "application/7z-compressed",
  "application/x-compressed",
  "application/zip",
  "application/x-zip-compressed",
  "application/x-rar-compressed",
  "application/vnd.rar",
  "application/x-rar",
  "application/x-tar",
  "application/x-gzip",
  "application/gzip",
  "application/x-bzip2",
  "application/x-xz",
  "application/zstd",
  "application/x-iso9660-image",
  "application/x-apple-diskimage",
  "application/x-msdownload",
  "application/vnd.android.package-archive",
  "application/x-debian-package",
  "application/x-redhat-package-manager",
  "application/pdf",
  "application/epub+zip",
  "application/x-bittorrent",
  "application/octet-stream",
  "binary/octet-stream"
];

const MIME_PREFIXES = [
  "video/",
  "audio/"
];

// Doğrudan tarayıcıya bırakılacak oturum açma / SSO kimlik doğrulama alan adları
const DEFAULT_EXCLUDED_DOMAINS = [
  "accounts.google.com",
  "login.microsoftonline.com",
  "appleid.apple.com"
];

function matchesMime(mime) {
  if (!mime) return false;
  const m = mime.toLowerCase().trim();
  if (KNOWN_MIME_EXACT.includes(m)) return true;
  for (const prefix of MIME_PREFIXES) {
    if (m.startsWith(prefix)) return true;
  }
  return false;
}

function extractCandidateExtensions(str) {
  if (!str) return [];
  const results = [];
  try {
    let decoded = str;
    try { decoded = decodeURIComponent(str); } catch (e) { }

    // Query parametresi ve hash işaretini ayır
    let pathPart = decoded.split(/[?#]/)[0];

    // Geçici indirme uzantılarını temizle (.crdownload, .download, .tmp, .part)
    pathPart = pathPart.replace(/\.(crdownload|download|tmp|part)$/i, "");

    // Path sonundaki dosya adını al
    const segments = pathPart.split(/[/\\]/);
    const fileName = segments[segments.length - 1];

    if (fileName && fileName.includes(".")) {
      const parts = fileName.split(".");
      const lastExt = parts[parts.length - 1].toLowerCase().trim();
      if (lastExt) results.push(lastExt);

      // Çok parçalı dosya kontrolü (örn: archive.7z.001 -> '7z')
      if (parts.length > 2) {
        const secondLastExt = parts[parts.length - 2].toLowerCase().trim();
        if (secondLastExt) results.push(secondLastExt);
      }
    }

    // URL query string içindeki dosya adlarını tara (?file=dosya.7z, ?name=setup.exe vb.)
    if (decoded.includes("?")) {
      const queryString = decoded.substring(decoded.indexOf("?") + 1);
      const searchParams = new URLSearchParams(queryString);
      for (const [, value] of searchParams.entries()) {
        if (!value || typeof value !== "string") continue;
        const valClean = value.replace(/\.(crdownload|download|tmp|part)$/i, "");
        const dot = valClean.lastIndexOf(".");
        if (dot !== -1 && dot < valClean.length - 1) {
          const ext = valClean.substring(dot + 1).toLowerCase().trim();
          if (ext && ext.length <= 10 && !results.includes(ext)) {
            results.push(ext);
          }
        }
      }
    }
  } catch (e) { }
  return results;
}

function extractHostname(urlStr) {
  if (!urlStr) return "";
  try {
    const u = new URL(urlStr);
    return (u.hostname || "").toLowerCase().trim();
  } catch (e) {
    return "";
  }
}

function isDomainExcluded(url, referrer, excludedList) {
  if (!Array.isArray(excludedList) || excludedList.length === 0) return false;

  const urlHost = extractHostname(url);
  const refHost = extractHostname(referrer);
  if (!urlHost && !refHost) return false;

  for (const item of excludedList) {
    if (!item || typeof item !== "string") continue;
    let pattern = item.toLowerCase().trim();
    if (!pattern) continue;

    if (pattern.startsWith("*.")) {
      pattern = pattern.substring(2);
    }

    if (urlHost && (urlHost === pattern || urlHost.endsWith("." + pattern))) return true;
    if (refHost && (refHost === pattern || refHost.endsWith("." + pattern))) return true;
  }

  return false;
}

function shouldCapture(downloadItem, allowedExtensions, minSizeBytes = 0, excludedDomains = []) {
  // 1. Hariç tutulan alan adları kontrolü (Kullanıcı listesi + yerleşik e-fatura/güvenlikli alan adları)
  const combinedExcluded = Array.from(new Set([...DEFAULT_EXCLUDED_DOMAINS, ...(excludedDomains || [])]));
  if (isDomainExcluded(downloadItem.url, downloadItem.referrer, combinedExcluded) ||
      isDomainExcluded(downloadItem.finalUrl, downloadItem.referrer, combinedExcluded)) {
    return false;
  }

  // 2. Minimum boyut kontrolü (fileSize biliniyorsa ve sınır konmuşsa)
  if (typeof minSizeBytes === "number" && minSizeBytes > 0) {
    if (downloadItem.fileSize > 0 && downloadItem.fileSize < minSizeBytes) {
      return false;
    }
  }

  if (downloadItem.mime && matchesMime(downloadItem.mime)) {
    return true;
  }

  const candidateList = [
    ...extractCandidateExtensions(downloadItem.filename),
    ...extractCandidateExtensions(downloadItem.finalUrl),
    ...extractCandidateExtensions(downloadItem.url)
  ];

  const allowedSet = new Set(allowedExtensions.map(e => e.toLowerCase()));
  for (const ext of candidateList) {
    if (allowedSet.has(ext)) {
      return true;
    }
  }

  // application/octet-stream veya binary/octet-stream genel ikili veri ise ve web sayfası değilse yakala
  if (downloadItem.mime === "application/octet-stream" || downloadItem.mime === "binary/octet-stream") {
    const isWebPage = candidateList.some(ext => ["html", "htm", "php", "asp", "aspx", "jsp", "js", "css"].includes(ext));
    if (!isWebPage) return true;
  }

  return false;
}

// ─── Ayarların Bellek İçi Önbelleği (Senkron ve 0 ms Gecikmeli Kararlar İçin) ───
let cachedSettings = {
  isEnabled: true,
  bypassOnAltKey: true,
  extensions: DEFAULT_EXTENSIONS,
  minSizeBytes: 0,
  excludedDomains: DEFAULT_EXCLUDED_DOMAINS
};

function updateSettingsCache() {
  if (typeof chrome === "undefined" || !chrome.storage || !chrome.storage.local) return;
  chrome.storage.local.get({
    isEnabled: true,
    bypassOnAltKey: true,
    extensions: [],
    minSizeBytes: 0,
    excludedDomains: []
  }, (st) => {
    if (st) {
      cachedSettings.isEnabled = st.isEnabled !== false;
      cachedSettings.bypassOnAltKey = st.bypassOnAltKey !== false;
      cachedSettings.extensions = Array.from(new Set([...DEFAULT_EXTENSIONS, ...(st.extensions || [])]));
      cachedSettings.minSizeBytes = st.minSizeBytes || 0;
      cachedSettings.excludedDomains = Array.from(new Set([...DEFAULT_EXCLUDED_DOMAINS, ...(st.excludedDomains || [])]));
    }
  });
}
updateSettingsCache();
try {
  if (typeof chrome !== "undefined" && chrome.storage && chrome.storage.onChanged) {
    chrome.storage.onChanged.addListener(() => updateSettingsCache());
  }
} catch (_) { }

// ─── FDownloader Canlılık Önbelleği (Ağ Beklemesini Önler) ───
let lastBridgeAliveTime = 0;
let isBridgeCurrentlyAlive = false;

async function quickIsBridgeAlive() {
  const now = Date.now();
  // Son 4.5 saniye içinde başarılı bir heartbeat alındıysa hemen true dön
  if (isBridgeCurrentlyAlive && (now - lastBridgeAliveTime < 4500)) {
    return true;
  }
  return await isBridgeAlive();
}

async function safeReloadExtension(reason) {
  try {
    if (typeof chrome === "undefined" || !chrome.storage || !chrome.storage.local) return;
    const store = await chrome.storage.local.get("last_reload_timestamp");
    const lastTime = store?.last_reload_timestamp || 0;
    const now = Date.now();
    // 60 saniyelik kesin cooldown: sonsuz yeniden başlatma ve Chrome 'etkin değil' kilidini önler
    if (now - lastTime < 60000) {
      console.warn(`⚡ FDownloader: Yenileme isteği (${reason}) engellendi, 60 sn cooldown devrede.`);
      return;
    }
    await chrome.storage.local.set({ last_reload_timestamp: now });
    if (chrome.runtime && chrome.runtime.reload) {
      console.log(`⚡ FDownloader: ${reason}, eklenti diskten yenileniyor...`);
      chrome.runtime.reload();
    }
  } catch (e) {
    console.warn("Yenileme yürütülemedi:", e);
  }
}

async function isBridgeAlive() {
  try {
    const manifest = chrome.runtime.getManifest();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 1200);
    const res = await fetch(`${BRIDGE_URL}/health?v=${encodeURIComponent(manifest.version || "1.0.0")}`, {
      headers: { "X-Extension-Version": manifest.version || "1.0.0" },
      signal: controller.signal
    });
    clearTimeout(timer);
    if (res.ok) {
      isBridgeCurrentlyAlive = true;
      lastBridgeAliveTime = Date.now();
      try {
        const data = await res.json();
        if (data && data.reloadRequested === true) {
          safeReloadExtension("Uygulamadan yenileme sinyali alındı");
        }
      } catch (_) { }
      return true;
    }
    isBridgeCurrentlyAlive = false;
    return false;
  } catch (e) {
    isBridgeCurrentlyAlive = false;
    return false;
  }
}

async function checkBridgeHeartbeat() {
  try {
    const manifest = chrome.runtime.getManifest();
    const ver = manifest.version || "1.0.0";
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 1500);
    
    // Öncelikli olarak POST /ping dene
    let res = null;
    try {
      res = await fetch(`${BRIDGE_URL}/ping`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Extension-Version": ver
        },
        body: JSON.stringify({ version: ver }),
        signal: controller.signal
      });
    } catch (_) {
      // POST başarısız olursa basit GET /health ile kalp atışını bildir
      try {
        res = await fetch(`${BRIDGE_URL}/health?v=${encodeURIComponent(ver)}`, {
          method: "GET",
          headers: { "X-Extension-Version": ver },
          signal: controller.signal
        });
      } catch (__) { }
    }
    clearTimeout(timer);

    if (res && res.ok) {
      isBridgeCurrentlyAlive = true;
      lastBridgeAliveTime = Date.now();
      const data = await res.json().catch(() => null);
      if (data && data.reloadRequested === true) {
        safeReloadExtension("Kalp atışında yenileme sinyali alındı");
      }
    } else {
      isBridgeCurrentlyAlive = false;
    }
  } catch (_) {
    isBridgeCurrentlyAlive = false;
  }
}

// FDownloader uygulamasından komut/yenileme kontrolü (3.5 saniye aralıkla)
setInterval(checkBridgeHeartbeat, 3500);
setTimeout(checkBridgeHeartbeat, 1000);

// Chrome Manifest V3 Service Worker uyanıklık ve alarm mekanizması
try {
  if (typeof chrome !== "undefined" && chrome.alarms) {
    chrome.alarms.create("fdownloader_heartbeat", { periodInMinutes: 0.5 });
    chrome.alarms.onAlarm.addListener(alarm => {
      if (alarm && alarm.name === "fdownloader_heartbeat") {
        checkBridgeHeartbeat();
      }
    });
  }
} catch (_) { }

// Sekme ve pencere odaklanmalarında kalp atışını anında tetikle
try {
  if (typeof chrome !== "undefined" && chrome.tabs && chrome.tabs.onActivated) {
    chrome.tabs.onActivated.addListener(() => checkBridgeHeartbeat());
  }
  if (typeof chrome !== "undefined" && chrome.windows && chrome.windows.onFocusChanged) {
    chrome.windows.onFocusChanged.addListener(() => checkBridgeHeartbeat());
  }
} catch (_) { }

async function getCookiesForUrl(url, referrer) {
  const cookieMap = new Map();
  const urlsToQuery = new Set();

  if (url && (url.startsWith("http://") || url.startsWith("https://"))) {
    urlsToQuery.add(url);
    try {
      const parsed = new URL(url);
      const host = parsed.hostname.toLowerCase();
      // Kök alan adı (apex domain) çerezleri
      const parts = host.split(".");
      if (parts.length >= 2) {
        const rootDomain = parts.slice(-2).join(".");
        urlsToQuery.add(`https://${rootDomain}/`);
        urlsToQuery.add(`https://www.${rootDomain}/`);
      }
      // Google Drive / Cloud depolama özel çerez havuzu
      if (host.includes("google.com")) {
        urlsToQuery.add("https://drive.google.com/");
        urlsToQuery.add("https://google.com/");
      }
    } catch (_) { }
  }

  if (referrer && (referrer.startsWith("http://") || referrer.startsWith("https://"))) {
    urlsToQuery.add(referrer);
  }

  for (const qUrl of urlsToQuery) {
    try {
      const list = await chrome.cookies.getAll({ url: qUrl });
      if (list) {
        list.forEach(c => {
          if (!cookieMap.has(c.name)) {
            cookieMap.set(c.name, c.value);
          }
        });
      }
    } catch (_) { }
  }

  return Array.from(cookieMap.entries())
    .map(([k, v]) => `${k}=${v}`)
    .join("; ");
}

async function sendToFDownloader(item) {
  try {
    const manifest = chrome.runtime.getManifest();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 1200); // 1.2 sn zaman aşımı (köprü kapalıysa anında hata verir)
    const res = await fetch(`${BRIDGE_URL}/download`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Extension-Version": manifest.version || "1.0.0"
      },
      body: JSON.stringify(item),
      signal: controller.signal
    });
    clearTimeout(timer);
    const data = await res.json();
    return data && data.success === true;
  } catch (err) {
    return false;
  }
}

function arrayBufferToBase64(buffer) {
  let binary = "";
  const bytes = new Uint8Array(buffer);
  const len = bytes.byteLength;
  const chunkSize = 0x8000;
  for (let i = 0; i < len; i += chunkSize) {
    binary += String.fromCharCode.apply(null, bytes.subarray(i, Math.min(i + chunkSize, len)));
  }
  return btoa(binary);
}

function isProtectedInvoiceOrSessionUrl(url, filename) {
  if (!url) return false;
  const u = url.toLowerCase();
  const fn = (filename || "").toLowerCase();

  // Sadece ve sadece Cloudflare bot korumalı e-Fatura ve kamu portalları
  if (u.includes("fatura.n11.com") || u.includes("earsivportal.efatura.gov.tr") || u.includes("earsiv") || u.includes("efatura")) {
    return true;
  }
  if (u.includes("fatura") && (u.includes(".pdf") || fn.includes(".pdf") || u.includes("n11.com") || u.includes("gib.gov.tr"))) {
    return true;
  }
  return false;
}

async function fetchFileAsBase64(url, referrer) {
  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 4000); // 4 sn kesin zaman aşımı (tarayıcıyı asla kilitlemez)
    const headers = {};
    if (referrer && (referrer.startsWith("http://") || referrer.startsWith("https://"))) {
      headers["Referer"] = referrer;
    }
    const res = await fetch(url, {
      method: "GET",
      headers: headers,
      credentials: "include",
      signal: controller.signal
    });
    clearTimeout(timeout);

    if (!res.ok) {
      console.warn("fetchFileAsBase64 HTTP hatası:", res.status, url);
      return null;
    }

    const cl = res.headers.get("content-length");
    if (cl && parseInt(cl, 10) > 30 * 1024 * 1024) {
      return null;
    }

    const buf = await res.arrayBuffer();
    if (buf.byteLength > 30 * 1024 * 1024) {
      return null;
    }

    return arrayBufferToBase64(buf);
  } catch (err) {
    console.warn("fetchFileAsBase64 hatası:", err);
    return null;
  }
}

async function sendBatchToFDownloader(items) {
  if (!items || items.length === 0) return false;
  try {
    const manifest = chrome.runtime.getManifest();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 6000);
    const res = await fetch(`${BRIDGE_URL}/download/batch`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Extension-Version": manifest.version || "1.0.0"
      },
      body: JSON.stringify({ items: items }),
      signal: controller.signal
    });
    clearTimeout(timer);
    if (res.ok) {
      const data = await res.json();
      return data && data.success === true;
    }
  } catch (err) {
    console.warn("Toplu indirme /batch endpointine iletilemedi, sırayla deneniyor:", err);
  }

  // Fallback: tek tek gönder
  for (const it of items) {
    await sendToFDownloader(it);
    await new Promise(r => setTimeout(r, 60));
  }
  return true;
}

// ─── Alt Tuşu Takibi (5 Saniye Boyunca İndirme Yakalamama / Baypas) ───
let memIsAltDown = false;
let memAltPressedUntil = 0;

// Alt tuşu baypası persistence hazırlandı

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message && message.type === "PING_BRIDGE") {
    checkBridgeHeartbeat();
    sendResponse({ ok: true });
    return false;
  }

  if (message && message.type === "ALT_EVENT") {
    memIsAltDown = Boolean(message.isDown);
    if (message.isDown) {
      memAltPressedUntil = Math.max(memAltPressedUntil, Date.now() + 5000);
    }
    sendResponse({ ok: true });
    return false;
  }

  if (message && message.type === "INTERCEPT_LINK" && message.url) {
    (async () => {
      // 5 saniyelik Alt tuşu baypası aktif mi?
      const now = Date.now();
      const isAltActive = memIsAltDown || (now < memAltPressedUntil);
      if (isAltActive) {
        console.log("⚡ FDownloader: Alt tuşu (5 sn baypas) devrede. Link doğrudan tarayıcıya bırakıldı.");
        sendResponse({ success: false, reason: "alt_key_bypass" });
        return;
      }

      if (isDomainExcluded(message.url, message.referrer || sender.url || "", cachedSettings.excludedDomains)) {
        sendResponse({ success: false, reason: "excluded_domain" });
        return;
      }

      let fileBase64 = null;
      if (isProtectedInvoiceOrSessionUrl(message.url, message.filename)) {
        fileBase64 = await fetchFileAsBase64(message.url, message.referrer || sender.url || "");
      }

      const cookies = await getCookiesForUrl(message.url, message.referrer || sender.url || "");
      const captured = await sendToFDownloader({
        url: message.url,
        filename: message.filename || "",
        referrer: message.referrer || sender.url || "",
        cookies: cookies,
        userAgent: navigator.userAgent,
        fileBase64: fileBase64
      });

      sendResponse({ success: captured });
    })();
    return true; // Asenkron sendResponse için zorunlu
  }
});

// ─── 1. İndirme Yakalama (chrome.downloads.onDeterminingFilename) ───────────

chrome.downloads.onDeterminingFilename.addListener((downloadItem, suggest) => {
  // 1. blob: veya data: linklerini tarayıcı kendisi hemen yönlendirsin
  const rawUrl = downloadItem.finalUrl || downloadItem.url || "";
  if (!rawUrl || rawUrl.startsWith("blob:") || rawUrl.startsWith("data:")) {
    suggest();
    return false;
  }

  // 2. Eklenti kapalıysa SENKRON OLARAK ANINDA suggest() çağır (0 ms gecikme)
  if (!cachedSettings.isEnabled) {
    suggest();
    return false;
  }

  // 3. Alt tuşuna basıldıysa SENKRON OLARAK ANINDA suggest() çağır (0 ms gecikme)
  const now = Date.now();
  const isAltActive = memIsAltDown || (now < memAltPressedUntil);
  if (cachedSettings.bypassOnAltKey !== false && isAltActive) {
    console.log("⚡ FDownloader: Alt tuşu (5 sn baypas) devrede. İndirme doğrudan tarayıcıya bırakıldı.");
    suggest();
    return false;
  }

  // 4. Hedef dosya değilse SENKRON OLARAK ANINDA suggest() çağır (0 ms gecikme)
  const isTargetFile = shouldCapture(downloadItem, cachedSettings.extensions, cachedSettings.minSizeBytes, cachedSettings.excludedDomains);
  if (!isTargetFile) {
    suggest();
    return false;
  }

  // 5. Asenkron FDownloader aktarımı (Köprü kapalıysa 5-10ms içinde tarayıcıya bırakır)
  (async () => {
    let captured = false;
    try {
      const cookies = await getCookiesForUrl(downloadItem.url, downloadItem.referrer);
      
      let fileBase64 = null;
      if (isProtectedInvoiceOrSessionUrl(downloadItem.url, downloadItem.filename)) {
        fileBase64 = await fetchFileAsBase64(downloadItem.url, downloadItem.referrer);
      }

      captured = await sendToFDownloader({
        url: downloadItem.url,
        filename: downloadItem.filename,
        referrer: downloadItem.referrer || "",
        cookies: cookies,
        userAgent: navigator.userAgent,
        totalBytes: downloadItem.fileSize > 0 ? downloadItem.fileSize : null,
        mime: downloadItem.mime || "",
        fileBase64: fileBase64
      });
    } catch (e) {
      console.warn("FDownloader aktarım hatası:", e);
    }

    if (captured) {
      // 1. Önce suggest() çağrılarak determining_filename aşaması sonlandırılır
      suggest({ filename: downloadItem.filename });
      // 2. İndirme in_progress durumuna geçtiği an iptal edilip indirme geçmişinden silinir
      setTimeout(() => {
        chrome.downloads.cancel(downloadItem.id, () => {
          chrome.downloads.erase({ id: downloadItem.id }, () => {});
        });
      }, 60);
    } else {
      // FDownloader kapalıysa veya yakalamadıysa hemen tarayıcıya devret (sıfır gecikme)
      suggest();
    }
  })();

  return true; // Asenkron suggest için zorunlu
});

// ─── 2. Sağ Tık Bağlam Menüsü (Context Menu) ────────────────────────────────

function setupContextMenus() {
  chrome.contextMenus.removeAll(() => {
    // 1. Doğrudan bağlantı veya medya üzerine tıklandığında (tek tıkla direkt indirme)
    chrome.contextMenus.create({
      id: "fdownloader-capture-link",
      title: "⚡ FDownloader ile İndir",
      contexts: ["link", "video", "audio", "image"]
    });

    // 2. Sayfa, çerçeve, seçim veya bağlantı üzerinde sağ tık ana menüsü
    chrome.contextMenus.create({
      id: "fdownloader-menu-parent",
      title: "⚡ FDownloader",
      contexts: ["page", "frame", "selection", "link"]
    });

    // 2.1 Bu Sayfayı / Belgeyi İndir (Örn: Açık PDF sekmesi)
    chrome.contextMenus.create({
      id: "fdownloader-capture-page",
      parentId: "fdownloader-menu-parent",
      title: "📄 Bu Sayfayı / Açık Belgeyi İndir (PDF/Web)",
      contexts: ["page", "frame", "link"]
    });

    // 2.2 Sayfadaki Tüm PDF'leri İndir
    chrome.contextMenus.create({
      id: "fdownloader-extract-pdf",
      parentId: "fdownloader-menu-parent",
      title: "📑 Sayfadaki Tüm PDF Belgelerini İndir",
      contexts: ["page", "frame", "selection", "link"]
    });

    // 2.3 Sayfadaki Tüm Arşivleri İndir
    chrome.contextMenus.create({
      id: "fdownloader-extract-archives",
      parentId: "fdownloader-menu-parent",
      title: "📦 Sayfadaki Tüm Arşivleri İndir (ZIP, RAR, 7Z...)",
      contexts: ["page", "frame", "selection", "link"]
    });

    // 2.4 Sayfadaki Tüm Ofis Belgelerini İndir
    chrome.contextMenus.create({
      id: "fdownloader-extract-docs",
      parentId: "fdownloader-menu-parent",
      title: "📝 Sayfadaki Tüm Ofis Belgelerini İndir (Word, Excel, PPT...)",
      contexts: ["page", "frame", "selection", "link"]
    });

    // 2.5 Sayfadaki Tüm Medyaları İndir
    chrome.contextMenus.create({
      id: "fdownloader-extract-media",
      parentId: "fdownloader-menu-parent",
      title: "🎬 Sayfadaki Tüm Videoları & Sesleri İndir",
      contexts: ["page", "frame", "selection", "link"]
    });

    // 2.6 Sayfadaki Tüm Dosyaları İndir
    chrome.contextMenus.create({
      id: "fdownloader-extract-all",
      parentId: "fdownloader-menu-parent",
      title: "📥 Sayfadaki Tüm Dosyaları Yakala...",
      contexts: ["page", "frame", "selection", "link"]
    });
  });
}

chrome.runtime.onInstalled.addListener(() => {
  setupContextMenus();
});

chrome.runtime.onStartup.addListener(() => {
  setupContextMenus();
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === "fdownloader-capture-link") {
    const targetUrl = info.linkUrl || info.srcUrl || info.pageUrl;
    if (!targetUrl || targetUrl.startsWith("blob:") || targetUrl.startsWith("data:")) return;

    const alive = await quickIsBridgeAlive();
    if (!alive) return;

    let fileBase64 = null;
    if (isProtectedInvoiceOrSessionUrl(targetUrl, "")) {
      fileBase64 = await fetchFileAsBase64(targetUrl, tab?.url || "");
    }

    const cookies = await getCookiesForUrl(targetUrl, tab?.url || "");
    await sendToFDownloader({
      url: targetUrl,
      filename: "",
      referrer: tab?.url || "",
      cookies: cookies,
      userAgent: navigator.userAgent,
      fileBase64: fileBase64
    });
    return;
  }

  if (info.menuItemId === "fdownloader-capture-page") {
    const targetUrl = info.pageUrl || tab?.url;
    if (!targetUrl || targetUrl.startsWith("blob:") || targetUrl.startsWith("data:")) return;

    const alive = await quickIsBridgeAlive();
    if (!alive) return;

    const cookies = await getCookiesForUrl(targetUrl, tab?.url || "");
    let filename = "";
    if (tab?.title) {
      filename = tab.title.replace(/[\\/:*?"<>|]/g, "_").trim();
      if (targetUrl.toLowerCase().includes(".pdf") && !filename.toLowerCase().endsWith(".pdf")) {
        filename += ".pdf";
      }
    }

    let fileBase64 = null;
    if (isProtectedInvoiceOrSessionUrl(targetUrl, filename)) {
      fileBase64 = await fetchFileAsBase64(targetUrl, tab?.url || "");
    }

    await sendToFDownloader({
      url: targetUrl,
      filename: filename,
      referrer: tab?.url || "",
      cookies: cookies,
      userAgent: navigator.userAgent,
      fileBase64: fileBase64
    });
    return;
  }

  const categoryMap = {
    "fdownloader-extract-pdf": "pdf",
    "fdownloader-extract-archives": "archives",
    "fdownloader-extract-docs": "docs",
    "fdownloader-extract-media": "media",
    "fdownloader-extract-all": "all"
  };

  const category = categoryMap[info.menuItemId];
  if (category && tab?.id) {
    try {
      chrome.tabs.sendMessage(tab.id, {
        type: "FDOWNLOADER_SCAN_LINKS",
        category: category
      }, async (response) => {
        if (chrome.runtime.lastError || !response) return;

        const items = response.links || [];
        if (items.length === 0) return;

        const alive = await quickIsBridgeAlive();
        if (!alive) return;

        const payloadItems = [];
        for (const it of items) {
          const cookies = await getCookiesForUrl(it.url, tab.url || "");
          payloadItems.push({
            url: it.url,
            filename: it.filename || "",
            referrer: tab.url || "",
            cookies: cookies,
            userAgent: navigator.userAgent
          });
        }

        await sendBatchToFDownloader(payloadItems);
      });
    } catch (e) {
      console.warn("Sayfa link tarama hatası:", e);
    }
  }
});
