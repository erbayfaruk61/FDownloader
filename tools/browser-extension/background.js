// ⚡ FDownloader Chrome/Edge/Brave/Firefox Browser Extension (Manifest V3)
const BRIDGE_URL = "http://127.0.0.1:9666";

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

function shouldCapture(downloadItem, allowedExtensions) {
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

let isBridgeConnected = false;
let heartbeatTimer = null;

async function sendHeartbeatToFDownloader() {
  let success = false;
  try {
    const manifest = chrome.runtime.getManifest();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 1500);
    const res = await fetch(`${BRIDGE_URL}/ping`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Extension-Version": manifest.version || "1.0.0"
      },
      body: JSON.stringify({
        version: manifest.version || "1.0.0",
        name: manifest.name || "FDownloader Entegrasyonu"
      }),
      signal: controller.signal
    });
    clearTimeout(timer);
    success = res.ok;
  } catch (e) {
    success = false;
  }

  isBridgeConnected = success;

  // Akıllı dinamik kontrol aralığı:
  // Eğer bağlantı henüz yoksa FDownloader yeni başlatılıyor olabilir; 1.5 saniyede bir hızlı kontrol et.
  // Bağlantı kurulduğunda ise sistemi yormamak için kontrol aralığını 12 saniyeye çıkar.
  const nextInterval = isBridgeConnected ? 12000 : 1500;
  clearTimeout(heartbeatTimer);
  heartbeatTimer = setTimeout(sendHeartbeatToFDownloader, nextInterval);
}

// Service worker başlangıcında hemen çalıştır
sendHeartbeatToFDownloader();

if (chrome.alarms) {
  try {
    chrome.alarms.create("fdownloader_heartbeat", { periodInMinutes: 0.5 });
    chrome.alarms.onAlarm.addListener((alarm) => {
      if (alarm.name === "fdownloader_heartbeat") {
        sendHeartbeatToFDownloader();
      }
    });
  } catch (e) { }
}

try {
  chrome.runtime.onInstalled?.addListener(() => sendHeartbeatToFDownloader());
  chrome.runtime.onStartup?.addListener(() => sendHeartbeatToFDownloader());
  chrome.tabs.onActivated?.addListener(() => {
    if (!isBridgeConnected) sendHeartbeatToFDownloader();
  });
  chrome.windows.onFocusChanged?.addListener((winId) => {
    if (winId !== chrome.windows.WINDOW_ID_NONE && !isBridgeConnected) {
      sendHeartbeatToFDownloader();
    }
  });
} catch (e) { }

async function isBridgeAlive() {
  try {
    const manifest = chrome.runtime.getManifest();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 2500);
    const res = await fetch(`${BRIDGE_URL}/health?v=${encodeURIComponent(manifest.version || "1.0.0")}`, {
      headers: { "X-Extension-Version": manifest.version || "1.0.0" },
      signal: controller.signal
    });
    clearTimeout(timer);
    return res.ok;
  } catch (e) {
    return false;
  }
}

async function getCookiesForUrl(url, referrer) {
  const cookieMap = new Map();
  try {
    if (url && (url.startsWith("http://") || url.startsWith("https://"))) {
      const urlCookies = await chrome.cookies.getAll({ url });
      if (urlCookies) {
        urlCookies.forEach(c => cookieMap.set(c.name, c.value));
      }
    }
  } catch (e) { }

  try {
    if (referrer && (referrer.startsWith("http://") || referrer.startsWith("https://"))) {
      const refCookies = await chrome.cookies.getAll({ url: referrer });
      if (refCookies) {
        refCookies.forEach(c => {
          if (!cookieMap.has(c.name)) {
            cookieMap.set(c.name, c.value);
          }
        });
      }
    }
  } catch (e) { }

  return Array.from(cookieMap.entries())
    .map(([k, v]) => `${k}=${v}`)
    .join("; ");
}

async function sendToFDownloader(item) {
  try {
    const manifest = chrome.runtime.getManifest();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 3500);
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
    console.warn("FDownloader'a gönderilemedi:", err);
    return false;
  }
}

// ─── Alt Tuşu Takibi (Sadece anlık basılı tutuluyorsa devreye girer) ───
let memIsAltDown = false;
let memAltPressedUntil = 0;

