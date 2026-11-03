using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Engine.Probe;

/// <summary>
/// Bir URL hakkında bilgi toplar: dosya boyutu, range desteği, dosya adı, ETag.
/// İndirmeye başlamadan önce her zaman çalıştırılır.
/// </summary>
public sealed class DownloadProber
{
    private readonly HttpClient _http;
    private readonly ILogger<DownloadProber> _logger;

    public DownloadProber(HttpClient http, ILogger<DownloadProber> logger)
    {
        _http = http;
        _logger = logger;
    }

    private const int MaxRedirects = 10;

    /// <summary>
    /// Verilen URL'yi probe eder ve dosya hakkında bilgi döner.
    /// </summary>
    /// <param name="url">İndirilecek URL.</param>
    /// <param name="referer">İstek Referer başlığı (opsiyonel).</param>
    /// <param name="cookies">Cookie dizgesi (opsiyonel).</param>
    /// <param name="userAgent">Özel User-Agent (opsiyonel).</param>
    /// <param name="ct">İptal jetonu.</param>
    public async Task<Network.ProbeResult> ProbeAsync(
        string url,
        string? referer = null,
        string? cookies = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Probe başladı: {Url}", url);

        try
        {
            // Önce HEAD isteği dene
            var result = await TryHeadProbeAsync(url, referer, cookies, userAgent, ct);
            if (result is not null && result.IsSuccess) return result;

            // HEAD başarısız olduysa küçük Range isteği ile dene
            _logger.LogDebug("HEAD başarısız veya desteklenmiyor, Range isteği ile probe deneniyor: {Url}", url);
            return await RangeProbeAsync(url, referer, cookies, userAgent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Probe başarısız: {Url}", url);
            return new Network.ProbeResult
            {
                FinalUrl = url,
                FileName = FileNameSanitizer.ExtractFromUrl(url) ?? "downloaded_file",
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<Network.ProbeResult?> TryHeadProbeAsync(
        string url, string? referer, string? cookies, string? userAgent, CancellationToken ct)
    {
        var (response, finalUrl, error) = await ExecuteWithRedirectsAsync(
            HttpMethod.Head, url, referer, cookies, userAgent, isRangeProbe: false, ct);

        if (error is not null)
        {
            return new Network.ProbeResult
            {
                FinalUrl = finalUrl,
                FileName = FileNameSanitizer.ExtractFromUrl(finalUrl) ?? "downloaded_file",
                IsSuccess = false,
                ErrorMessage = error
            };
        }

        if (response is null) return null;

        using (response)
        {
            // Bazı sunucular HEAD metoduna 405 veya 403 döner
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                _logger.LogDebug("HEAD {StatusCode} döndü, Range ile devam edilecek", (int)response.StatusCode);
                return null;
            }

            return BuildResult(url, finalUrl, response.Headers, response.Content.Headers);
        }
    }

    private async Task<Network.ProbeResult> RangeProbeAsync(
        string url, string? referer, string? cookies, string? userAgent, CancellationToken ct)
    {
        var (response, finalUrl, error) = await ExecuteWithRedirectsAsync(
            HttpMethod.Get, url, referer, cookies, userAgent, isRangeProbe: true, ct);

        if (error is not null)
        {
            return new Network.ProbeResult
            {
                FinalUrl = finalUrl,
                FileName = FileNameSanitizer.ExtractFromUrl(finalUrl) ?? "downloaded_file",
                IsSuccess = false,
                ErrorMessage = error
            };
        }

        if (response is null)
        {
            return new Network.ProbeResult
            {
                FinalUrl = url,
                FileName = FileNameSanitizer.ExtractFromUrl(url) ?? "downloaded_file",
                IsSuccess = false,
                ErrorMessage = "Sunucudan yanıt alınamadı."
            };
        }

        using (response)
        {
            try
            {
                await response.Content.ReadAsByteArrayAsync(ct); // Yanıtı oku ve bağlantıyı serbest bırak
            }
            catch { }

            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                _logger.LogWarning("Range probe başarısız: HTTP {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);
                return new Network.ProbeResult
                {
                    FinalUrl = finalUrl,
                    FileName = FileNameSanitizer.ExtractFromUrl(finalUrl) ?? "downloaded_file",
                    IsSuccess = false,
                    ErrorMessage = $"Sunucu {(int)response.StatusCode} ({response.ReasonPhrase}) yanıtı döndü."
                };
            }

            return BuildResult(url, finalUrl, response.Headers, response.Content.Headers);
        }
    }

    private async Task<(HttpResponseMessage? Response, string FinalUrl, string? Error)> ExecuteWithRedirectsAsync(
        HttpMethod method,
        string startUrl,
        string? referer,
        string? cookies,
        string? userAgent,
        bool isRangeProbe,
        CancellationToken ct)
    {
        string currentUrl = startUrl;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int redirectCount = 0; redirectCount <= MaxRedirects; redirectCount++)
        {
            if (!visited.Add(currentUrl))
            {
                return (null, currentUrl, $"Yönlendirme döngüsü tespit edildi: {currentUrl}");
            }

            HttpRequestMessage request = BuildRequest(method, currentUrl, referer, cookies, userAgent);
            if (isRangeProbe)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("redirect", StringComparison.OrdinalIgnoreCase))
            {
                return (null, currentUrl, $"Yönlendirme döngüsü veya çok fazla yönlendirme: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (null, currentUrl, ex.Message);
            }

            // Eğer otomatik yönlendirme ile başka bir URL'e varılmışsa
            var autoFinalUri = response.RequestMessage?.RequestUri?.ToString();
            if (!string.IsNullOrEmpty(autoFinalUri) && !string.Equals(autoFinalUri, currentUrl, StringComparison.OrdinalIgnoreCase))
            {
                currentUrl = autoFinalUri;
            }

            if (!IsRedirect(response.StatusCode))
            {
                return (response, currentUrl, null);
            }

            // Manuel 3xx yönlendirme durumu
            var location = response.Headers.Location;
            response.Dispose();

            if (location is null)
            {
                return (null, currentUrl, "Yönlendirme yanıtında Location başlığı bulunamadı.");
            }

            var baseUri = new Uri(currentUrl);
            currentUrl = new Uri(baseUri, location).ToString();
        }

        return (null, currentUrl, $"Maksimum yönlendirme sınırı ({MaxRedirects}) aşıldı.");
    }

    private static bool IsRedirect(System.Net.HttpStatusCode code) =>
        code is System.Net.HttpStatusCode.MovedPermanently
             or System.Net.HttpStatusCode.Found
             or System.Net.HttpStatusCode.SeeOther
             or System.Net.HttpStatusCode.TemporaryRedirect
             or (System.Net.HttpStatusCode)308;

    private Network.ProbeResult BuildResult(
        string originalUrl,
        string finalUrl,
        System.Net.Http.Headers.HttpResponseHeaders responseHeaders,
        System.Net.Http.Headers.HttpContentHeaders contentHeaders)
    {
        // Dosya adı belirleme (öncelik sırası)
        string? fileName =
            FileNameSanitizer.ExtractFromContentDisposition(
                contentHeaders.ContentDisposition?.ToString()) ??
            FileNameSanitizer.ExtractFromUrl(finalUrl) ??
            FileNameSanitizer.ExtractFromUrl(originalUrl);

        // MIME'den uzantı tahmini
        string? mimeType = contentHeaders.ContentType?.MediaType;
        string fallbackExt = GetExtensionFromMime(mimeType);

        fileName = FileNameSanitizer.Sanitize(fileName, fallbackExt);

        // Range desteği: Accept-Ranges: bytes veya sunucunun Content-Range dönmesi
        bool supportsRange = (responseHeaders.AcceptRanges?.Contains("bytes") == true) ||
                             (contentHeaders.ContentRange != null);

        // Boyut belirleme: 206 Partial Content yanıtlarında Content-Length sadece parçanın boyutudur (örn 1 byte).
        // Gerçek dosya boyutu her zaman Content-Range başlığında (/52428800) yer alır!
        long contentLength = -1;

        if (contentHeaders.ContentRange?.Length.HasValue == true)
        {
            contentLength = contentHeaders.ContentRange.Length.Value;
            supportsRange = true;
        }
        else if (contentHeaders.TryGetValues("Content-Range", out var rangeValues) ||
                 responseHeaders.TryGetValues("Content-Range", out rangeValues))
        {
            var rangeStr = rangeValues.FirstOrDefault() ?? "";
            var match = System.Text.RegularExpressions.Regex.Match(rangeStr, @"/(\d+)");
            if (match.Success)
            {
                contentLength = long.Parse(match.Groups[1].Value);
                supportsRange = true;
            }
        }
        else if (contentHeaders.ContentLength.HasValue)
        {
            contentLength = contentHeaders.ContentLength.Value;
        }

        // Content-Encoding kontrolü (gzip vb. — parçalı indirmeyi engeller)
        bool hasEncoding = contentHeaders.ContentEncoding?.Any() == true;

        string? etag = responseHeaders.ETag?.Tag;
        string? lastModified = contentHeaders.LastModified?.ToString("R");

        _logger.LogInformation(
            "Probe tamamlandı: {FileName}, Boyut={Size}, Range={Range}, ETag={ETag}",
            fileName, contentLength, supportsRange, etag);

        return new Network.ProbeResult
        {
            FinalUrl = finalUrl,
            FileName = fileName,
            ContentLength = contentLength,
            SupportsRange = supportsRange,
            ContentType = mimeType,
            ETag = etag,
            LastModified = lastModified,
            HasContentEncoding = hasEncoding,
            IsSuccess = true
        };
    }

    private HttpRequestMessage BuildRequest(
        HttpMethod method, string url,
        string? referer, string? cookies, string? userAgent)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent ?? Network.NetworkConstants.DefaultUserAgent);

        if (!string.IsNullOrWhiteSpace(referer))
            request.Headers.TryAddWithoutValidation("Referer", referer);

        if (!string.IsNullOrWhiteSpace(cookies))
            request.Headers.TryAddWithoutValidation("Cookie", cookies);

        // Sıkıştırma isteme (Content-Length tutarsızlığı yaratır)
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");

        return request;
    }

