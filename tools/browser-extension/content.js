// ⚡ FDownloader - Content Script (Alt Tuşu İzleyici & Akıllı İndirme Yakalayıcı)
(function () {
  function updateAltState(isDown) {
    try {
      if (!chrome.runtime || !chrome.runtime.id) return;
      chrome.runtime.sendMessage({
        type: "ALT_EVENT",
        isDown: Boolean(isDown)
      }).catch(() => { });
    } catch (e) { }
  }

  // 1. Klavye basışları (Alt tuşuna basıldığında)
  window.addEventListener("keydown", (e) => {
    if (e.key === "Alt" || e.code === "AltLeft" || e.code === "AltRight" || e.altKey) {
      updateAltState(true);
    }
  }, true);

  // 2. Klavye bırakılması (Alt bırakıldıktan sonra 5 sn daha geçerli kalır)
  window.addEventListener("keyup", (e) => {
    if (e.key === "Alt" || e.code === "AltLeft" || e.code === "AltRight") {
      updateAltState(false);
    }
  }, true);

  // 3. Mouse ve Tıklama (Alt basılı tutularak tıklandığında)
  window.addEventListener("mousedown", (e) => {
    if (e.altKey) {
      updateAltState(true);
    }
  }, true);

  window.addEventListener("click", (e) => {
    if (e.altKey) {
      updateAltState(true);
    }
  }, true);

  // 5. İndirilebilir Link Tıklamalarını Doğrudan Yakalama (IDM Tarzı Kesintisiz Yakalama)
  const INTERCEPT_EXTENSIONS = new Set([
    "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "dat", "bin", "iso", "img",
    "exe", "msi", "apk", "xapk", "dmg", "pkg", "deb", "rpm",
    "mp4", "mkv", "avi", "mov", "mp3", "flac", "wav", "pdf", "torrent"
  ]);

  function getLinkExtension(urlStr) {
    try {
      if (!urlStr || urlStr.startsWith("javascript:") || urlStr.startsWith("#")) return null;
      const cleanPath = urlStr.split(/[?#]/)[0];
      const dot = cleanPath.lastIndexOf(".");
      if (dot !== -1 && dot < cleanPath.length - 1) {
        const ext = cleanPath.substring(dot + 1).toLowerCase().trim();
        if (ext && ext.length <= 8) return ext;
      }
    } catch (e) { }
    return null;
  }

  document.addEventListener("click", (e) => {
    // Alt tuşu basılıysa tarayıcı kendisi indirsin
    if (e.altKey) return;

    // Tıklanan eleman veya üst kapsayıcı bir bağlantı (<a>) mı?
    const link = e.target.closest("a");
    if (!link || !link.href) return;

    const href = link.href;
    if (!href.startsWith("http://") && !href.startsWith("https://")) return;

    const ext = getLinkExtension(href);
    if (ext && INTERCEPT_EXTENSIONS.has(ext)) {
      e.preventDefault();
      e.stopImmediatePropagation();

      chrome.runtime.sendMessage({
        type: "INTERCEPT_LINK",
        url: href,
        filename: link.download || "",
        referrer: window.location.href
      }, (res) => {
        if (!res || !res.success) {
          // FDownloader yanıt vermezse standart tarayıcı indirmesine devam et
          window.location.href = href;
        }
      });
    }
  }, true);
})();
