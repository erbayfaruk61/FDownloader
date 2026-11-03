using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace FDownloader.Core.Tests.MockServer;

/// <summary>
/// P1-03: Motor testleri için 13 uç noktalı yerel test HTTP sunucusu.
/// Deterministik veri üretir ve hash karşılaştırmasını destekler.
/// </summary>
public sealed class TestHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;
    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}/";

    public static readonly byte[] NormalContent = Encoding.UTF8.GetBytes("FDownloader Test Suite Normal Content - Deterministic 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ");
    public static readonly string NormalContentSha256 = Convert.ToHexString(SHA256.HashData(NormalContent)).ToLowerInvariant();

    public static readonly byte[] LargeChunkPattern = Encoding.ASCII.GetBytes("0123456789ABCDEF"); // 16 bytes pattern

    public string DynamicEtag { get; set; } = "\"v1\"";
    public string DynamicContent { get; set; } = "Content Version 1";
    public int AbortRemainingCount { get; set; } = int.MaxValue;

    public TestHttpServer(int port = 0)
    {
        if (port == 0)
        {
            var tcpListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
        }

        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseUrl);
    }

    public void Start()
    {
        _listener.Start();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleContextAsync(context));
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;
        var path = req.Url?.AbsolutePath ?? "/";

        try
        {
            switch (path)
            {
                // 1. Normal dosya (Range destekli, 206)
                case "/normal":
                    await HandleNormalAsync(req, res);
                    break;

                // 2. Range desteksiz dosya (Accept-Ranges: none, Range isteğini yok sayar ve 200 döner)
                case "/no-range":
                    res.Headers["Accept-Ranges"] = "none";
                    res.StatusCode = 200;
                    res.ContentLength64 = NormalContent.Length;
                    await res.OutputStream.WriteAsync(NormalContent);
                    break;

                // 3. Boyutu bilinmeyen akış (chunked)
                case "/chunked":
                    res.SendChunked = true;
                    res.StatusCode = 200;
                    for (int i = 0; i < 5; i++)
                    {
                        var chunk = Encoding.UTF8.GetBytes($"chunk_{i}_data;");
                        await res.OutputStream.WriteAsync(chunk);
                        await res.OutputStream.FlushAsync();
                    }
                    break;

                // 4. Yavaş yanıt (bayt başına / parça başına gecikmeli)
                case "/slow":
                    res.StatusCode = 200;
                    res.ContentLength64 = NormalContent.Length;
                    int chunkSize = 10;
                    for (int i = 0; i < NormalContent.Length; i += chunkSize)
                    {
                        int len = Math.Min(chunkSize, NormalContent.Length - i);
                        await res.OutputStream.WriteAsync(NormalContent, i, len);
                        await res.OutputStream.FlushAsync();
                        await Task.Delay(20);
                    }
                    break;

                // 5. Yarıda kopan bağlantı (belirli bayttan sonra bağlantıyı zorla kes)
                case "/abort":
                    if (AbortRemainingCount > 0)
                    {
                        AbortRemainingCount--;
                        res.StatusCode = 200;
                        res.ContentLength64 = NormalContent.Length;
                        await res.OutputStream.WriteAsync(NormalContent, 0, 15);
                        await res.OutputStream.FlushAsync();
                        res.Abort(); // Bağlantıyı zorla kapatır
                        return;
                    }
                    // Abort hakkı bittiğinde normal Range destekli yanıt ver (kaldığı yerden devam etsin)
                    await HandleNormalAsync(req, res);
                    break;

                // 6. Yanlış Content-Length (Content-Length içerikten daha büyük/kısa)
                case "/wrong-length":
                    res.StatusCode = 200;
                    res.ContentLength64 = NormalContent.Length + 500; // Yanıltıcı uzunluk
                    await res.OutputStream.WriteAsync(NormalContent);
                    break;

                // 7. Yönlendirme zinciri ve yönlendirme döngüsü
                case "/redirect-1":
                    res.StatusCode = 302;
                    res.RedirectLocation = $"{BaseUrl}redirect-2";
                    break;
                case "/redirect-2":
                    res.StatusCode = 302;
                    res.RedirectLocation = $"{BaseUrl}normal";
                    break;
                case "/redirect-loop":
                    res.StatusCode = 302;
                    res.RedirectLocation = $"{BaseUrl}redirect-loop";
                    break;

                // 8. Hata durumları (500, 503, 429 Retry-After, 403, 404)
                case "/error-500":
                    res.StatusCode = 500;
                    break;
                case "/error-503":
                    res.StatusCode = 503;
                    break;
                case "/error-429":
                    res.StatusCode = 429;
                    res.Headers["Retry-After"] = "5";
                    break;
                case "/error-403":
                    res.StatusCode = 403;
                    break;
                case "/error-404":
                    res.StatusCode = 404;
                    break;

                // 9. Content-Disposition biçimleri
                case "/disposition-simple":
                    res.StatusCode = 200;
                    res.Headers["Content-Disposition"] = "attachment; filename=\"setup_simple.exe\"";
                    await res.OutputStream.WriteAsync(NormalContent);
                    break;
                case "/disposition-utf8":
                    res.StatusCode = 200;
                    res.Headers["Content-Disposition"] = "attachment; filename*=UTF-8''t%C3%BCrk%C3%A7e_%C5%9Fark%C4%B1.mp3";
                    await res.OutputStream.WriteAsync(NormalContent);
                    break;
                case "/disposition-malicious":
                    res.StatusCode = 200;
                    res.Headers["Content-Disposition"] = "attachment; filename=\"../../../../evil.exe\"";
                    await res.OutputStream.WriteAsync(NormalContent);
                    break;

                // 10. Kimlik doğrulama isteyen uç nokta (Basic)
                case "/auth-basic":
                    var auth = req.Headers["Authorization"];
                    if (auth == "Basic YWRtaW46c2VjcmV0") // admin:secret
                    {
                        res.StatusCode = 200;
                        await res.OutputStream.WriteAsync(NormalContent);
                    }
                    else
                    {
                        res.StatusCode = 401;
                        res.Headers["WWW-Authenticate"] = "Basic realm=\"TestRealm\"";
                    }
                    break;

                // 11. Çerez veya Referer zorunlu uç nokta
                case "/require-cookie-referer":
                    var cookie = req.Headers["Cookie"];
                    var referer = req.Headers["Referer"];
                    if (cookie != null && cookie.Contains("session_token=valid123") && !string.IsNullOrEmpty(referer))
                    {
                        res.StatusCode = 200;
                        await res.OutputStream.WriteAsync(NormalContent);
                    }
                    else
                    {
                        res.StatusCode = 403;
                    }
                    break;

                // 12. ETag/Last-Modified değişen dosya
                case "/dynamic-file":
                    res.Headers["ETag"] = DynamicEtag;
                    var dynBytes = Encoding.UTF8.GetBytes(DynamicContent);
                    res.StatusCode = 200;
                    res.ContentLength64 = dynBytes.Length;
                    await res.OutputStream.WriteAsync(dynBytes);
                    break;

                // 13. Büyük deterministik akış (bellekte tutmadan 1 MB deterministik veri, Range destekli)
                case "/large-stream":
                    long totalLargeBytes = 1024 * 1024; // 1 MB
                    res.Headers["Accept-Ranges"] = "bytes";
                    var rangeHdr = req.Headers["Range"];
                    if (!string.IsNullOrEmpty(rangeHdr) && rangeHdr.StartsWith("bytes="))
                    {
                        var parts = rangeHdr["bytes=".Length..].Split('-');
                        long start = long.Parse(parts[0]);
                        long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                            ? long.Parse(parts[1])
                            : totalLargeBytes - 1;

                        if (start >= totalLargeBytes || end >= totalLargeBytes || start > end)
                        {
                            res.StatusCode = 416;
                            return;
                        }

                        long len = end - start + 1;
                        res.StatusCode = 206;
                        res.Headers["Content-Range"] = $"bytes {start}-{end}/{totalLargeBytes}";
                        res.ContentLength64 = len;

                        long written = 0;
                        while (written < len)
                        {
                            long globalPos = start + written;
                            int patternOffset = (int)(globalPos % LargeChunkPattern.Length);
                            int toWrite = (int)Math.Min(LargeChunkPattern.Length - patternOffset, len - written);
                            await res.OutputStream.WriteAsync(LargeChunkPattern, patternOffset, toWrite);
                            written += toWrite;
                        }
                    }
                    else
                    {
                        res.StatusCode = 200;
                        res.ContentLength64 = totalLargeBytes;

                        long sent = 0;
                        while (sent < totalLargeBytes)
                        {
                            int toSend = (int)Math.Min(LargeChunkPattern.Length, totalLargeBytes - sent);
                            await res.OutputStream.WriteAsync(LargeChunkPattern, 0, toSend);
                            sent += toSend;
                        }
                    }
                    break;

                default:
                    res.StatusCode = 404;
                    break;
            }
        }
        catch
        {
            // İstemci bağlantıyı erken keserse veya hata olursa yut
        }
        finally
        {
            try { res.Close(); } catch { }
        }
    }

    private static async Task HandleNormalAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        res.Headers["Accept-Ranges"] = "bytes";
        var rangeHeader = req.Headers["Range"];

        if (string.IsNullOrEmpty(rangeHeader))
        {
            res.StatusCode = 200;
            res.ContentLength64 = NormalContent.Length;
            await res.OutputStream.WriteAsync(NormalContent);
            return;
        }

        // bytes=start-end biçimini ayrıştır
        if (rangeHeader.StartsWith("bytes="))
        {
            var parts = rangeHeader["bytes=".Length..].Split('-');
            long start = long.Parse(parts[0]);
            long end = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                ? long.Parse(parts[1])
                : NormalContent.Length - 1;

            if (start >= NormalContent.Length || end >= NormalContent.Length || start > end)
            {
                res.StatusCode = 416; // Range Not Satisfiable
                return;
            }

            long length = end - start + 1;
            res.StatusCode = 206; // Partial Content
            res.Headers["Content-Range"] = $"bytes {start}-{end}/{NormalContent.Length}";
            res.ContentLength64 = length;
            await res.OutputStream.WriteAsync(NormalContent, (int)start, (int)length);
            return;
        }

        res.StatusCode = 200;
        res.ContentLength64 = NormalContent.Length;
        await res.OutputStream.WriteAsync(NormalContent);
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
            _cts.Dispose();
        }
        catch { }
    }
}

