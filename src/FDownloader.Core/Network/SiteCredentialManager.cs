using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using FDownloader.Core.Storage;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Network;

/// <summary>
/// Belirli bir alan adı / web sitesi için kayıtlı kimlik doğrulama bilgisi.
/// </summary>
public sealed class SiteCredential
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string AuthType { get; set; } = "Basic"; // Basic, Digest, NTLM
}

/// <summary>
/// Web sitelerine ait kimlik doğrulama bilgilerini (Basic, Digest, NTLM) yöneten ve
/// DPAPI ile güvenli olarak depolayan servis.
/// </summary>
public sealed class SiteCredentialManager
{
    private const string SettingsKey = "Network.SiteCredentials.Json";
    private readonly ConcurrentDictionary<string, SiteCredential> _credentials = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<SiteCredentialManager> _logger;

    public SiteCredentialManager(ILogger<SiteCredentialManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Bir alan adı için kimlik doğrulama bilgisi ekler veya günceller.
    /// </summary>
    public void SetCredential(string host, string username, string password, string? domain = null, string authType = "Basic")
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Alan adı boş olamaz.", nameof(host));

        var cleanHost = NormalizeHost(host);
        var cred = new SiteCredential
        {
            Host = cleanHost,
            Username = username,
            Password = password,
            Domain = domain,
            AuthType = authType
        };

        _credentials[cleanHost] = cred;
        _logger.LogDebug("Site kimlik bilgisi güncellendi: {Host} ({AuthType})", cleanHost, authType);
    }

    /// <summary>
    /// Kayıtlı kimlik bilgisini siler.
    /// </summary>
    public bool RemoveCredential(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        return _credentials.TryRemove(NormalizeHost(host), out _);
    }

    /// <summary>
    /// Belirtilen host için kimlik bilgisini getirir.
    /// </summary>
    public SiteCredential? GetCredential(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var clean = NormalizeHost(host);
        if (_credentials.TryGetValue(clean, out var cred)) return cred;

        // Üst domain araması (ör. sub.example.com -> example.com)
        foreach (var pair in _credentials)
        {
            if (clean.EndsWith("." + pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Tüm kayıtlı kimlik bilgilerini döner.
    /// </summary>
    public IReadOnlyList<SiteCredential> GetAllCredentials()
    {
        return _credentials.Values.ToList();
    }

    /// <summary>
    /// Hedef URI için eşleşen bir kimlik bilgisi varsa verilen CredentialCache'e ekler.
    /// </summary>
    public void PopulateCredentialCache(CredentialCache cache, Uri uri)
    {
        var cred = GetCredential(uri.Host);
        if (cred == null) return;

        NetworkCredential netCred = !string.IsNullOrWhiteSpace(cred.Domain)
            ? new NetworkCredential(cred.Username, cred.Password, cred.Domain)
            : new NetworkCredential(cred.Username, cred.Password);

        var authTypes = cred.AuthType.Equals("Any", StringComparison.OrdinalIgnoreCase)
            ? new[] { "Basic", "Digest", "NTLM" }
            : new[] { cred.AuthType };

        foreach (var at in authTypes)
        {
            cache.Add(uri, at, netCred);
        }
    }

    /// <summary>
    /// Hedef URI için kayıtlı Basic kimlik doğrulaması varsa peşin Authorization başlık değerini döner (ör. "Basic dXNlcjpwYXNz").
    /// </summary>
    public string? GetBasicAuthHeaderValue(Uri uri)
    {
        var cred = GetCredential(uri.Host);
        if (cred == null) return null;

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cred.Username}:{cred.Password}"));
        return $"Basic {token}";
    }

    /// <summary>
    /// Kimlik bilgilerini veritabanındaki DPAPI ile şifrelenen ayarlardan yükler.
    /// </summary>
    public void LoadFromRepository(DownloadRepository repository)
    {
        try
        {
            var json = repository.GetSetting(SettingsKey);
            if (string.IsNullOrWhiteSpace(json)) return;

            var list = JsonSerializer.Deserialize<List<SiteCredential>>(json);
            if (list != null)
            {
                foreach (var item in list)
                {
                    if (!string.IsNullOrWhiteSpace(item.Host))
                    {
                        _credentials[NormalizeHost(item.Host)] = item;
                    }
                }
                _logger.LogInformation("{Count} adet site kimlik bilgisi depodan yüklendi.", list.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Site kimlik bilgileri depodan yüklenemedi.");
        }
    }

    /// <summary>
    /// Kimlik bilgilerini DPAPI ile şifrelenecek şekilde veritabanına kaydeder.
    /// </summary>
    public void SaveToRepository(DownloadRepository repository)
    {
        try
        {
            var list = _credentials.Values.ToList();
            var json = JsonSerializer.Serialize(list);
            repository.SetSetting(SettingsKey, json);
            _logger.LogDebug("Site kimlik bilgileri depoya kaydedildi.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Site kimlik bilgileri depoya kaydedilemedi.");
        }
    }

    private static string NormalizeHost(string host)
    {
        var clean = host.Trim();
        if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(clean, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        return clean.TrimEnd('/');
    }
}

