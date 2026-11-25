// ⚡ FDownloader - Content Script (Alt Tuşu İzleyici & Akıllı İndirme Yakalayıcı)
(function () {
  function isExtensionValid() {
    try {
      return Boolean(chrome && chrome.runtime && chrome.runtime.id);
    } catch (e) {
      return false;
    }
  }

    let localAltUntil = 0;

  function updateAltState(isDown) {
    if (isDown) {
      localAltUntil = Math.max(localAltUntil, Date.now() + 5000);
    }
    if (!isExtensionValid()) return;
    try {
      chrome.runtime.sendMessage({
        type: "ALT_EVENT",
        isDown: Boolean(isDown),
        until: Date.now() + 5000
      }, () => {
        // Alıcı dinleyici hazır değilse veya uyuyorsa hatayı sessizce yut
        if (chrome.runtime.lastError) { /* ignore */ }
      });
    } catch (e) { }
  }

  // 1. Klavye basışları (Alt tuşuna basıldığında)
  window.addEventListener("keydown", (e) => {
    if (e.key === "Alt" || e.code === "AltLeft" || e.code === "AltRight" || e.altKey) {
      updateAltState(true);
    }
  }, true);

  // 2. Klavye bırakılması (Alt bırakıldığında)
  window.addEventListener("keyup", (e) => {
    if (e.key === "Alt" || e.code === "AltLeft" || e.code === "AltRight") {
      updateAltState(false);
    }
  }, true);

  // 3. Mouse ve Tıklama (Alt basılı tutularak tıklandığında)
  window.addEventListener("pointerdown", (e) => {
    if (e.altKey || e.key === "Alt") {
      updateAltState(true);
    }
  }, true);

  window.addEventListener("mousedown", (e) => {
    if (e.altKey || e.key === "Alt") {
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

  // Tıklama akışı tarayıcının yerel mekanizmasına bırakılır;
  // FDownloader indirmeyi chrome.downloads.onDeterminingFilename üzerinden merkezi ve gecikmesiz yakalar.
  document.addEventListener("click", (e) => {
    if (e.altKey) {
      updateAltState(true);
    }
  }, true);

  // ─── 6. IDM Tarzı Video Köşesinde İndirme Paneli (Floating Video Grabber) ───
  let currentActiveVideo = null;
  let panelHideTimer = null;
  let shadowRoot = null;
  let panelContainer = null;
  let buttonEl = null;
  let isHoveringVideoOrButton = false;
  let isDirectlyHoveringButton = false;
  let inactivityTimer = null;
  const INACTIVITY_TIMEOUT_MS = 2400;


  let grabberSettings = {
    grabberPosition: "outside-top-right",
    grabberHoverOnly: true,
    grabberHoverOpacity: 100,
    grabberIdleOpacity: 0
  };

  try {
    if (isExtensionValid() && chrome.storage && chrome.storage.local) {
      chrome.storage.local.get({
        grabberPosition: "outside-top-right",
        grabberHoverOnly: true,
        grabberHoverOpacity: 100,
        grabberIdleOpacity: 0
      }, (res) => {
        if (chrome.runtime.lastError) return;
        if (res) grabberSettings = Object.assign(grabberSettings, res);
      });

      chrome.storage.onChanged.addListener((changes, area) => {
        if (area === "local") {
          for (let key in changes) {
            if (key in grabberSettings) {
              grabberSettings[key] = changes[key].newValue;
            }
          }
          if (currentActiveVideo) {
            if (grabberSettings.grabberHoverOnly && !isHoveringVideoOrButton) {
              hideGrabberImmediately();
            } else {
              updateGrabberPosition(currentActiveVideo);
            }
          }
        }
      });
    }
  } catch (e) { }

  function ensureGrabberHost() {
    try {
      let host = document.getElementById("fdownloader-video-host");
      if (!host || !shadowRoot || !panelContainer) {
        if (host) {
          try { host.remove(); } catch (e) { }
        }

        const targetParent = document.body || document.documentElement;
        if (!targetParent) return;

        host = document.createElement("div");
        host.id = "fdownloader-video-host";
        host.style.position = "absolute";
        host.style.top = "0";
        host.style.left = "0";
        host.style.width = "0";
        host.style.height = "0";
        host.style.zIndex = "2147483647";
        host.style.pointerEvents = "none";
        targetParent.appendChild(host);

        shadowRoot = host.attachShadow({ mode: "open" });

        const style = document.createElement("style");
        style.textContent = `
          .fdown-wrap {
            position: fixed;
            display: none;
            pointer-events: auto;
            z-index: 2147483647;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
            user-select: none;
            transition: opacity 0.18s ease, transform 0.18s ease;
            opacity: 0;
            transform: translateY(-2px);
          }
          .fdown-wrap.visible {
            display: flex;
            transform: translateY(0);
          }
          .fdown-btn {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            background: rgba(15, 23, 42, 0.92);
            backdrop-filter: blur(8px);
            -webkit-backdrop-filter: blur(8px);
            color: #F8FAFC;
            border: 1px solid rgba(56, 189, 248, 0.65);
            border-radius: 6px;
            padding: 5px 10px;
            font-size: 11px;
            font-weight: 600;
            cursor: pointer;
            box-shadow: 0 4px 14px rgba(0, 0, 0, 0.4), 0 0 8px rgba(56, 189, 248, 0.25);
            transition: all 0.15s ease;
          }
          .fdown-btn:hover {
            background: #0284C7;
            border-color: #38BDF8;
            box-shadow: 0 4px 18px rgba(2, 132, 199, 0.5), 0 0 12px rgba(56, 189, 248, 0.45);
            transform: scale(1.03);
            color: #FFFFFF;
          }
          .fdown-btn:active {
            transform: scale(0.97);
          }
          .fdown-logo {
            width: 18px;
            height: 18px;
            border-radius: 4px;
            object-fit: contain;
            vertical-align: middle;
            display: block;
          }
          .fdown-badge {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            background: linear-gradient(135deg, #0EA5E9, #38BDF8);
            color: #FFFFFF;
            border-radius: 4px;
            width: 17px;
            height: 17px;
            font-size: 11px;
            font-weight: 800;
          }
        `;
        shadowRoot.appendChild(style);

        panelContainer = document.createElement("div");
        panelContainer.className = "fdown-wrap";

        buttonEl = document.createElement("button");
        buttonEl.className = "fdown-btn";

        const logoContainer = document.createElement("span");
        logoContainer.className = "fdown-logo-slot";
        logoContainer.style.display = "inline-flex";
        logoContainer.style.alignItems = "center";
        logoContainer.style.justifyContent = "center";
        logoContainer.style.width = "18px";
        logoContainer.style.height = "18px";
        logoContainer.style.flexShrink = "0";
        // Güncel FDownloader logosu
        const logoImg = document.createElement("img");
        logoImg.className = "fdown-logo";
        logoImg.width = 18;
        logoImg.height = 18;
        logoImg.alt = "FDownloader";
        try {
          logoImg.src = (typeof chrome !== "undefined" && chrome.runtime && chrome.runtime.getURL)
            ? chrome.runtime.getURL("icons/icon-48.png")
            : "icons/icon-48.png";
        } catch (_) {
          logoImg.src = "icons/icon-48.png";
        }
        logoContainer.appendChild(logoImg);

        buttonEl.appendChild(logoContainer);

        const labelSpan = document.createElement("span");
        labelSpan.className = "fdown-label";
        labelSpan.textContent = "Bu Videoyu İndir";
        buttonEl.appendChild(labelSpan);

        buttonEl.addEventListener("click", onGrabberButtonClick);
        panelContainer.addEventListener("mouseenter", () => {
          isDirectlyHoveringButton = true;
          isHoveringVideoOrButton = true;
          if (panelHideTimer) clearTimeout(panelHideTimer);
          if (inactivityTimer) clearTimeout(inactivityTimer);
          if (panelContainer && panelContainer.style) {
            const hoverOp = (grabberSettings.grabberHoverOpacity ?? 100) / 100;
            panelContainer.style.opacity = hoverOp.toString();
            panelContainer.style.pointerEvents = "auto";
          }
        });
        panelContainer.addEventListener("mousemove", () => {
          isDirectlyHoveringButton = true;
          isHoveringVideoOrButton = true;
          if (panelHideTimer) clearTimeout(panelHideTimer);
          if (inactivityTimer) clearTimeout(inactivityTimer);
        });
        panelContainer.addEventListener("mouseleave", () => {
          isDirectlyHoveringButton = false;
          scheduleHideGrabber();
        });

        panelContainer.appendChild(buttonEl);
        shadowRoot.appendChild(panelContainer);
      }
    } catch (err) { }
  }

  function isYouTubeVideoPage() {
    const host = window.location.hostname.toLowerCase();
    if (!host.includes("youtube.com") && !host.includes("youtu.be")) return true;
    if (host.includes("youtu.be")) return window.location.pathname.length > 1;
    const path = window.location.pathname.toLowerCase();
    return path === "/watch" ||
      path.startsWith("/shorts/") ||
      path.startsWith("/live/") ||
      path.startsWith("/embed/");
  }

  function isVideoDrmProtected(video) {
    if (!video) return false;
    if (video._fdownloader_has_drm === true) return true;
    if (video.mediaKeys != null) return true;
    return false;
  }

  function findManifestUrlInPage() {
    try {
      const resources = performance.getEntriesByType("resource");
      for (let i = resources.length - 1; i >= 0; i--) {
        const name = resources[i].name || "";
        const cleanUrl = name.split(/[?#]/)[0].toLowerCase();
        if (cleanUrl.endsWith(".m3u8") || cleanUrl.endsWith(".mpd")) {
          return name;
        }
      }
    } catch (e) { }
    return null;
  }

  function resolveVideoTarget(video) {
    if (isVideoDrmProtected(video)) {
      return { isDrm: true, url: null, filename: null };
    }

    const host = window.location.hostname.toLowerCase();
    const isYouTube = host.includes("youtube.com") || host.includes("youtu.be");
    if (isYouTube) {
      if (!isYouTubeVideoPage()) {
        return { url: null, filename: null };
      }
      return {
        url: window.location.href,
        filename: document.title ? document.title.replace(/\s*-\s*YouTube$/i, "").trim() + ".mp4" : "YouTube Video.mp4"
      };
    }

    const isFacebook = host.includes("facebook.com") || host.includes("fb.watch");
    if (isFacebook) {
      // 1. Feed veya kart üzerindeki kalıcı Reel / Watch / Post video URL'sini bul (yt-dlp için en stabil ve kopmayan yöntem)
      let postLink = null;
      try {
        const container = video.closest("div[role='feed'], div[role='article'], div[data-pagelet], div[aria-label*='Video'], div.x1y1aw1k, [data-video-id], div[data-nosnippet]") || video.parentElement;
        if (container) {
          // Öncelikle sayısal ID içeren net video/reel linklerini ara (örn: /reel/12345 veya /watch/?v=12345)
          const links = container.querySelectorAll("a[href*='/reel/'], a[href*='/watch/'], a[href*='/videos/'], a[href*='fb.watch']");
          for (const a of links) {
            const h = a.href || "";
            // Hashtag ve arama sayfalarını hariç tut
            if (h.includes("/hashtag/") || h.includes("/search/")) continue;
            if (/\/(reel\/\d+|watch\/?\?v=\d+|videos\/\d+|[a-zA-Z0-9\.]+\/videos\/\d+)/.test(h) || h.includes("fb.watch/")) {
              postLink = h;
              break;
            }
            if (!postLink && !h.includes("/hashtag/")) {
              postLink = h;
            }
          }
        }
      } catch (e) { }

      // 2. Sayfa URL'sinin kendisi geçerli bir video/reel sayfası mı? (Hashtag/Search hariç)
      const loc = window.location.href;
      const isWatchOrReelPage = (loc.includes("/reel/") || loc.includes("/watch/") || loc.includes("/videos/") || loc.includes("fb.watch")) &&
                                !loc.includes("/hashtag/") && !loc.includes("/search/");
      const bestMediaUrl = postLink || (isWatchOrReelPage ? loc : null);

      if (bestMediaUrl) {
        return {
          url: bestMediaUrl,
          filename: document.title ? document.title.replace(/\s*\|\s*Facebook$/i, "").trim() + ".mp4" : "Facebook Video.mp4"
        };
      }

      // 3. Doğrudan video elementi src veya currentSrc fbcdn / mp4 URL'si içeriyorsa
      const src = video.currentSrc || video.src || "";
      if (src && !src.startsWith("blob:") && !src.startsWith("data:")) {
        return {
          url: src,
          filename: document.title ? document.title.replace(/\s*\|\s*Facebook$/i, "").trim() + ".mp4" : "Facebook Video.mp4"
        };
      }

      // 4. Blob video ise performans akışındaki en son fbcdn .mp4 kaynağını bul
      try {
        const entries = performance.getEntriesByType("resource");
        for (let i = entries.length - 1; i >= 0; i--) {
          const entryName = entries[i].name || "";
          if (entryName.includes("fbcdn.net") && (entryName.includes(".mp4") || entryName.includes("bytestart="))) {
            return {
              url: entryName,
              filename: document.title ? document.title.replace(/\s*\|\s*Facebook$/i, "").trim() + ".mp4" : "Facebook Video.mp4"
            };
          }
        }
      } catch (_) { }

      // 5. Son çare: Post linki veya mevcut sayfa URL'si
      return {
        url: postLink || window.location.href,
        filename: document.title ? document.title.replace(/\s*\|\s*Facebook$/i, "").trim() + ".mp4" : "Facebook Video.mp4"
      };
    }

    const isInstagram = host.includes("instagram.com");
    if (isInstagram) {
      // 1. Gönderi/Reel kartı içinden kalıcı /reel/, /p/ veya /tv/ bağlantısını DOM'dan çek
      let igPostLink = null;
      const igPermalinkRegex = /(?:^|\/)(?:p|reel|reels|tv)\/[A-Za-z0-9_-]+/;
      const extractIgLink = (root) => {
        if (!root) return null;
        const links = root.querySelectorAll("a[href*='/reel/'], a[href*='/reels/'], a[href*='/p/'], a[href*='/tv/']");
        for (const a of links) {
          try {
            const u = new URL(a.href, window.location.origin);
            if (igPermalinkRegex.test(u.pathname)) {
              return u.href;
            }
          } catch (_) { }
        }
        return null;
      };

      try {
        const article = video.closest("article");
        if (article) {
          igPostLink = extractIgLink(article);
        }
        if (!igPostLink) {
          let el = video.parentElement;
          for (let depth = 0; el && depth < 10; depth++, el = el.parentElement) {
            if (el.tagName === "MAIN" || el.tagName === "BODY" || el.tagName === "HTML") break;
            igPostLink = extractIgLink(el);
            if (igPostLink) break;
          }
        }
      } catch (_) { }

      // 2. Sayfa URL'si tekil bir gönderi/reel sayfasıysa kullan
      const isIgPostPage = igPermalinkRegex.test(window.location.pathname);
      const bestIgUrl = igPostLink || (isIgPostPage ? window.location.href : null);
      if (bestIgUrl) {
        return { url: bestIgUrl, filename: "Instagram Video.mp4" };
      }

      // 3. Doğrudan mp4/cdn src varsa kullan
      const src = video.currentSrc || video.src || "";
      if (src && !src.startsWith("blob:") && !src.startsWith("data:")) {
        return { url: src, filename: "Instagram Video.mp4" };
      }

      return { url: window.location.href, filename: "Instagram Video.mp4" };
    }

    const src = video.currentSrc || video.src || "";
    if (src && !src.startsWith("blob:") && !src.startsWith("data:")) {
      let candidateName = "";
      try {
        const u = new URL(src, window.location.href);
        candidateName = u.pathname.split("/").pop() || "";
      } catch (e) { }

      const cleanLower = src.split(/[?#]/)[0].toLowerCase();
      const isStream = cleanLower.endsWith(".m3u8") || cleanLower.endsWith(".mpd");
      const ext = isStream ? ".mp4" : "";

      return {
        url: src,
        filename: candidateName || (document.title ? document.title.trim() + (ext || ".mp4") : "Video" + (ext || ".mp4")),
        isStream: isStream
      };
    }

    // Blob tabanlı akış ise sayfada yüklenen HLS (.m3u8) veya DASH (.mpd) manifestini bul
    if (src.startsWith("blob:")) {
      const manifestUrl = findManifestUrlInPage();
      if (manifestUrl) {
        return {
          url: manifestUrl,
          filename: document.title ? document.title.trim() + ".mp4" : "Stream.mp4",
          isStream: true
        };
      }
    }

    // <source> çocuklarını kontrol et
    try {
      const sources = video.querySelectorAll("source");
      for (const s of sources) {
        const sSrc = s.src || "";
        if (sSrc && !sSrc.startsWith("blob:") && !sSrc.startsWith("data:")) {
          return {
            url: sSrc,
            filename: document.title ? document.title.trim() + ".mp4" : "Video.mp4"
          };
        }
      }
    } catch (e) { }

    return {
      url: window.location.href,
      filename: document.title ? document.title.trim() + ".mp4" : "Video.mp4"
    };
  }

  function onGrabberButtonClick(e) {
    e.preventDefault();
    e.stopPropagation();

    if (!isExtensionValid() || !currentActiveVideo || !buttonEl) return;

    const target = resolveVideoTarget(currentActiveVideo);
    if (target.isDrm) {
      const label = buttonEl.querySelector(".fdown-label");
      if (label) label.textContent = "🔒 Korumalı İçerik (DRM İndirilemez)";
      setTimeout(() => {
        if (label) label.textContent = "Bu Videoyu İndir";
      }, 3000);
      return;
    }
    if (!target.url) return;

    const label = buttonEl.querySelector(".fdown-label");
    const originalText = label ? label.textContent : "Bu Videoyu İndir";
    if (label) label.textContent = "⚡ FDownloader'a Gönderiliyor...";

    chrome.runtime.sendMessage({
      type: "INTERCEPT_LINK",
      url: target.url,
      filename: target.filename,
      referrer: window.location.href
    }, () => {
      if (chrome.runtime.lastError) { /* ignore */ }
      setTimeout(() => {
        if (label) label.textContent = originalText;
        hideGrabberImmediately();
      }, 900);
    });
  }

  function updateGrabberPosition(video) {
    if (!video || !panelContainer || !panelContainer.style) return;
    if (!video.isConnected) {
      hideGrabberImmediately();
      currentActiveVideo = null;
      return;
    }

    const rect = video.getBoundingClientRect();
    if (rect.width < 140 || rect.height < 90) {
      hideGrabberImmediately();
      return;
    }

    if (rect.bottom < 0 || rect.top > window.innerHeight || rect.right < 0 || rect.left > window.innerWidth) {
      hideGrabberImmediately();
      return;
    }

    const pos = grabberSettings.grabberPosition || "outside-top-right";
    const btnHeight = 32;

    let top = 10;
    if (pos.startsWith("outside")) {
      if (rect.top - btnHeight - 4 >= 4) {
        top = rect.top - btnHeight - 4;
      } else {
        top = Math.max(6, rect.top + 8);
      }
    } else {
      top = Math.max(6, rect.top + 8);
    }

    if (pos.endsWith("left")) {
      const left = pos.startsWith("outside")
        ? Math.max(6, rect.left + 2)
        : Math.max(6, rect.left + 8);
      panelContainer.style.left = `${left}px`;
      panelContainer.style.right = "auto";
    } else {
      const right = pos.startsWith("outside")
        ? Math.max(6, window.innerWidth - rect.right + 2)
        : Math.max(6, window.innerWidth - rect.right + 8);
      panelContainer.style.right = `${right}px`;
      panelContainer.style.left = "auto";
    }

    panelContainer.style.top = `${top}px`;
    panelContainer.classList.add("visible");
  }

  function showGrabberForVideo(video) {
    if (!video || !video.isConnected) return;
    if (!isYouTubeVideoPage()) {
      hideGrabberImmediately();
      return;
    }
    ensureGrabberHost();
    if (!panelContainer || !panelContainer.style) return;

    if (panelHideTimer) clearTimeout(panelHideTimer);
    currentActiveVideo = video;
    updateGrabberPosition(video);
    isHoveringVideoOrButton = true;
    const hoverOp = (grabberSettings.grabberHoverOpacity ?? 100) / 100;
    panelContainer.style.opacity = hoverOp.toString();
    panelContainer.style.pointerEvents = "auto";
  }

  function onVideoMouseMove(video) {
    showGrabberForVideo(video);

    if (inactivityTimer) clearTimeout(inactivityTimer);

    if (grabberSettings.grabberHoverOnly) {
      inactivityTimer = setTimeout(() => {
        if (!isDirectlyHoveringButton) {
          scheduleHideGrabber();
        }
      }, INACTIVITY_TIMEOUT_MS);
    }
  }

  function onVideoMouseLeave() {
    if (inactivityTimer) clearTimeout(inactivityTimer);
    setTimeout(() => {
      if (!isDirectlyHoveringButton) {
        scheduleHideGrabber();
      }
    }, 120);
  }

  function scheduleHideGrabber() {
    isHoveringVideoOrButton = false;
    if (panelHideTimer) clearTimeout(panelHideTimer);
    panelHideTimer = setTimeout(() => {
      if (isHoveringVideoOrButton || isDirectlyHoveringButton) return;
      if (grabberSettings.grabberHoverOnly) {
        hideGrabberImmediately();
      } else {
        const idleOp = (grabberSettings.grabberIdleOpacity ?? 0) / 100;
        if (idleOp <= 0) {
          hideGrabberImmediately();
        } else {
          if (panelContainer && panelContainer.style) {
            panelContainer.style.opacity = idleOp.toString();
            panelContainer.style.pointerEvents = "auto";
          }
        }
      }
    }, 450);
  }

  function hideGrabberImmediately() {
    if (panelContainer && panelContainer.style) {
      panelContainer.style.opacity = "0";
      panelContainer.style.pointerEvents = "none";
      setTimeout(() => {
        if (!isHoveringVideoOrButton && !isDirectlyHoveringButton && panelContainer) {
          panelContainer.classList.remove("visible");
        }
      }, 200);
    }
  }

  function attachVideoListeners(video) {
    if (!video || video._fdownloader_attached) return;
    video._fdownloader_attached = true;

    video.addEventListener("encrypted", () => {
      video._fdownloader_has_drm = true;
    });
    video.addEventListener("play", () => {
      showGrabberForVideo(video);
    });
    video.addEventListener("mouseenter", () => onVideoMouseMove(video));
    video.addEventListener("mousemove", () => onVideoMouseMove(video));
    video.addEventListener("mouseleave", () => onVideoMouseLeave());
  }

  let lastGlobalCheckTime = 0;
  function onGlobalMouseMove(e) {
    if (!grabberSettings.grabberEnabled) return;
    if (isDirectlyHoveringButton) return;

    const now = Date.now();
    if (now - lastGlobalCheckTime < 45) return;
    lastGlobalCheckTime = now;

    const x = e.clientX;
    const y = e.clientY;

    let foundVideo = null;
    try {
      if (typeof document.elementsFromPoint === "function") {
        const elements = document.elementsFromPoint(x, y);
        for (let i = 0; i < elements.length; i++) {
          const el = elements[i];
          if (el.tagName === "VIDEO") {
            foundVideo = el;
            break;
          }
          if (el.querySelector && el.querySelector("video")) {
            const v = el.querySelector("video");
            const vr = v.getBoundingClientRect();
            if (x >= vr.left && x <= vr.right && y >= vr.top && y <= vr.bottom) {
              foundVideo = v;
              break;
            }
          }
        }
      }
    } catch (_) { }

    if (!foundVideo) {
      const allVideos = document.querySelectorAll("video");
      for (let i = 0; i < allVideos.length; i++) {
        const v = allVideos[i];
        if (!v.isConnected) continue;
        const rect = v.getBoundingClientRect();
        if (rect.width >= 140 && rect.height >= 90) {
          if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) {
            foundVideo = v;
            break;
          }
        }
      }
    }

    if (foundVideo) {
      attachVideoListeners(foundVideo);
      showGrabberForVideo(foundVideo);

      if (inactivityTimer) clearTimeout(inactivityTimer);
      if (grabberSettings.grabberHoverOnly) {
        inactivityTimer = setTimeout(() => {
          if (!isDirectlyHoveringButton) {
            scheduleHideGrabber();
          }
        }, INACTIVITY_TIMEOUT_MS);
      }
    } else {
      if (isHoveringVideoOrButton && !isDirectlyHoveringButton) {
        scheduleHideGrabber();
      }
    }
  }

  function scanForVideos() {
    try {
      const videos = document.querySelectorAll("video");
      videos.forEach(attachVideoListeners);
    } catch (e) { }
  }

  function startVideoTracking() {
    scanForVideos();

    const targetNode = document.documentElement || document.body;
    if (targetNode) {
      try {
        const observer = new MutationObserver(() => scanForVideos());
        observer.observe(targetNode, { childList: true, subtree: true });
      } catch (e) { }
    }

    // Katmanları (overlay) delen global fare takibi
    document.addEventListener("mousemove", onGlobalMouseMove, { passive: true });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", startVideoTracking, { once: true });
  } else {
    startVideoTracking();
  }

  window.addEventListener("scroll", () => {
    if (currentActiveVideo && panelContainer && panelContainer.classList.contains("visible")) {
      updateGrabberPosition(currentActiveVideo);
    }
  }, { passive: true });

  window.addEventListener("resize", () => {
    if (currentActiveVideo && panelContainer && panelContainer.classList.contains("visible")) {
      updateGrabberPosition(currentActiveVideo);
    }
  }, { passive: true });

  // FDownloader Service Worker ve Köprü senkronizasyonu
  function pingExtensionBridge() {
    try {
      if (typeof chrome !== "undefined" && chrome.runtime && chrome.runtime.sendMessage) {
        chrome.runtime.sendMessage({ type: "PING_BRIDGE" }).catch(() => {});
      }
    } catch (_) { }
  }
  pingExtensionBridge();
  setInterval(pingExtensionBridge, 15000);

  // ─── Sayfadan Dosya Tarama & Bildirim (Context Menu Handler) ─────────────────
  function extractPageLinksByCategory(category) {
    const result = [];
    const seenUrls = new Set();

    const pdfExts = new Set(["pdf"]);
    const archiveExts = new Set(["zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso", "img", "bin", "dat", "cab", "wim"]);
    const docExts = new Set(["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "txt", "rtf", "csv", "epub", "mobi"]);
    const mediaExts = new Set(["mp4", "mkv", "avi", "mov", "flv", "webm", "mp3", "flac", "wav", "aac", "m4a", "ogg", "opus", "m3u8"]);

    function matchCategory(url) {
      if (!url || url.startsWith("javascript:") || url.startsWith("data:") || url.startsWith("blob:") || url.startsWith("#")) return false;
      let clean = url.split("?")[0].split("#")[0].toLowerCase();
      let extMatch = clean.match(/\.([a-z0-9]{2,5})$/i);
      let ext = extMatch ? extMatch[1] : "";

      if (category === "pdf") {
        return ext === "pdf" || clean.endsWith(".pdf") || url.toLowerCase().includes("application/pdf") || url.toLowerCase().includes(".pdf?");
      }
      if (category === "archives") {
        return archiveExts.has(ext);
      }
      if (category === "docs") {
        return docExts.has(ext);
      }
      if (category === "media") {
        return mediaExts.has(ext);
      }
      if (category === "all") {
        return (ext && (pdfExts.has(ext) || archiveExts.has(ext) || docExts.has(ext) || mediaExts.has(ext))) || clean.endsWith(".pdf");
      }
      return false;
    }

    // 1. a[href] etiketleri
    const anchors = document.querySelectorAll("a[href]");
    for (const a of anchors) {
      try {
        const fullUrl = new URL(a.href, document.baseURI).href;
        if (!seenUrls.has(fullUrl) && matchCategory(fullUrl)) {
          seenUrls.add(fullUrl);
          let fn = a.download || a.title || a.textContent || "";
          fn = fn.trim().replace(/[\\/:*?"<>|\r\n\t]+/g, " ");
          result.push({ url: fullUrl, filename: fn });
        }
      } catch (e) { }
    }

    // 2. embed / iframe / object etiketleri
    const embeds = document.querySelectorAll("embed[src], iframe[src], object[data]");
    for (const el of embeds) {
      try {
        const src = el.src || el.data;
        if (src) {
          const fullUrl = new URL(src, document.baseURI).href;
          if (!seenUrls.has(fullUrl) && matchCategory(fullUrl)) {
            seenUrls.add(fullUrl);
            result.push({ url: fullUrl, filename: "" });
          }
        }
      } catch (e) { }
    }

    // 3. Mevcut sayfa URL'si doğrudan eşleşiyorsa (örneğin açık sekmede bir PDF açılmışsa)
    try {
      const loc = window.location.href;
      if (!seenUrls.has(loc) && matchCategory(loc)) {
        seenUrls.add(loc);
        result.unshift({ url: loc, filename: document.title || "" });
      }
    } catch (e) { }

    return result;
  }

  function showInPageToast(message, isWarning) {
    try {
      let toast = document.getElementById("fdownloader-page-toast");
      if (toast) toast.remove();

      toast = document.createElement("div");
      toast.id = "fdownloader-page-toast";
      toast.style.position = "fixed";
      toast.style.bottom = "24px";
      toast.style.right = "24px";
      toast.style.zIndex = "2147483647";
      toast.style.padding = "10px 16px";
      toast.style.borderRadius = "8px";
      toast.style.fontFamily = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif";
      toast.style.fontSize = "12px";
      toast.style.fontWeight = "600";
      toast.style.color = "#FFFFFF";
      toast.style.background = isWarning ? "rgba(239, 68, 68, 0.95)" : "rgba(15, 23, 42, 0.95)";
      toast.style.border = isWarning ? "1px solid #EF4444" : "1px solid #38BDF8";
      toast.style.boxShadow = "0 8px 24px rgba(0, 0, 0, 0.4)";
      toast.style.backdropFilter = "blur(8px)";
      toast.style.transition = "opacity 0.3s ease, transform 0.3s ease";
      toast.style.transform = "translateY(0)";
      toast.textContent = message;

      const targetParent = document.body || document.documentElement;
      if (targetParent) {
        targetParent.appendChild(toast);
        setTimeout(() => {
          if (toast && toast.parentNode) {
            toast.style.opacity = "0";
            toast.style.transform = "translateY(10px)";
            setTimeout(() => toast.remove(), 350);
          }
        }, 3000);
      }
    } catch (e) { }
  }

  chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg && msg.type === "FDOWNLOADER_SCAN_LINKS") {
      const categoryNames = {
        "pdf": "PDF",
        "archives": "Arşiv",
        "docs": "Ofis Belgesi",
        "media": "Medya",
        "all": "Dosya"
      };
      const catTitle = categoryNames[msg.category] || "Dosya";
      const links = extractPageLinksByCategory(msg.category);
      if (links.length === 0) {
        showInPageToast(`⚠️ Bu sayfada indirilebilecek ${catTitle} bağlantısı bulunamadı.`, true);
      } else {
        showInPageToast(`⚡ ${links.length} adet ${catTitle} FDownloader'a aktarılıyor...`, false);
      }
      sendResponse({ links: links });
      return true;
    }
  });
})();
