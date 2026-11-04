using System.Net;

namespace FDownloader.Core.Network;

/// <summary>
/// Proxy ve site kimlik bilgileriyle yapılandırılmış SocketsHttpHandler ve HttpClient örnekleri üreten fabrika.
/// </summary>
public static class NetworkHandlerFactory
{
    /// <summary>
    /// Verilen proxy ve kimlik doğrulama ayarlarına göre yapılandırılmış bir SocketsHttpHandler oluşturur.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(
        ProxyConfig? proxyConfig = null,
        SiteCredentialManager? credentialManager = null,
        bool allowAutoRedirect = true)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            EnableMultipleHttp2Connections = true
        };

        // 1. Proxy Yapılandırması
        if (proxyConfig != null)
        {
            var webProxy = proxyConfig.CreateWebProxy();
            if (webProxy != null)
            {
                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }
            else
            {
                handler.Proxy = null;
                handler.UseProxy = false;
            }
        }
        else
        {
            // Varsayılan sistem proxy'sini kullan
            handler.Proxy = WebRequest.GetSystemWebProxy();
            handler.UseProxy = true;
        }

        // 2. Site Kimlik Bilgileri Yapılandırması (CredentialCache)
        if (credentialManager != null)
        {
            var cache = new CredentialCache();
            foreach (var cred in credentialManager.GetAllCredentials())
            {
                if (!string.IsNullOrWhiteSpace(cred.Host))
                {
                    var netCred = !string.IsNullOrWhiteSpace(cred.Domain)
                        ? new NetworkCredential(cred.Username, cred.Password, cred.Domain)
                        : new NetworkCredential(cred.Username, cred.Password);

                    var authTypes = cred.AuthType.Equals("Any", StringComparison.OrdinalIgnoreCase)
                        ? new[] { "Basic", "Digest", "NTLM" }
                        : new[] { cred.AuthType };

                    foreach (var at in authTypes)
                    {
                        // Standart port URI'leri için eşleşme
                        try { cache.Add(new Uri($"http://{cred.Host}"), at, netCred); } catch { }
                        try { cache.Add(new Uri($"https://{cred.Host}"), at, netCred); } catch { }
                        try { cache.Add(cred.Host, 80, at, netCred); } catch { }
                        try { cache.Add(cred.Host, 443, at, netCred); } catch { }
                        try { cache.Add(cred.Host, 8080, at, netCred); } catch { }
                    }
                }
            }
            handler.Credentials = cache;
        }

        return handler;
    }

    /// <summary>
    /// Yapılandırılmış bir HttpClient oluşturur.
    /// Basic kimlik doğrulaması kayıtlı siteler için peşin (preemptive) Authorization başlığı eklenir.
    /// </summary>
    public static HttpClient CreateClient(
        ProxyConfig? proxyConfig = null,
        SiteCredentialManager? credentialManager = null,
        TimeSpan? timeout = null)
    {
        var primaryHandler = CreateHandler(proxyConfig, credentialManager);

        HttpMessageHandler topHandler = primaryHandler;
        if (credentialManager != null)
        {
            topHandler = new PreemptiveAuthHandler(credentialManager, primaryHandler);
        }

        return new HttpClient(topHandler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(60)
        };
    }
}

/// <summary>
/// Kayıtlı Basic kimlik doğrulaması olan siteler için sunucudan 401 beklemeden
/// peşin (preemptive) olarak Authorization başlığı ekleyen ara katman işleyicisi.
/// </summary>
internal sealed class PreemptiveAuthHandler : DelegatingHandler
{
    private readonly SiteCredentialManager _credentialManager;

    public PreemptiveAuthHandler(SiteCredentialManager credentialManager, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _credentialManager = credentialManager;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri != null && !request.Headers.Contains("Authorization"))
        {
            var basicHeader = _credentialManager.GetBasicAuthHeaderValue(request.RequestUri);
            if (basicHeader != null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", basicHeader);
            }
        }
        return base.SendAsync(request, cancellationToken);
    }
}