// Eski takılı kalmış storage bayraklarını sıfırla
try {
  chrome.storage.local.remove(["isAltDown", "altPressedUntil"]);
} catch (e) { }

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message && message.type === "ALT_EVENT") {
    memIsAltDown = Boolean(message.isDown);
    memAltPressedUntil = memIsAltDown ? (Date.now() + 1500) : 0;
    sendResponse({ ok: true });
    return false;
  }

  if (message && message.type === "INTERCEPT_LINK" && message.url) {
    (async () => {
      const alive = await isBridgeAlive();
      if (!alive) {
        sendResponse({ success: false, reason: "bridge_offline" });
        return;
      }

      const cookies = await getCookiesForUrl(message.url, message.referrer || sender.url || "");
      const captured = await sendToFDownloader({
        url: message.url,
        filename: message.filename || "",
        referrer: message.referrer || sender.url || "",
        cookies: cookies,
        userAgent: navigator.userAgent
      });

      sendResponse({ success: captured });
    })();
    return true; // Asenkron sendResponse için zorunlu
  }
});

// ─── 1. İndirme Yakalama (chrome.downloads.onDeterminingFilename) ───────────

chrome.downloads.onDeterminingFilename.addListener((downloadItem, suggest) => {
  // blob: veya data: linklerini tarayıcı kendisi indirsin
  const rawUrl = downloadItem.finalUrl || downloadItem.url || "";
  if (!rawUrl || rawUrl.startsWith("blob:") || rawUrl.startsWith("data:")) {
    suggest();
    return false;
  }

  chrome.storage.local.get({
    isEnabled: true,
    bypassOnAltKey: true,
    extensions: []
  }, async (settings) => {
    if (!settings.isEnabled) {
      suggest();
      return;
    }

    // Yalnızca Alt tuşuna aktif olarak basılıyorsa tarayıcı kendisi indirsin
    if (settings.bypassOnAltKey !== false) {
      const now = Date.now();
      const isAltActive = memIsAltDown && (now < memAltPressedUntil);

      if (isAltActive) {
        console.log("⚡ FDownloader: Alt tuşu basılı. İndirme doğrudan tarayıcıya bırakıldı.");
        suggest();
        return;
      }
    }

    // Kullanıcıda eski bir kayıt olsa bile DEFAULT_EXTENSIONS daima geçerli kalır
    const allowed = Array.from(new Set([
      ...DEFAULT_EXTENSIONS,
      ...(settings.extensions || [])
    ]));

    const isTargetFile = shouldCapture(downloadItem, allowed);

    if (!isTargetFile) {
      suggest();
      return;
    }

    // FDownloader açık mı kontrol et
    const alive = await isBridgeAlive();
    if (!alive) {
      // Uygulama açık değilse tarayıcı standart indirmeye devam etsin
      suggest();
      return;
    }

    // İlgili domain çerezlerini topla
    const cookies = await getCookiesForUrl(downloadItem.url, downloadItem.referrer);

    // FDownloader'a aktar
    const captured = await sendToFDownloader({
      url: downloadItem.url,
      filename: downloadItem.filename,
      referrer: downloadItem.referrer || "",
      cookies: cookies,
      userAgent: navigator.userAgent,
      totalBytes: downloadItem.fileSize > 0 ? downloadItem.fileSize : null,
      mime: downloadItem.mime || ""
    });

    if (captured) {
      // Tarayıcının kendi indirmesini iptal et ve indirme geçmişinden temizle
      chrome.downloads.cancel(downloadItem.id, () => {
        chrome.downloads.erase({ id: downloadItem.id });
      });
    } else {
      suggest();
    }
  });

  return true; // Asenkron suggest için zorunlu
});

// ─── 2. Sağ Tık Bağlam Menüsü (Context Menu) ────────────────────────────────

chrome.runtime.onInstalled.addListener(() => {
  // Mevcut açık HTTP/HTTPS sekmelerine content.js'i otomatik enjekte et (sayfa yenileme ihtiyacını kaldırır)
  try {
    chrome.tabs.query({ url: ["http://*/*", "https://*/*"] }, (tabs) => {
      if (tabs && tabs.length > 0) {
        for (const tab of tabs) {
          if (tab.id) {
            chrome.scripting.executeScript({
              target: { tabId: tab.id, allFrames: true },
              files: ["content.js"]
            }).catch(() => { });
          }
        }
      }
    });
  } catch (e) { }

  chrome.contextMenus.create({
    id: "fdownloader-capture-link",
    title: "⚡ FDownloader ile İndir",
    contexts: ["link", "video", "audio", "image"]
  });
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === "fdownloader-capture-link") {
    const targetUrl = info.linkUrl || info.srcUrl || info.pageUrl;
    if (!targetUrl || targetUrl.startsWith("blob:") || targetUrl.startsWith("data:")) return;

    const alive = await isBridgeAlive();
    if (!alive) {
      return;
    }

    const cookies = await getCookiesForUrl(targetUrl, tab?.url || "");

    await sendToFDownloader({
      url: targetUrl,
      filename: "",
      referrer: tab?.url || "",
      cookies: cookies,
      userAgent: navigator.userAgent
    });
  }
});
