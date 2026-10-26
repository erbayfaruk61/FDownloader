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
            if (result is not null) return result;

            // HEAD başarısız olduysa küçük Range isteği ile dene
            _logger.LogDebug("HEAD başarısız, Range isteği ile probe deneniyor: {Url}", url);
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
        using var request = BuildRequest(HttpMethod.Head, url, referer, cookies, userAgent);
        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch
        {
            return null; // HEAD çalışmadı, Range ile devam
        }

        // Bazı sunucular HEAD'e 405 döner
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            _logger.LogDebug("HEAD {StatusCode} döndü, Range ile devam", (int)response.StatusCode);
            return null;
        }

        return BuildResult(url, response.RequestMessage?.RequestUri?.ToString() ?? url, response.Headers, response.Content.Headers);
    }

    private async Task<Network.ProbeResult> RangeProbeAsync(
        string url, string? referer, string? cookies, string? userAgent, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Get, url, referer, cookies, userAgent);
        // Sadece ilk 0 byte'ı iste — sunucu Range yanıtı verir veya tam içerik döner
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            await response.Content.ReadAsByteArrayAsync(ct); // Bağlantıyı serbest bırak
        }
        catch (Exception ex)
        {
            return new Network.ProbeResult
            {
                FinalUrl = url,
                FileName = FileNameSanitizer.ExtractFromUrl(url) ?? "downloaded_file",
                IsSuccess = false,
                ErrorMessage = $"Probe bağlantı hatası: {ex.Message}"
            };
        }

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            _logger.LogWarning("Range probe başarısız: HTTP {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);
            return new Network.ProbeResult
            {
                FinalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url,
                FileName = FileNameSanitizer.ExtractFromUrl(url) ?? "downloaded_file",
                IsSuccess = false,
                ErrorMessage = $"Sunucu {(int)response.StatusCode} ({response.ReasonPhrase}) yanıtı döndü."
            };
        }

        return BuildResult(url, response.RequestMessage?.RequestUri?.ToString() ?? url, response.Headers, response.Content.Headers);
    }

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
