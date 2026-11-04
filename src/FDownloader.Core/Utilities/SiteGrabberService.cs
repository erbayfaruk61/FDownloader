using System.Text.RegularExpressions;
using FDownloader.Core.Domain;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Utilities;

/// <summary>
/// Site yakalayıcı (Site Grabber) için tarama parametreleri.
/// </summary>
public class SiteGrabberOptions
{
    /// <summary>
    /// Tarama derinliği. 1 = yalnızca verilen sayfa, 2 = sayfadaki iç bağlantılar vb.
    /// </summary>
    public int MaxDepth { get; set; } = 1;

    /// <summary>
    /// Sadece aynı alan adına (domain) ait sayfaları tara. Dış sitelere sıçramaz.
    /// </summary>
    public bool SameDomainOnly { get; set; } = true;

    /// <summary>
    /// Sitenin robots.txt kurallarına (Disallow yönergelerine) uy.
    /// </summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>
    /// İstekler arası nezaket bekleme süresi (milisaniye). Sunucuyu boğmamak için uygulanır.
    /// </summary>
    public int DelayBetweenRequestsMs { get; set; } = 100;

    /// <summary>
    /// Taranacak maksimum sayfa sayısı (aşırı büyük sitelerde sonsuz döngü ve donmayı engeller).
    /// </summary>
    public int MaxPagesToCrawl { get; set; } = 30;

    /// <summary>
    /// İndirilecek maksimum HTML boyutu (bayt). Aşırı büyük sayfalarda bellek şişmesini engeller.
    /// </summary>
    public long MaxHtmlSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Sadece belirli kategorilerdeki dosyaları topla (null ise tümü).
    /// </summary>
    public HashSet<DownloadCategory>? AllowedCategories { get; set; }
}

/// <summary>
/// robots.txt kurallarını ayrıştıran ve izin durumunu denetleyen sınıf.
/// </summary>
public sealed class RobotsTxtRules
{
    private readonly List<string> _disallowedPaths = new();

    public static async Task<RobotsTxtRules> FetchAndParseAsync(HttpClient httpClient, Uri baseUri, CancellationToken ct)
    {
        var rules = new RobotsTxtRules();
        try
        {
            var robotsUri = new Uri(baseUri, "/robots.txt");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));

