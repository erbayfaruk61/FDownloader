using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Network;

public class BrowserDownloadRequest
{
    public string Url { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? Referrer { get; set; }
    public string? Cookies { get; set; }
    public string? UserAgent { get; set; }
    public long? TotalBytes { get; set; }
    public bool IsAltDown { get; set; }
}

/// <summary>
/// Tarayıcı eklentisi (Chrome, Edge, Firefox, Brave) ile haberleşen yerel HTTP köprüsü (port 9666).
/// Güvenlik: yalnızca 127.0.0.1'e bağlanır; her oturumda benzersiz gizli token üretilir;
/// Origin ve Host başlıkları doğrulanır (DNS rebinding ve CSRF koruması).
/// </summary>
public sealed class BrowserBridgeService : IDisposable
{
    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<BrowserBridgeService> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public const int DefaultPort = 9666;

    private static volatile bool _reloadRequested = false;

    /// <summary>
    /// Tarayıcı eklentilerine bir sonraki kalp atışında kendini yeniden yükleme (reload) sinyali gönderir.
    /// </summary>
    public static void RequestExtensionReload()
    {
        _reloadRequested = true;
    }

    // ─── Güvenlik Sabitleri ───────────────────────────────────────────────
    private static readonly string[] AllowedOriginPrefixes =
    [
        "chrome-extension://",
        "moz-extension://",
        "extension://"
    ];

    private static bool IsAllowedOrigin(string origin)
    {
        if (string.IsNullOrEmpty(origin) || origin.Equals("null", StringComparison.OrdinalIgnoreCase))
            return true; // yerel araçlar (curl, Postman) veya null origin

        foreach (var prefix in AllowedOriginPrefixes)
        {
            if (origin.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // Yalnızca bu Host değerleri kabul edilir (DNS rebinding koruması)
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        $"127.0.0.1:{DefaultPort}",
        $"localhost:{DefaultPort}"
    };

    // Uygulama başlangıcında üretilen, oturum süresi boyunca geçerli gizli token
    private readonly string _sessionToken;

    public bool IsRunning => _listener?.IsListening ?? false;

    // Token'ı eklenti ile paylaşmak için dışarıya sun (BrowserBridgeInfoEvent üzerinden)
    public string SessionToken => _sessionToken;

    public BrowserBridgeService(DownloadEventBus eventBus, ILogger<BrowserBridgeService> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
        _sessionToken = GenerateSessionToken();
    }

    private static string GenerateSessionToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{DefaultPort}/");

        try
        {
            _listener.Prefixes.Add($"http://localhost:{DefaultPort}/");
        }
        catch { }

        try
        {
            _listener.Start();
            _logger.LogInformation("FDownloader Tarayıcı Köprüsü aktif: http://127.0.0.1:{Port}/", DefaultPort);
            Task.Run(() => ListenLoopAsync(_cts.Token));
        }
        catch (HttpListenerException hex)
        {
            _logger.LogWarning(hex, "FDownloader Tarayıcı Köprüsü localhost ile başlatılamadı, salt 127.0.0.1 deneniyor...");
            try
            {
                _listener.Close();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{DefaultPort}/");
                _listener.Start();
                _logger.LogInformation("FDownloader Tarayıcı Köprüsü aktif (127.0.0.1): http://127.0.0.1:{Port}/", DefaultPort);
                Task.Run(() => ListenLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FDownloader Tarayıcı Köprüsü başlatılamadı (Port: {Port})", DefaultPort);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FDownloader Tarayıcı Köprüsü başlatılamadı (Port: {Port})", DefaultPort);
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context));
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Tarayıcı köprüsü istek dinleme hatası");
            }
        }
    }