    private static string GetExtensionFromMime(string? mime) => mime switch
    {
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/x-matroska" => ".mkv",
        "video/quicktime" => ".mov",
        "video/x-msvideo" => ".avi",
        "audio/mpeg" => ".mp3",
        "audio/ogg" => ".ogg",
        "audio/flac" => ".flac",
        "audio/wav" or "audio/x-wav" => ".wav",
        "audio/aac" => ".aac",
        "application/pdf" => ".pdf",
        "application/epub+zip" => ".epub",
        "application/zip" or "application/x-zip-compressed" => ".zip",
        "application/x-rar-compressed" or "application/vnd.rar" or "application/x-rar" => ".rar",
        "application/x-7z-compressed" or "application/7z-compressed" or "application/x-compressed" => ".7z",
        "application/x-tar" => ".tar",
        "application/x-gzip" or "application/gzip" => ".gz",
        "application/x-bzip2" or "application/x-bzip" => ".bz2",
        "application/x-xz" => ".xz",
        "application/zstd" => ".zst",
        "application/x-iso9660-image" => ".iso",
        "application/x-apple-diskimage" => ".dmg",
        "application/vnd.android.package-archive" => ".apk",
        "application/x-debian-package" => ".deb",
        "application/x-redhat-package-manager" => ".rpm",
        "application/x-bittorrent" => ".torrent",
        "application/octet-stream" => ".bin",
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/svg+xml" => ".svg",
        _ => string.Empty
    };
}
