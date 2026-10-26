using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Utilities;

/// <summary>
/// Sistem panosunu (clipboard) izleyerek yeni kopyalanan indirme ve medya linklerini yakalar (ClipGrab / IDM tarzı).
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
            _logger.LogInformation("Doğrudan indirme linki yakalandı: {Url}", text);
            _eventBus.Publish(new ClipboardUrlDetectedEvent(text, ClipboardUrlType.DirectFile));
        }
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

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;

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
