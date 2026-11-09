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

        // 2. Host (Domain) aynı veya aynı kök/alt alan adına (same-site/subdomain) mı bağlı kontrol et
        return IsSameDomainOrSubdomain(originalUri.Host, targetUri.Host);
    }

    /// <summary>
    /// İki host'un aynı domain, birbirinin alt alanı (subdomain) veya aynı kök alana (apex domain) bağlı olup olmadığını kontrol eder.
    /// </summary>
    public static bool IsSameDomainOrSubdomain(string hostA, string hostB)
    {
        if (string.IsNullOrWhiteSpace(hostA) || string.IsNullOrWhiteSpace(hostB))
            return false;

        hostA = hostA.Trim().ToLowerInvariant();
        hostB = hostB.Trim().ToLowerInvariant();

        if (hostA == hostB)
            return true;

        if (hostA.EndsWith("." + hostB) || hostB.EndsWith("." + hostA))
            return true;

        var rootA = GetRootDomain(hostA);
        var rootB = GetRootDomain(hostB);
        return !string.IsNullOrEmpty(rootA) && rootA == rootB;
    }

    /// <summary>
    /// Host adresinin kök alan adını (apex domain) ayıklar (örn. cold-1.gofile.io -> gofile.io).
    /// </summary>
    public static string GetRootDomain(string host)
    {
        if (System.Net.IPAddress.TryParse(host, out _))
            return host;

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
            return host;

        string secondToLast = parts[^2];
        if (parts.Length >= 3 && (secondToLast is "com" or "net" or "org" or "gov" or "edu" or "co") && parts[^1].Length <= 3)
        {
            return $"{parts[^3]}.{parts[^2]}.{parts[^1]}";
        }

        return $"{parts[^2]}.{parts[^1]}";
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

