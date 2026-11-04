using System.Globalization;
using System.Text.RegularExpressions;
using FDownloader.Core.Utilities;

namespace FDownloader.Core.Batch;

/// <summary>
/// Toplu link listesi metnini veya aralık desenlerini ayrıştırıp geçerli URL listesine dönüştüren servis.
/// </summary>
public static class BatchDownloadParser
{
    private static readonly string[] LineSeparators = ["\r\n", "\r", "\n"];

    private static readonly Regex UrlExtractRegex = new(
        @"https?://[^\s""'<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// Çok satırlı metni, panodan yapıştırılan içeriği veya aralık şablonunu ayrıştırır.
    /// Satır satır URL'leri, metin içindeki linkleri veya [01-20] gibi sayı/harf desenlerini destekler.
    /// </summary>
    public static List<BatchDownloadItem> Parse(string rawInput, BatchDownloadOptions? options = null)
    {
        options ??= new BatchDownloadOptions();
        var items = new List<BatchDownloadItem>();
        if (string.IsNullOrWhiteSpace(rawInput))
            return items;

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = rawInput.Split(LineSeparators, StringSplitOptions.None);
        var currentIndex = 1;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
                continue;

            // 1. Durum: Satır aralık deseni içeriyor mu? (Örn: https://example.com/file_[01-10].zip)
            if (BatchRangePattern.HasPattern(trimmedLine))
            {
                var expandedUrls = BatchRangePattern.ExpandPattern(trimmedLine, options.MaxGeneratedItems);
                foreach (var expUrl in expandedUrls)
                {
                    if (items.Count >= options.MaxGeneratedItems)
                        break;

                    AddItem(expUrl, options, items, seenUrls, ref currentIndex);
                }
            }
            // 2. Durum: Satır doğrudan tek bir URL veya metin içinde linkler içeriyor mu?
            else
            {
                var matches = UrlExtractRegex.Matches(trimmedLine);
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        if (items.Count >= options.MaxGeneratedItems)
                            break;

                        AddItem(match.Value, options, items, seenUrls, ref currentIndex);
                    }
                }
                else
                {
                    // Geçersiz/desteklenmeyen girdi satırı
                    items.Add(new BatchDownloadItem
                    {
                        Index = currentIndex++,
                        Url = trimmedLine,
                        ErrorMessage = "Geçersiz veya desteklenmeyen URL protokolü (yalnızca http:// veya https:// desteklenir)."
                    });
                }
            }

            if (items.Count >= options.MaxGeneratedItems)
                break;
        }

        return items;
    }

    private static void AddItem(
        string rawUrl,
        BatchDownloadOptions options,
        List<BatchDownloadItem> items,
        HashSet<string> seenUrls,
        ref int currentIndex)
    {
        var cleanUrl = rawUrl.Trim();
        if (options.DeduplicateUrls && seenUrls.Contains(cleanUrl))
            return;

        seenUrls.Add(cleanUrl);

        if (!Uri.TryCreate(cleanUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            items.Add(new BatchDownloadItem
            {
                Index = currentIndex++,
                Url = cleanUrl,
                ErrorMessage = "URL biçimi geçersiz."
            });
            return;
        }

        // Önerilen dosya adını çıkar
        var baseFileName = ExtractFileNameFromUri(uri);
        var suggestedName = ApplyFileNamePattern(options.FileNamePattern, baseFileName, currentIndex);

        string? destinationPath = null;
        if (!string.IsNullOrWhiteSpace(options.DestinationDirectory))
        {
            var safeName = FileNameSanitizer.Sanitize(suggestedName);
            destinationPath = System.IO.Path.Combine(options.DestinationDirectory, safeName);
        }

        items.Add(new BatchDownloadItem
        {
            Index = currentIndex++,
            Url = cleanUrl,
            SuggestedFileName = suggestedName,
            DestinationPath = destinationPath
        });
    }

    private static string ExtractFileNameFromUri(Uri uri)
    {
        try
        {
            var path = uri.AbsolutePath.TrimEnd('/');
            var fileName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return Uri.UnescapeDataString(fileName);
            }
        }
        catch
        {
            // Fallback
        }

        return "download";
    }

    private static string ApplyFileNamePattern(string? pattern, string defaultFileName, int index)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return defaultFileName;

        var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(defaultFileName);
        var ext = System.IO.Path.GetExtension(defaultFileName);

        var result = pattern
            .Replace("{name}", nameWithoutExt, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", index.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{index:D2}", index.ToString("D2", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{index:D3}", index.ToString("D3", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{index:D4}", index.ToString("D4", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

        // Eğer kullanıcı desende uzantıyı belirtmediyse varsayılan uzantıyı koru
        if (!System.IO.Path.HasExtension(result) && !string.IsNullOrEmpty(ext))
        {
            result += ext;
        }

        return result;
    }
}
