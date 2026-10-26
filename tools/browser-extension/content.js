// ⚡ FDownloader - Content Script (Alt Tuşu İzleyici)
(function () {
  const BYPASS_DURATION_MS = 5000; // 5 saniye

  function updateAltState(isDown) {
    try {
      if (!chrome.runtime || !chrome.runtime.id) return;

      const now = Date.now();
      const payload = {
        isAltDown: Boolean(isDown),
        altPressedUntil: now + BYPASS_DURATION_MS
      };

      // 1. Doğrudan storage.local'a yaz (Service Worker uyuyor olsa bile anında kaydedilir)
      chrome.storage.local.set(payload, () => {
        if (chrome.runtime.lastError) {
          // Sessizce geç
        }
      });

      // 2. Yedek olarak background'a anlık mesaj ilet
      chrome.runtime.sendMessage({
        type: "ALT_EVENT",
        isDown: Boolean(isDown),
        altPressedUntil: now + BYPASS_DURATION_MS
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

  // 4. Pencere odağı değişirse
  window.addEventListener("blur", () => {
    updateAltState(false);
  }, true);
})();