    /// <summary>
    /// Origin başlığı: yalnızca kendi eklentimizin chrome-extension:// adresinden veya
    /// null (curl/Postman gibi yerel araçlardan) gelen isteklere izin ver.
    /// Host başlığı: yalnızca 127.0.0.1:9666 veya localhost:9666 kabul et (DNS rebinding koruması).
    /// </summary>
    private bool IsRequestAllowed(HttpListenerRequest request)
    {
        // Host doğrulaması — DNS rebinding saldırısını önler
        var host = request.Headers["Host"] ?? string.Empty;
        if (!string.IsNullOrEmpty(host) && !AllowedHosts.Contains(host))
        {
            _logger.LogWarning("Köprü: geçersiz Host başlığı reddedildi: {Host}", host);
            return false;
        }

        // Origin doğrulaması — yabancı web sitelerinin fetch() ile istek atmasını engeller
        var origin = request.Headers["Origin"] ?? string.Empty;
        if (!IsAllowedOrigin(origin))
        {
            _logger.LogWarning("Köprü: yetkisiz Origin reddedildi: {Origin}", origin);
            return false;
        }

        return true;
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS: yalnızca izin verilen eklenti origin'lerine izin ver
        var requestOrigin = request.Headers["Origin"] ?? string.Empty;
        var allowedOrigin = (!string.IsNullOrEmpty(requestOrigin) && IsAllowedOrigin(requestOrigin))
            ? requestOrigin
            : "null";

        response.AppendHeader("Access-Control-Allow-Origin", allowedOrigin);
        response.AppendHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, X-Extension-Version, X-Bridge-Token");
        response.AppendHeader("Access-Control-Max-Age", "600");
        response.AppendHeader("Vary", "Origin");

        try
        {
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            // ─── Güvenlik: Origin + Host doğrulaması ───────────────────────
            if (!IsRequestAllowed(request))
            {
                response.StatusCode = 403;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath?.ToLowerInvariant() ?? "";

            if (path == "/show" || path == "/bring-to-front")
            {
                _eventBus.Publish(new ShowMainWindowEvent());
                var showJson = JsonSerializer.Serialize(new { status = "ok", message = "window brought to front" });
                await WriteJsonResponseAsync(response, 200, showJson);
                return;
            }

            if (path == "/ping")
            {
                var version = "1.0.0";
                var extHeader = request.Headers["X-Extension-Version"];
                if (!string.IsNullOrWhiteSpace(extHeader))
                {
                    version = extHeader.Trim();
                }

                if (request.HttpMethod == "POST")
                {
                    try
                    {
                        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                        var body = await reader.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            using var doc = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("version", out var vProp))
                            {
                                var vStr = vProp.GetString();
                                if (!string.IsNullOrWhiteSpace(vStr)) version = vStr.Trim();
                            }
                        }
                    }
                    catch { }
                }
                else if (!string.IsNullOrWhiteSpace(request.QueryString["v"]))
                {
                    version = request.QueryString["v"]!.Trim();
                }

                _eventBus.Publish(new BrowserExtensionHeartbeatEvent(version, DateTime.UtcNow));

                var shouldReload = _reloadRequested;
                if (shouldReload) _reloadRequested = false;

                var pingJson = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    app = "FDownloader",
                    extensionVersion = version,
                    reloadRequested = shouldReload,
                    timestamp = DateTime.UtcNow
                });
                await WriteJsonResponseAsync(response, 200, pingJson);
                return;
            }

            if (path == "/extension/reload")
            {
                RequestExtensionReload();
                var relJson = JsonSerializer.Serialize(new { status = "ok", message = "reload_signal_queued" });
                await WriteJsonResponseAsync(response, 200, relJson);
                return;
            }

            if (path == "/health" || path == "/status" || path == "/")
            {
                var extHeader = request.Headers["X-Extension-Version"];
                var queryV = request.QueryString["v"] ?? request.QueryString["version"];
                var detectedVersion = !string.IsNullOrWhiteSpace(extHeader)
                    ? extHeader.Trim()
                    : (!string.IsNullOrWhiteSpace(queryV) ? queryV.Trim() : null);

                if (!string.IsNullOrWhiteSpace(detectedVersion))
                {
                    _eventBus.Publish(new BrowserExtensionHeartbeatEvent(detectedVersion, DateTime.UtcNow));
                }

                var shouldReload = _reloadRequested;
                if (shouldReload) _reloadRequested = false;

                var healthJson = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    app = "FDownloader",
                    version = "1.0.0",
                    bridge = "active",
                    reloadRequested = shouldReload
                });
                await WriteJsonResponseAsync(response, 200, healthJson);
                return;
            }

            if (request.HttpMethod == "POST" && (path == "/download" || path == "/capture"))
            {
                var extHeader = request.Headers["X-Extension-Version"];
                _eventBus.Publish(new BrowserExtensionHeartbeatEvent(
                    !string.IsNullOrWhiteSpace(extHeader) ? extHeader.Trim() : "1.0.0",
                    DateTime.UtcNow));

                // Gövde boyutunu sınırla (max 64 KB): sonsuz veri saldırısını önler
                using var limitedStream = new LimitedStream(request.InputStream, maxBytes: 65_536);
                using var reader = new StreamReader(limitedStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();

                var downloadReq = JsonSerializer.Deserialize<BrowserDownloadRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (downloadReq != null && !string.IsNullOrWhiteSpace(downloadReq.Url))
                {
                    // ─── Alt Tuşu Baypası (Kullanıcı Alt tuşuna basılı tutarak tıkladıysa indirmeyi tarayıcıya bırak) ───
                    if (downloadReq.IsAltDown || IsAltBypassActive())
                    {
                        _logger.LogInformation("Alt tuşu basılı algılandı (OS seviyesinde). İndirme tarayıcıya devredildi: {Url}", downloadReq.Url);
                        var bypassJson = JsonSerializer.Serialize(new
                        {
                            success = false,
                            bypass = true,
                            message = "Alt tuşu baypası devrede, indirme tarayıcıya devredildi"
                        });
                        await WriteJsonResponseAsync(response, 200, bypassJson);
                        return;
                    }

                    // ─── URL şema doğrulaması (komut enjeksiyonu ve yerel dosya erişimini önler) ───
                    var rawUrl = downloadReq.Url.Trim();
                    if (!IsUrlSchemeAllowed(rawUrl))
                    {
                        _logger.LogWarning("Köprü: izin verilmeyen URL şeması reddedildi: {Url}", rawUrl[..Math.Min(rawUrl.Length, 100)]);
                        response.StatusCode = 400;
                        response.Close();
                        return;
                    }

                    // URL uzunluk sınırı
                    if (rawUrl.Length > 8192)
                    {
                        response.StatusCode = 400;
                        response.Close();
                        return;
                    }

                    _logger.LogInformation("Tarayıcıdan indirme yakalandı: {Url} (Dosya: {FileName})", rawUrl, downloadReq.FileName);

                    _eventBus.Publish(new BrowserDownloadCapturedEvent(
                        rawUrl,
                        downloadReq.FileName?.Trim(),
                        downloadReq.Referrer?.Trim(),
                        downloadReq.Cookies?.Trim(),
                        downloadReq.UserAgent?.Trim(),
                        downloadReq.TotalBytes
                    ));

                    var resultJson = JsonSerializer.Serialize(new
                    {
                        success = true,
                        message = "İndirme FDownloader tarafından başarıyla yakalandı"
                    });
                    await WriteJsonResponseAsync(response, 200, resultJson);
                    return;
                }
            }

            response.StatusCode = 404;
            response.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP köprü isteği işlenirken hata oluştu");
            try
            {
                var errJson = JsonSerializer.Serialize(new { success = false, error = ex.Message });
                await WriteJsonResponseAsync(response, 500, errJson);
            }
            catch { }
        }
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse response, int statusCode, string json)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    /// <summary>
    /// Yalnızca http://, https:// ve ftp:// şemalarına izin ver.
    /// file://, data://, javascript:// ve diğer şemalar reddedilir.
    /// </summary>
    private static bool IsUrlSchemeAllowed(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
    }

    // ─── Windows P/Invoke Alt Tuşu Tespiti (IDM Tarzı Global Baypas) ───────
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_MENU = 0x12; // Alt tuşu (VK_MENU)
    private static DateTime _lastAltPressedTime = DateTime.MinValue;
    private static readonly object _altLock = new();

        public static void NotifyAltKeyPressed()
    {
        lock (_altLock)
        {
            _lastAltPressedTime = DateTime.UtcNow;
        }
    }

    public static bool IsAltBypassActive()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Alt tuşu o anda fiziksel olarak basılı mı?
                if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
                {
                    lock (_altLock)
                    {
                        _lastAltPressedTime = DateTime.UtcNow;
                    }
                    return true;
                }
            }
            catch { }
        }

        lock (_altLock)
        {
            // Tıklamadan hemen sonra bırakma toleransı: 1.5 saniye (indirmelerin takılmasını önler)
            return (DateTime.UtcNow - _lastAltPressedTime).TotalSeconds < 5.0;
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

/// <summary>
/// Maksimum okunabilir bayt sınırlaması uygulayan Stream sarmalayıcı.
/// Sonsuz veri akışı (DoS) saldırısını önler.
/// </summary>
internal sealed class LimitedStream(Stream inner, long maxBytes) : Stream
{
    private long _remaining = maxBytes;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_remaining <= 0) return 0;
        var toRead = (int)Math.Min(count, _remaining);
        var n = inner.Read(buffer, offset, toRead);
        _remaining -= n;
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_remaining <= 0) return ValueTask.FromResult(0);
        var toRead = (int)Math.Min(buffer.Length, _remaining);
        var subBuffer = buffer[..toRead];
        var readValueTask = inner.ReadAsync(subBuffer, cancellationToken);
        if (readValueTask.IsCompletedSuccessfully)
        {
            var n = readValueTask.Result;
            _remaining -= n;
            return ValueTask.FromResult(n);
        }

        return AwaitReadAsync(readValueTask);
    }

    private async ValueTask<int> AwaitReadAsync(ValueTask<int> task)
    {
        var n = await task;
        _remaining -= n;
        return n;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
