# FDownloader - Next Generation Download Manager

<p align="center">
  <img src="src/FDownloader.App/Assets/logo.png" alt="FDownloader Logo" width="128" height="128" />
</p>

<p align="center">
  <strong>High-performance, modern, multi-threaded, and intelligent desktop download manager.</strong>
</p>

<p align="center">
  <a href="https://github.com/erbayfaruk61/FDownloader/releases/latest">
    <img src="https://img.shields.io/github/v/release/erbayfaruk61/FDownloader?style=for-the-badge&logo=github&color=10B981" alt="Latest Release" />
  </a>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 9" />
  <img src="https://img.shields.io/badge/UI-Avalonia_UI-8A2BE2?style=for-the-badge" alt="Avalonia UI" />
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows" alt="Windows" />
  <img src="https://img.shields.io/badge/License-MIT-blue?style=for-the-badge" alt="MIT License" />
</p>

<p align="center">
  <a href="https://github.com/erbayfaruk61/FDownloader/releases/latest/download/FDownloader-Setup.exe">
    <img src="https://img.shields.io/badge/Download_Now-FDownloader--Setup.exe-success?style=for-the-badge&logo=windows&logoColor=white" height="38" alt="Download Windows Installer" />
  </a>
  &nbsp;
  <a href="https://github.com/erbayfaruk61/FDownloader/releases/latest/download/FDownloader-Browser-Extension.zip">
    <img src="https://img.shields.io/badge/Browser_Extension-.ZIP-0284C7?style=for-the-badge&logo=googlechrome&logoColor=white" height="38" alt="Download Browser Extension" />
  </a>
</p>

---

## 🚀 Key Features (v1.5.0)

* ⚡ **High-Speed Multi-Part Downloading:** Segments HTTP/HTTPS connections dynamically to fully utilize available bandwidth; effortlessly resumes interrupted transfers.
* 🧩 **Smart Media Sniffer (Floating Grabber):** Automatically detects video and audio streams on web pages and displays an overlay grabber button (compatible with platforms supported by yt-dlp).
* 🖥️ **System Tray & Windows Autostart:** Seamlessly minimizes to the taskbar notification area when closed or minimized; ready to auto-start with Windows.
* 🔒 **Auto Archive Extractor & Password Support:** Automatically uncompresses ZIP, RAR, 7Z, and TAR archives upon download completion. Supports password-protected archives via embedded 7-Zip engine.
* 🛡️ **Network Adapter Binding (VPN Kill-Switch):** Locks downloads strictly to a specified physical or VPN network adapter; immediately pauses transfers if the VPN drops.
* 🕸️ **Site & Gallery Grabber:** Discovers and queues all media, image, audio, or document links from any webpage with a single click.
* 🌊 **BitTorrent Integration:** Full support for Magnet links and `.torrent` files with preview file selection, sequential downloading (streaming mode), and peer tracking.
* 🔔 **Interactive Toast Notifications:** Contextual notifications in the bottom-right corner offering immediate "Open File" and "Open Folder" actions.
* 🔄 **Seamless Auto-Updates:** Powered by Velopack to silently check for GitHub Releases and perform single-click updates.
* 🎨 **Rich Customizable Themes:** Ocean Blue, Cyber Purple, Platinum Gray, Light Clean, and High Contrast accessibility themes powered by Avalonia UI.

---

## 📥 Quick Installation

1. Download the [Latest Release Installer (FDownloader-Setup.exe)](https://github.com/erbayfaruk61/FDownloader/releases/latest/download/FDownloader-Setup.exe).
2. Run the installer.
3. Desktop and Start Menu shortcuts are created automatically.

---

## 🧩 Browser Extension Setup

To capture downloads and video streams automatically from Chrome, Edge, Brave, Opera, or Firefox:

1. Download `FDownloader-Browser-Extension.zip` from [Releases](https://github.com/erbayfaruk61/FDownloader/releases/latest) and extract it to a folder *(or click "Open Extension Folder" in FDownloader **Settings > Browser**)*.
2. In your Chromium-based browser, navigate to:
   - **Chrome / Brave / Opera:** `chrome://extensions`
   - **Microsoft Edge:** `edge://extensions`
3. Toggle on **Developer Mode** in the top right.
4. Click **Load unpacked** in the top left and select the extracted extension folder.

---

## 🛠️ Build from Source

```powershell
# Clone the repository
git clone https://github.com/erbayfaruk61/FDownloader.git
cd FDownloader

# Build and run
dotnet run --project src/FDownloader.App/FDownloader.App.csproj
```

---

## ⚖️ Legal Disclaimer

FDownloader is a general-purpose utility designed for legitimate personal content management and downloading. Users bear sole responsibility for ensuring compliance with copyright laws, intellectual property rights, and terms of service of third-party platforms. FDownloader does not circumvent DRM, bypass paywalls, or alter copy-protection systems.

---

## 📜 Credits & Acknowledgments

FDownloader is built on top of remarkable open-source projects:

* [Avalonia UI](https://avaloniaui.net/) - Cross-platform XAML GUI framework.
* [MonoTorrent](https://github.com/alanmcgovern/monotorrent) - BitTorrent client engine.
* [yt-dlp](https://github.com/yt-dlp/yt-dlp) - Media extraction engine.
* [SharpCompress](https://github.com/adamhathcock/sharpcompress) & [7-Zip](https://www.7-zip.org/) - Archive processing.
* [Velopack](https://velopack.io) - Modern desktop installation and update framework.
* Detailed notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) | Privacy policy: [docs/GIZLILIK.md](docs/GIZLILIK.md)

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

