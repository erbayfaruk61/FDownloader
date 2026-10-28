using System.Net;
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
}

/// <summary>
/// Tarayıcı eklentisi (Chrome, Edge, Firefox, Brave) ile haberleşen yerel HTTP köprüsü (port 9666).
/// </summary>
public sealed class BrowserBridgeService : IDisposable
{
    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<BrowserBridgeService> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    public const int DefaultPort = 9666;

    public bool IsRunning => _listener?.IsListening ?? false;

    public BrowserBridgeService(DownloadEventBus eventBus, ILogger<BrowserBridgeService> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
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

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS Başlıkları (Tarayıcı uzantısının localhost'a fetch yapabilmesi için)
        response.AppendHeader("Access-Control-Allow-Origin", "*");
        response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

        try
        {
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
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

                var pingJson = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    app = "FDownloader",
                    extensionVersion = version,
                    timestamp = DateTime.UtcNow
                });
                await WriteJsonResponseAsync(response, 200, pingJson);
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

                var healthJson = JsonSerializer.Serialize(new
                {
                    status = "ok",
                    app = "FDownloader",
                    version = "1.0.0",
                    bridge = "active"
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

                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();

                var downloadReq = JsonSerializer.Deserialize<BrowserDownloadRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (downloadReq != null && !string.IsNullOrWhiteSpace(downloadReq.Url))
                {
                    _logger.LogInformation("Tarayıcıdan indirme yakalandı: {Url} (Dosya: {FileName})", downloadReq.Url, downloadReq.FileName);

                    _eventBus.Publish(new BrowserDownloadCapturedEvent(
                        downloadReq.Url.Trim(),
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