            using var res = await httpClient.GetAsync(robotsUri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync(cts.Token);
                rules.Parse(content);
            }
        }
        catch { }
        return rules;
    }

    public void Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        bool appliesToUs = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#') || string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
            {
                var agent = line["User-agent:".Length..].Trim();
                appliesToUs = agent == "*" || agent.Contains("FDownloader", StringComparison.OrdinalIgnoreCase);
            }
            else if (appliesToUs && line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
            {
                var path = line["Disallow:".Length..].Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    _disallowedPaths.Add(path);
                }
            }
        }
    }

    public bool IsAllowed(Uri uri)
    {
        if (_disallowedPaths.Count == 0) return true;
        var pathAndQuery = uri.PathAndQuery;
        foreach (var disallowed in _disallowedPaths)
        {
            if (pathAndQuery.StartsWith(disallowed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// Web sitelerindeki indirilebilir tüm dosyaları analiz edip listeleyen servis (Site Grabber).
/// Sayfadaki görselleri, videoları, belgeleri ve arşivleri derinlik, robots.txt ve hız sınırıyla çeker.
/// </summary>
public sealed class SiteGrabberService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SiteGrabberService> _logger;

    // ReDoS korumalı Regex (3 saniye zaman aşımı)
    private static readonly Regex HrefRegex = new(@"href\s*=\s*[""'](?<url>[^""'#\s]+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(3));
    private static readonly Regex SrcRegex = new(@"src\s*=\s*[""'](?<url>[^""'#\s]+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(3));

    public SiteGrabberService(HttpClient httpClient, ILogger<SiteGrabberService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Geriye dönük uyumlu temel sayfa analizi.
    /// </summary>
    public Task<List<GrabbedFileItem>> AnalyzePageAsync(string pageUrl)
    {
        return AnalyzePageAsync(pageUrl, options: null, progress: null, CancellationToken.None);
    }

    /// <summary>
    /// İptal belirteci ile geriye dönük uyumlu sayfa analizi.
    /// </summary>
    public Task<List<GrabbedFileItem>> AnalyzePageAsync(string pageUrl, CancellationToken cancellationToken)
    {
        return AnalyzePageAsync(pageUrl, options: null, progress: null, cancellationToken);
    }

    /// <summary>
    /// Web adresini derinlik, robots.txt ve filtre kurallarına uygun olarak tarar.
    /// </summary>
    public async Task<List<GrabbedFileItem>> AnalyzePageAsync(
        string pageUrl,
        SiteGrabberOptions? options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SiteGrabberOptions();

        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var initialUri))
        {
            throw new ArgumentException("Geçersiz URL formatı.", nameof(pageUrl));
        }

        var results = new List<GrabbedFileItem>();
        var foundFileUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(Uri Uri, int Depth)>();

        // 1. robots.txt kontrolü
        RobotsTxtRules? robotsRules = null;
        if (options.RespectRobotsTxt)
        {
            progress?.Report("robots.txt denetleniyor...");
            robotsRules = await RobotsTxtRules.FetchAndParseAsync(_httpClient, initialUri, cancellationToken);
            if (!robotsRules.IsAllowed(initialUri))
            {
                _logger.LogWarning("Başlangıç adresi sitenin robots.txt kuralları tarafından engellenmiş: {Url}", initialUri);
                return results;
            }
        }

        queue.Enqueue((initialUri, 1));

        while (queue.Count > 0 && visitedPages.Count < options.MaxPagesToCrawl)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (currentUri, currentDepth) = queue.Dequeue();
            var currentUrlStr = currentUri.ToString();

            if (!visitedPages.Add(currentUrlStr))
            {
                continue;
            }

            progress?.Report($"Taranıyor (Derinlik {currentDepth}): {currentUri.PathAndQuery}");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0.0.0 Safari/537.36");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
                var contentLength = response.Content.Headers.ContentLength ?? 0;

                // Eğer doğrudan bir dosya veya medya linki verilmişse direkt sonuçlara ekle
                if (!contentType.Contains("text/html") && !contentType.Contains("application/xhtml") && !contentType.Contains("text/xml"))
                {
                    AddFileResult(results, foundFileUrls, currentUri.ToString(), currentDepth, contentLength > 0 ? contentLength : null, contentType, options);
                    continue;
                }

                // Aşırı büyük HTML sayfalarında donmayı engelleme (MaxHtmlSizeBytes)
                if (contentLength > options.MaxHtmlSizeBytes)
                {
                    _logger.LogWarning("Sayfa boyutu izin verilen sınırı ({Max} bayt) aştığı için atlandı: {Url}", options.MaxHtmlSizeBytes, currentUri);
                    continue;
                }

                // HTML içeriğini güvenli oku
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                if (html.Length > options.MaxHtmlSizeBytes)
                {
                    html = html[..(int)options.MaxHtmlSizeBytes];
                }

                // Sayfadaki bağlantıları ayıkla
                var extractedUrls = ExtractLinks(currentUri, html);

                foreach (var extracted in extractedUrls)
                {
                    if (extracted.Scheme != Uri.UriSchemeHttp && extracted.Scheme != Uri.UriSchemeHttps)
                    {
                        continue;
                    }

                    // robots.txt kontrolü
                    if (options.RespectRobotsTxt && robotsRules != null && !robotsRules.IsAllowed(extracted))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(extracted.LocalPath).ToLowerInvariant();
                    var isHtmlPage = ext is ".html" or ".htm" or ".php" or ".asp" or ".aspx" or ".jsp" || string.IsNullOrEmpty(ext);

                    if (isHtmlPage)
                    {
                        // Sonraki derinlik için kuyruğa ekle
                        if (currentDepth < options.MaxDepth)
                        {
                            if (!options.SameDomainOnly || IsSameDomain(initialUri, extracted))
                            {
                                if (!visitedPages.Contains(extracted.ToString()))
                                {
                                    queue.Enqueue((extracted, currentDepth + 1));
                                }
                            }
                        }
                    }
                    else
                    {
                        // İndirilebilir dosya / medya
                        AddFileResult(results, foundFileUrls, extracted.ToString(), currentDepth, null, null, options);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Site Grabber sayfa analiz hatası: {Url}", currentUri);
            }

            // Nezaket gecikmesi (sunucuyu boğmama)
            if (options.DelayBetweenRequestsMs > 0 && queue.Count > 0)
            {
                await Task.Delay(options.DelayBetweenRequestsMs, cancellationToken);
            }
        }

        _logger.LogInformation("Site Grabber taraması tamamlandı: {Count} dosya bulundu ({Pages} sayfa gezildi).", results.Count, visitedPages.Count);
        return results.OrderBy(r => r.Category).ThenBy(r => r.FileName).ToList();
    }

    /// <summary>
    /// Bulunan dosyaların boyutlarını paralel HEAD istekleriyle sorgular.
    /// </summary>
    public async Task ProbeFileSizesAsync(
        IEnumerable<GrabbedFileItem> items,
        int maxConcurrency = 4,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, item.Url);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    if (res.Content.Headers.ContentLength.HasValue)
                    {
                        item.FileSize = res.Content.Headers.ContentLength.Value;
                    }
                    if (res.Content.Headers.ContentType != null)
                    {
                        item.ContentType = res.Content.Headers.ContentType.MediaType;
                    }
                }
            }
            catch { }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private static void AddFileResult(
        List<GrabbedFileItem> results,
        HashSet<string> foundFileUrls,
        string url,
        int depth,
        long? fileSize,
        string? contentType,
        SiteGrabberOptions options)
    {
        if (!foundFileUrls.Add(url)) return;

        var localPath = new Uri(url).LocalPath;
        var fileName = Path.GetFileName(localPath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length <= 1) return;

        var category = DetermineCategory(fileName);

        // Kategori filtresi
        if (options.AllowedCategories != null && !options.AllowedCategories.Contains(category))
        {
            return;
        }

        results.Add(new GrabbedFileItem
        {
            Url = url,
            FileName = FileNameSanitizer.Sanitize(fileName),
            Category = category,
            FileSize = fileSize,
            ContentType = contentType,
            DepthFound = depth,
            IsSelected = category != DownloadCategory.Other
        });
    }

    private static HashSet<Uri> ExtractLinks(Uri baseUri, string html)
    {
        var result = new HashSet<Uri>();

        foreach (Match match in HrefRegex.Matches(html))
        {
            var val = match.Groups["url"].Value.Trim();
            if (TryResolveUrl(baseUri, val, out var resolved)) result.Add(resolved);
        }

        foreach (Match match in SrcRegex.Matches(html))
        {
            var val = match.Groups["url"].Value.Trim();
            if (TryResolveUrl(baseUri, val, out var resolved)) result.Add(resolved);
        }

        return result;
    }

    private static bool TryResolveUrl(Uri baseUri, string relativeOrAbsolute, out Uri resolvedUri)
    {
        resolvedUri = null!;
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute) ||
            relativeOrAbsolute.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            relativeOrAbsolute.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            relativeOrAbsolute.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            relativeOrAbsolute.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(baseUri, relativeOrAbsolute, out var resolved) &&
            (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps))
        {
            resolvedUri = resolved;
            return true;
        }

        return false;
    }

    public static bool IsSameDomain(Uri baseUri, Uri targetUri)
    {
        return string.Equals(baseUri.Host, targetUri.Host, StringComparison.OrdinalIgnoreCase) ||
               targetUri.Host.EndsWith("." + baseUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    public static DownloadCategory DetermineCategory(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".flv" or ".m4v" => DownloadCategory.Video,
            ".mp3" or ".flac" or ".wav" or ".m4a" or ".aac" or ".ogg" or ".wma" => DownloadCategory.Music,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".epub" => DownloadCategory.Document,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" or ".bz2" => DownloadCategory.Archive,
            ".exe" or ".msi" or ".bat" or ".cmd" or ".apk" or ".dmg" or ".pkg" => DownloadCategory.Program,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".ico" or ".bmp" => DownloadCategory.Image,
            ".torrent" => DownloadCategory.Torrent,
            _ => DownloadCategory.Other
        };
    }
}

/// <summary>
/// Yakalanan dosya öğesi bilgisi.
/// </summary>
public class GrabbedFileItem
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DownloadCategory Category { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public bool IsSelected { get; set; } = true;
    public int DepthFound { get; set; } = 1;
}
