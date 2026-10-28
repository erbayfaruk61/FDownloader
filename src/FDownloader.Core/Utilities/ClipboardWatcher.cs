using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Utilities;

/// <summary>
/// Sistem panosunu (clipboard) izleyerek kopyalanan indirme ve medya bağlantılarını otomatik yakalar.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<ClipboardWatcher> _logger;
    private readonly CancellationTokenSource _cts = new();
    private string? _lastProcessedText;
    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            _logger.LogInformation("Pano izleyici durumu: {Status}", value ? "Aktif" : "Pasif");
        }
    }

    private static readonly Regex MediaUrlRegex = new(
        @"(https?://)?(www\.)?(youtube\.com|youtu\.be|tiktok\.com|twitter\.com|x\.com|instagram\.com|vimeo\.com|dailymotion\.com|facebook\.com|twitch\.tv)/[^\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> DownloadableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Arşiv & Disk İmajları
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".img", ".dmg", ".cab", ".bin", ".cue",
        ".001", ".002", ".003", ".zst", ".tgz", ".vhd", ".vhdx", ".vmdk",
        // Kurulum & Yürütülebilir Dosyalar
        ".exe", ".msi", ".apk", ".xapk", ".deb", ".rpm", ".pkg", ".appimage", ".appx", ".msix", ".jar",
        // Video & Ses
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts",
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".wma",
        // Belgeler
        ".pdf", ".epub", ".mobi", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv",
        // Torrent
        ".torrent"
    };

    public ClipboardWatcher(DownloadEventBus eventBus, ILogger<ClipboardWatcher> logger)
    {
        _eventBus = eventBus;
        _logger = logger;

        // Arka planda periyodik kontrol döngüsü
        Task.Run(() => MonitorLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Manuel olarak bir metni analiz edip olay tetikler.
    /// </summary>
    public void ProcessText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();

        if (text == _lastProcessedText) return;
        _lastProcessedText = text;

        if (text.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Magnet linki yakalandı: {Url}", text);
            _eventBus.Publish(new ClipboardUrlDetectedEvent(text, ClipboardUrlType.TorrentMagnet));
        }
        else if (text.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Torrent dosyası yakalandı: {Url}", text);
            _eventBus.Publish(new ClipboardUrlDetectedEvent(text, ClipboardUrlType.TorrentFile));
        }
        else if (MediaUrlRegex.IsMatch(text))
        {
            _logger.LogInformation("Medya/Video linki yakalandı: {Url}", text);
            _eventBus.Publish(new ClipboardUrlDetectedEvent(text, ClipboardUrlType.MediaVideo));
        }
        else if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                 (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (IsDownloadableFileUrl(uri))
            {
                _logger.LogInformation("Doğrudan indirme linki yakalandı: {Url}", text);
                _eventBus.Publish(new ClipboardUrlDetectedEvent(text, ClipboardUrlType.DirectFile));
            }
        }
    }

    private static bool IsDownloadableFileUrl(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path) || path == "/" || path.EndsWith("/"))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(ext))
        {
            if (DownloadableExtensions.Contains(ext))
            {
                return true;
            }

            // Web sayfası uzantıları kesinlikle indirme değildir
            if (ext is ".html" or ".htm" or ".php" or ".asp" or ".aspx" or ".jsp" or ".css" or ".js")
            {
                return false;
            }
        }

        // URL query parametrelerinde belirgin bir indirme uzantısı var mı? (?file=setup.exe vb.)
        if (!string.IsNullOrWhiteSpace(uri.Query))
        {
            var query = uri.Query;
            foreach (var validExt in DownloadableExtensions)
            {
                if (query.Contains(validExt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(800, cancellationToken);

                if (!IsEnabled) continue;

                // Windows panosundan metin oku (P/Invoke ile hafif ve UI thread'i kilitlemeden)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var text = GetWindowsClipboardText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        ProcessText(text);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Pano izleme periyodunda hata");
            }
        }
    }

    // ─── Windows P/Invoke Pano Okuyucu ──────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    /// <summary>
    /// Metni Windows panosuna yazar.
    /// </summary>
    public static bool SetWindowsClipboardText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (!OpenClipboard(IntPtr.Zero)) return false;

        try
        {
            EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero) return false;

            IntPtr target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero) return false;

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            SetClipboardData(CF_UNICODETEXT, hGlobal);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static string? GetWindowsClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;

        try
        {
            IntPtr hGlobal = GetClipboardData(CF_UNICODETEXT);
            if (hGlobal == IntPtr.Zero) return null;

            IntPtr pText = GlobalLock(hGlobal);
            if (pText == IntPtr.Zero) return null;

            try
            {
                return Marshal.PtrToStringUni(pText);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
