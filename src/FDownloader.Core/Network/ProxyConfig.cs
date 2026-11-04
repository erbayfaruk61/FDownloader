using System.Net;

namespace FDownloader.Core.Network;

/// <summary>
/// Proxy çalışma modu.
/// </summary>
public enum ProxyMode
{
    /// <summary>Proxy kullanma (doğrudan bağlantı).</summary>
    None = 0,

    /// <summary>İşletim sistemi varsayılan proxy ayarlarını kullan.</summary>
    System = 1,

    /// <summary>Kullanıcı tarafından elle girilen proxy sunucusunu kullan.</summary>
    Manual = 2
}

/// <summary>
/// Desteklenen proxy protokolleri.
/// </summary>
public enum ProxyProtocol
{
    /// <summary>HTTP proxy.</summary>
    Http = 0,

    /// <summary>HTTPS proxy (tünelleme / CONNECT).</summary>
    Https = 1,

    /// <summary>SOCKS5 proxy (TCP & DNS).</summary>
    Socks5 = 2
}

/// <summary>
/// Proxy yapılandırma modeli.
/// </summary>
public sealed class ProxyConfig
{
    public ProxyMode Mode { get; set; } = ProxyMode.System;
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.Http;
    public string? Server { get; set; }
    public int Port { get; set; } = 8080;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool BypassLocal { get; set; } = true;
    public List<string> BypassList { get; set; } = new();

    /// <summary>
    /// Yapılandırmaya uygun IWebProxy nesnesi oluşturur. Proxy kapalıysa null döner.
    /// </summary>
    public IWebProxy? CreateWebProxy()
    {
        if (Mode == ProxyMode.None)
        {
            return null;
        }

        if (Mode == ProxyMode.System)
        {
            return WebRequest.GetSystemWebProxy();
        }

        if (Mode == ProxyMode.Manual && !string.IsNullOrWhiteSpace(Server))
        {
            string scheme = Protocol switch
            {
                ProxyProtocol.Socks5 => "socks5",
                ProxyProtocol.Https => "https",
                _ => "http"
            };

            var proxyUri = new Uri($"{scheme}://{Server.Trim()}:{Port}");
            var webProxy = new WebProxy(proxyUri, BypassLocal, BypassList.ToArray());

            if (!string.IsNullOrWhiteSpace(Username))
            {
                webProxy.Credentials = new NetworkCredential(Username, Password ?? string.Empty);
            }

            return webProxy;
        }

        return null;
    }
}

