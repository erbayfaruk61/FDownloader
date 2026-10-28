// ⚡ FDownloader - Content Script (Alt Tuşu İzleyici & Akıllı İndirme Yakalayıcı)
(function () {
  function isExtensionValid() {
    try {
      return Boolean(chrome && chrome.runtime && chrome.runtime.id);
    } catch (e) {
      return false;
    }
  }

  function updateAltState(isDown) {
    if (!isExtensionValid()) return;
    try {
      chrome.runtime.sendMessage({
        type: "ALT_EVENT",
        isDown: Boolean(isDown)
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
    if (e.altKey) return;
    if (!isExtensionValid()) return;

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
        if (chrome.runtime.lastError || !res || !res.success) {
          window.location.href = href;
        }
      });
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

  const FDOWN_LOGO_DATA_URI = "data:image/png;base64,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";

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

        // CSP ve iframe kısıtlamalarına takılmayan, sıfır gecikmeli şık vektörel FDownloader logosu
        logoContainer.innerHTML = `<svg width="18" height="18" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg" style="border-radius:4px;flex-shrink:0;vertical-align:middle;display:block;">
          <rect width="32" height="32" rx="7" fill="#0A0F1D"/>
          <path d="M16 4C16 4 23 6 25 13C26 16.5 24.5 19.5 22 22L19 19L17.5 20.5L20.5 23.5C18.5 25.5 15.5 27 12 26C5 24 4 17 4 17C4 17 10 16 16 4Z" fill="#38BDF8"/>
          <circle cx="17.5" cy="11.5" r="2.5" fill="#FFFFFF"/>
          <path d="M8 24C8 24 10.5 21.5 12 19.5L13.5 21C11.5 22.5 8 24 8 24Z" fill="#F59E0B"/>
          <path d="M6 26C6 26 8 24 9.5 22L11 23.5C9 25.5 6 26 6 26Z" fill="#EF4444"/>
        </svg>`;

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

  function resolveVideoTarget(video) {
    const isYouTube = window.location.hostname.includes("youtube.com") || window.location.hostname.includes("youtu.be");
    if (isYouTube) {
      return {
        url: window.location.href,
        filename: document.title ? document.title.replace(/\s*-\s*YouTube$/i, "").trim() + ".mp4" : "YouTube Video.mp4"
      };
    }

    const src = video.currentSrc || video.src || "";
    if (src && !src.startsWith("blob:") && !src.startsWith("data:")) {
      let candidateName = "";
      try {
        const u = new URL(src, window.location.href);
        candidateName = u.pathname.split("/").pop() || "";
      } catch (e) { }

      return {
        url: src,
        filename: candidateName || (document.title ? document.title.trim() + ".mp4" : "Video.mp4")
      };
    }

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

    video.addEventListener("mouseenter", () => onVideoMouseMove(video));
    video.addEventListener("mousemove", () => onVideoMouseMove(video));
    video.addEventListener("mouseleave", () => onVideoMouseLeave());
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
})();
