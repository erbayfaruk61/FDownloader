using System.Text.RegularExpressions;
using FDownloader.Core.Domain;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Utilities;

/// <summary>
/// Web sitelerindeki indirilebilir tüm dosyaları analiz edip listeleyen servis (Site Grabber).
/// Sayfadaki görselleri, videoları, belgeleri ve arşivleri topluca çekmeyi sağlar.
/// </summary>
public sealed class SiteGrabberService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SiteGrabberService> _logger;

    private static readonly Regex HrefRegex = new(@"href\s*=\s*[""'](?<url>[^""'#\s]+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SrcRegex = new(@"src\s*=\s*[""'](?<url>[^""'#\s]+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SiteGrabberService(HttpClient httpClient, ILogger<SiteGrabberService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Verilen web adresini tarar ve bulunan tüm indirilebilir dosya bağlantılarını döner.
    /// </summary>
    public async Task<List<GrabbedFileItem>> AnalyzePageAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        var results = new List<GrabbedFileItem>();
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("Geçersiz URL formatı.", nameof(pageUrl));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0.0.0 Safari/537.36");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var foundUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // href ve src bağlantılarını tara
            foreach (Match match in HrefRegex.Matches(html))
            {
                var val = match.Groups["url"].Value.Trim();
                if (TryResolveUrl(baseUri, val, out var resolved)) foundUrls.Add(resolved);
            }

            foreach (Match match in SrcRegex.Matches(html))
            {
                var val = match.Groups["url"].Value.Trim();
                if (TryResolveUrl(baseUri, val, out var resolved)) foundUrls.Add(resolved);
            }

            foreach (var url in foundUrls)
            {
                var fileName = Path.GetFileName(new Uri(url).LocalPath);
                if (string.IsNullOrWhiteSpace(fileName) || fileName.Length <= 1) continue;

                var category = DetermineCategory(fileName);
                results.Add(new GrabbedFileItem
                {
                    Url = url,
                    FileName = FileNameSanitizer.Sanitize(fileName),
                    Category = category,
                    IsSelected = category != DownloadCategory.Other // Bilinen dosya türleri varsayılan seçili gelsin
                });
            }

            _logger.LogInformation("Site Grabber {Count} dosya buldu: {Url}", results.Count, pageUrl);
            return results.OrderBy(r => r.Category).ThenBy(r => r.FileName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Site Grabber analizi başarısız oldu: {Url}", pageUrl);
            throw;
        }
    }

    private static bool TryResolveUrl(Uri baseUri, string relativeOrAbsolute, out string resolvedUrl)
    {
        resolvedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute) ||
            relativeOrAbsolute.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            relativeOrAbsolute.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            relativeOrAbsolute.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(baseUri, relativeOrAbsolute, out var resolved) &&
            (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps))
        {
            resolvedUrl = resolved.ToString();
            return true;
        }

        return false;
    }

    private static DownloadCategory DetermineCategory(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".flv" => DownloadCategory.Video,
            ".mp3" or ".flac" or ".wav" or ".m4a" or ".aac" or ".ogg" => DownloadCategory.Music,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => DownloadCategory.Document,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" => DownloadCategory.Archive,
            ".exe" or ".msi" or ".bat" or ".cmd" or ".apk" => DownloadCategory.Program,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".ico" => DownloadCategory.Image,
            ".torrent" => DownloadCategory.Torrent,
            _ => DownloadCategory.Other
        };
    }
}

public class GrabbedFileItem
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DownloadCategory Category { get; set; }
    public bool IsSelected { get; set; } = true;
}
