const BRIDGE_URL = "http://127.0.0.1:9666";

document.addEventListener("DOMContentLoaded", async () => {
  const statusCard = document.getElementById("statusCard");
  const statusDot = document.getElementById("statusDot");
  const statusText = document.getElementById("statusText");
  const extensionEnabledToggle = document.getElementById("extensionEnabledToggle");
  const masterToggleLabel = document.getElementById("masterToggleLabel");
  const settingsContainer = document.getElementById("settingsContainer");
  const interceptToggle = document.getElementById("interceptToggle");
  const bypassAltToggle = document.getElementById("bypassAltToggle");

  const grabberPosSelect = document.getElementById("grabberPosSelect");
  const hoverOnlyToggle = document.getElementById("hoverOnlyToggle");
  const hoverOpacityRange = document.getElementById("hoverOpacityRange");
  const hoverOpacityVal = document.getElementById("hoverOpacityVal");
  const idleOpacityBox = document.getElementById("idleOpacityBox");
  const idleOpacityRange = document.getElementById("idleOpacityRange");
  const idleOpacityVal = document.getElementById("idleOpacityVal");

  function applyMasterToggleUI(enabled) {
    if (masterToggleLabel) {
      masterToggleLabel.textContent = enabled ? "Eklenti Aktif" : "Eklenti Pasif (Devre Dışı)";
      masterToggleLabel.style.color = enabled ? "#38bdf8" : "#94a3b8";
    }
    if (settingsContainer) {
      settingsContainer.style.opacity = enabled ? "1" : "0.4";
      settingsContainer.style.pointerEvents = enabled ? "auto" : "none";
    }
  }

  // Ayarları oku
  chrome.storage.local.get({
    extensionEnabled: true,
    isEnabled: true,
    bypassOnAltKey: true,
    grabberPosition: "outside-top-right",
    grabberHoverOnly: true,
    grabberHoverOpacity: 100,
    grabberIdleOpacity: 0
  }, (data) => {
    const extActive = data.extensionEnabled !== false;
    if (extensionEnabledToggle) {
      extensionEnabledToggle.checked = extActive;
    }
    applyMasterToggleUI(extActive);
    interceptToggle.checked = data.isEnabled !== false;
    bypassAltToggle.checked = data.bypassOnAltKey !== false;

    if (grabberPosSelect) grabberPosSelect.value = data.grabberPosition || "outside-top-right";
    if (hoverOnlyToggle) {
      hoverOnlyToggle.checked = data.grabberHoverOnly !== false;
      if (idleOpacityBox) idleOpacityBox.style.display = hoverOnlyToggle.checked ? "none" : "block";
    }
    if (hoverOpacityRange) {
      hoverOpacityRange.value = data.grabberHoverOpacity ?? 100;
      if (hoverOpacityVal) hoverOpacityVal.textContent = `${hoverOpacityRange.value}%`;
    }
    if (idleOpacityRange) {
      idleOpacityRange.value = data.grabberIdleOpacity ?? 0;
      if (idleOpacityVal) idleOpacityVal.textContent = `${idleOpacityRange.value}%`;
    }
  });

  if (extensionEnabledToggle) {
    extensionEnabledToggle.addEventListener("change", () => {
      const enabled = extensionEnabledToggle.checked;
      applyMasterToggleUI(enabled);
      chrome.storage.local.set({ extensionEnabled: enabled });
    });
  }

  interceptToggle.addEventListener("change", () => {
    chrome.storage.local.set({ isEnabled: interceptToggle.checked });
  });

  bypassAltToggle.addEventListener("change", () => {
    chrome.storage.local.set({ bypassOnAltKey: bypassAltToggle.checked });
  });

  if (grabberPosSelect) {
    grabberPosSelect.addEventListener("change", () => {
      chrome.storage.local.set({ grabberPosition: grabberPosSelect.value });
    });
  }

  if (hoverOnlyToggle) {
    hoverOnlyToggle.addEventListener("change", () => {
      chrome.storage.local.set({ grabberHoverOnly: hoverOnlyToggle.checked });
      if (idleOpacityBox) idleOpacityBox.style.display = hoverOnlyToggle.checked ? "none" : "block";
    });
  }

  if (hoverOpacityRange) {
    hoverOpacityRange.addEventListener("input", () => {
      if (hoverOpacityVal) hoverOpacityVal.textContent = `${hoverOpacityRange.value}%`;
    });
    hoverOpacityRange.addEventListener("change", () => {
      chrome.storage.local.set({ grabberHoverOpacity: parseInt(hoverOpacityRange.value, 10) });
    });
  }

  if (idleOpacityRange) {
    idleOpacityRange.addEventListener("input", () => {
      if (idleOpacityVal) idleOpacityVal.textContent = `${idleOpacityRange.value}%`;
    });
    idleOpacityRange.addEventListener("change", () => {
      chrome.storage.local.set({ grabberIdleOpacity: parseInt(idleOpacityRange.value, 10) });
    });
  }

  // FDownloader bağlantı testi ve sürüm bildirimi
  try {
    const manifest = chrome.runtime.getManifest();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 2500);
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

    if (res.ok) {
      statusCard.className = "status-badge status-online";
      statusDot.className = "dot dot-online";
      statusText.textContent = `Bağlandı (FDownloader Aktif - v${manifest.version || "1.0.0"})`;
    } else {
      throw new Error();
    }
  } catch (e) {
    statusCard.className = "status-badge status-offline";
    statusDot.className = "dot dot-offline";
    statusText.textContent = "FDownloader Kapalı (Çalıştırın)";
  }
});
