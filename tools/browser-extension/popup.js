const BRIDGE_URL = "http://127.0.0.1:9666";

document.addEventListener("DOMContentLoaded", async () => {
  const statusCard = document.getElementById("statusCard");
  const statusDot = document.getElementById("statusDot");
  const statusText = document.getElementById("statusText");
  const interceptToggle = document.getElementById("interceptToggle");
  const bypassAltToggle = document.getElementById("bypassAltToggle");

  // Ayarları oku
  chrome.storage.local.get({ isEnabled: true, bypassOnAltKey: true }, (data) => {
    interceptToggle.checked = data.isEnabled !== false;
    bypassAltToggle.checked = data.bypassOnAltKey !== false;
  });

  interceptToggle.addEventListener("change", () => {
    chrome.storage.local.set({ isEnabled: interceptToggle.checked });
  });

  bypassAltToggle.addEventListener("change", () => {
    chrome.storage.local.set({ bypassOnAltKey: bypassAltToggle.checked });
  });

  // FDownloader bağlantı testi
  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 2500);
    const res = await fetch(`${BRIDGE_URL}/health`, { signal: controller.signal });
    clearTimeout(timer);

    if (res.ok) {
      statusCard.className = "status-badge status-online";
      statusDot.className = "dot dot-online";
      statusText.textContent = "Bağlandı (FDownloader Aktif)";
    } else {
      throw new Error();
    }
  } catch (e) {
    statusCard.className = "status-badge status-offline";
    statusDot.className = "dot dot-offline";
    statusText.textContent = "FDownloader Kapalı (Çalıştırın)";
  }
});
