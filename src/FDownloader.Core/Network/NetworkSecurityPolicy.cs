namespace FDownloader.Core.Network;

/// <summary>
/// Ağ istekleri ve yönlendirmeleri için güvenlik ve gizlilik kurallarını denetleyen yardımcı sınıf.
/// </summary>
public static class NetworkSecurityPolicy
{
    /// <summary>
    /// Orijinal URI ile hedef yönlendirme URI'si arasında çerez veya kimlik bilgilerinin aktarılmasının güvenli olup olmadığını kontrol eder.
    /// Farklı host/domain veya HTTPS'ten HTTP'ye güvensiz düşürmelerde false döner (Cross-domain ve downgrade koruması).
    /// </summary>
    public static bool ShouldPreserveSensitiveHeaders(Uri originalUri, Uri targetUri)
    {
        // 1. HTTPS -> HTTP düşürme (downgrade) durumunda asla hassas başlık taşınmaz
        if (originalUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            targetUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 2. Host (Domain) aynı mı kontrol et (Cross-domain koruması)
        return string.Equals(originalUri.Host, targetUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// HTTPS'ten HTTP'ye bir güvenlik seviyesi düşürme (downgrade) olup olmadığını döner.
    /// </summary>
    public static bool IsHttpsDowngrade(Uri originalUri, Uri targetUri)
    {
        return originalUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
               targetUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Yalnızca güvenli şemaları (http, https, ftp) kabul eder; dosya/süreç enjeksiyonuna açık şemaları (file://, javascript://, data:// vb.) engeller.
    /// </summary>
    public static bool IsSafeUrlScheme(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                   uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                   uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

