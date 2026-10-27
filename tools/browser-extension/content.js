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

  // ─── 6. IDM Tarzı Video Köşesinde İndirme Paneli (Floating Video Grabber) ───
  let currentActiveVideo = null;
  let panelHideTimer = null;
  let shadowRoot = null;
  let panelContainer = null;
  let buttonEl = null;

  function ensureGrabberHost() {
    let host = document.getElementById("fdownloader-video-host");
    if (!host) {
      host = document.createElement("div");
      host.id = "fdownloader-video-host";
      host.style.position = "absolute";
      host.style.top = "0";
      host.style.left = "0";
      host.style.width = "0";
      host.style.height = "0";
      host.style.zIndex = "2147483647";
      host.style.pointerEvents = "none";
      document.documentElement.appendChild(host);

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
          transform: translateY(-4px);
        }
        .fdown-wrap.visible {
          display: flex;
          opacity: 1;
          transform: translateY(0);
        }
        .fdown-btn {
          display: inline-flex;
          align-items: center;
          gap: 6px;
          background: rgba(15, 23, 42, 0.90);
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
          width: 17px;
          height: 17px;
          border-radius: 4px;
          object-fit: contain;
          vertical-align: middle;
          box-shadow: 0 1px 4px rgba(0, 0, 0, 0.35);
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

      let iconUrl = "";
      try {
        if (chrome && chrome.runtime && chrome.runtime.getURL) {
          iconUrl = chrome.runtime.getURL("icons/icon-48.png");
        }
      } catch (e) { }

      const iconHtml = iconUrl
        ? `<img class="fdown-logo" src="${iconUrl}" width="17" height="17" alt="FDownloader" />`
        : `<span class="fdown-badge">F</span>`;

      buttonEl = document.createElement("button");
      buttonEl.className = "fdown-btn";
      buttonEl.innerHTML = `${iconHtml}<span class="fdown-label">Bu Videoyu İndir</span>`;

      buttonEl.addEventListener("click", onGrabberButtonClick);
      panelContainer.addEventListener("mouseenter", () => {
        if (panelHideTimer) clearTimeout(panelHideTimer);
      });
      panelContainer.addEventListener("mouseleave", () => {
        scheduleHideGrabber();
      });

      panelContainer.appendChild(buttonEl);
      shadowRoot.appendChild(panelContainer);
    }
  }

  function resolveVideoTarget(video) {
    const isYouTube = window.location.hostname.includes("youtube.com") || window.location.hostname.includes("youtu.be");
    if (isYouTube) {
      return {
        url: window.location.href,
        filename: document.title ? document.title.replace(/\s*-\s*YouTube$/i, "").trim() + ".mp4" : "YouTube Video.mp4"
      };
    }

    // Doğrudan video kaynağı
    const src = video.currentSrc || video.src || "";
    if (src && !src.startsWith("blob:") && !src.startsWith("data:")) {
      let candidateName = "";
      try {
        const u = new URL(src);
        candidateName = u.pathname.split("/").pop() || "";
      } catch (e) { }

      return {
        url: src,
        filename: candidateName || (document.title ? document.title.trim() + ".mp4" : "Video.mp4")
      };
    }

    // Eğer blob veya MSE ise sayfa URL'i streaming indiriciye (yt-dlp) paslanır
    return {
      url: window.location.href,
      filename: document.title ? document.title.trim() + ".mp4" : "Video.mp4"
    };
  }

  function onGrabberButtonClick(e) {
    e.preventDefault();
    e.stopPropagation();

    if (!currentActiveVideo) return;

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
    }, (res) => {
      setTimeout(() => {
        if (label) label.textContent = originalText;
        hideGrabberImmediately();
      }, 900);
    });
  }

  function updateGrabberPosition(video) {
    if (!video || !panelContainer) return;
    const rect = video.getBoundingClientRect();

    // Çok küçük videolar (ikon, thumbnail vb.) için gösterme
    if (rect.width < 140 || rect.height < 90) {
      hideGrabberImmediately();
      return;
    }

    // Ekran dışındaysa gizle
    if (rect.bottom < 0 || rect.top > window.innerHeight || rect.right < 0 || rect.left > window.innerWidth) {
      hideGrabberImmediately();
      return;
    }

    const top = Math.max(10, rect.top + 10);
    const right = Math.max(10, window.innerWidth - rect.right + 10);

    panelContainer.style.top = `${top}px`;
    panelContainer.style.right = `${right}px`;
    panelContainer.style.left = "auto";
    panelContainer.classList.add("visible");
  }

  function showGrabberForVideo(video) {
    ensureGrabberHost();
    if (panelHideTimer) clearTimeout(panelHideTimer);
    currentActiveVideo = video;
    updateGrabberPosition(video);
  }

  function scheduleHideGrabber() {
    if (panelHideTimer) clearTimeout(panelHideTimer);
    panelHideTimer = setTimeout(() => {
      hideGrabberImmediately();
    }, 1200);
  }

  function hideGrabberImmediately() {
    if (panelContainer) {
      panelContainer.classList.remove("visible");
    }
  }

  function attachVideoListeners(video) {
    if (video._fdownloader_attached) return;
    video._fdownloader_attached = true;

    video.addEventListener("mouseenter", () => showGrabberForVideo(video));
    video.addEventListener("play", () => showGrabberForVideo(video));
    video.addEventListener("playing", () => showGrabberForVideo(video));
    video.addEventListener("mouseleave", () => scheduleHideGrabber());
  }

  function scanForVideos() {
    const videos = document.querySelectorAll("video");
    videos.forEach(attachVideoListeners);
  }

  // İlk yüklemede ve sayfa değişimlerinde videoları bağla
  scanForVideos();

  const observer = new MutationObserver(() => {
    scanForVideos();
  });
  observer.observe(document.documentElement, { childList: true, subtree: true });

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

