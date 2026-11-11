using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FDownloader.App.Utilities;
using Velopack;
using Velopack.Sources;

namespace FDownloader.App.Services;

/// <summary>
/// GitHub Releases üzerinden çekilen sürüm ve değişiklik bilgisi.
/// </summary>
public record GitHubReleaseInfo(string TagName, string Name, string Body, DateTime? PublishedAt, string? HtmlUrl);

/// <summary>
/// Uygulama güncellemelerini GitHub Releases üzerinden kontrol eden ve uygulayan servis.
/// </summary>
public sealed class UpdateService
{
    public const string DefaultGitHubRepoUrl = "https://github.com/erbayfaruk61/FDownloader";
    private const string GitHubApiLatestReleaseUrl = "https://api.github.com/repos/erbayfaruk61/FDownloader/releases/latest";

    private readonly ILogger<UpdateService> _logger;
    private readonly UpdateManager _updateManager;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FDownloader", "2.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public bool IsInstalledApp => _updateManager.IsInstalled;
    public string? CurrentVersion => _updateManager.CurrentVersion?.ToFullString();

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        var source = new GithubSource(DefaultGitHubRepoUrl, string.Empty, false);
        _updateManager = new UpdateManager(source);
    }

    /// <summary>
    /// Yeni bir güncelleme olup olmadığını kontrol eder.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!_updateManager.IsInstalled)
        {
            _logger.LogInformation("Uygulama Velopack ile kurulu olmadığı için geliştirici modunda güncelleme kontrolü atlanıyor.");
            return null;
        }

        try
        {
            return await _updateManager.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güncelleme kontrolü sırasında hata oluştu.");
            return null;
        }
    }

    /// <summary>
    /// GitHub API üzerinden doğrudan en son sürüm bilgilerini ve sürüm notlarını (changelog) çeker.
    /// </summary>
    public async Task<GitHubReleaseInfo?> FetchLatestGitHubReleaseAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(GitHubApiLatestReleaseUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub Releases API yanıt vermedi: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            DateTime? publishedAt = root.TryGetProperty("published_at", out var pubEl) && pubEl.TryGetDateTime(out var dt) ? dt : null;

            return new GitHubReleaseInfo(tagName, name, body, publishedAt, htmlUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub sürüm notları çekilemedi.");
            return null;
        }
    }

    /// <summary>
    /// Belirli bir sürüm etiketinin veya en son sürümün sürüm notlarını (değişikliklerini) çeker.
    /// </summary>
    public async Task<string> FetchReleaseNotesAsync(string? targetTag = null, CancellationToken ct = default)
    {
        string tagToUse = targetTag ?? "";
        try
        {
            if (!string.IsNullOrWhiteSpace(targetTag))
            {
                string cleanTag = targetTag.Trim();
                string tagUrl = $"https://api.github.com/repos/erbayfaruk61/FDownloader/releases/tags/{cleanTag}";
                using var tagResponse = await _httpClient.GetAsync(tagUrl, ct);
                if (tagResponse.IsSuccessStatusCode)
                {
                    var json = await tagResponse.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("body", out var bodyEl))
                    {
                        var body = bodyEl.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(body) && !body.StartsWith("**Full Changelog**", StringComparison.OrdinalIgnoreCase))
                        {
                            return body;
                        }
                    }
                }
            }

            var latest = await FetchLatestGitHubReleaseAsync(ct);
            if (string.IsNullOrWhiteSpace(tagToUse) && !string.IsNullOrWhiteSpace(latest?.TagName))
            {
                tagToUse = latest.TagName;
            }

            if (!string.IsNullOrWhiteSpace(latest?.Body) && !latest.Body.Trim().StartsWith("**Full Changelog**", StringComparison.OrdinalIgnoreCase))
            {
                return latest.Body.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sürüm notları GitHub'dan alınamadı.");
        }

        if (!string.IsNullOrWhiteSpace(tagToUse))
        {
            return GetFallbackNotesForVersion(tagToUse);
        }

        return "Bu sürüm için detaylı değişiklik listesi GitHub üzerinde henüz yayınlanmadı veya internet bağlantısı sağlanamadı.\n\nDeğişiklikleri incelemek için GitHub deposunu ziyaret edebilirsiniz: " + DefaultGitHubRepoUrl;
    }

    private static string GetFallbackNotesForVersion(string tag)
    {
        var clean = tag.Trim().TrimStart('v');
        if (clean.StartsWith("2.0.2", StringComparison.OrdinalIgnoreCase))
        {
            return "### 🚀 FDownloader v2.0.2 Yenilikleri:\n\n" +
                   "• 🎬 Facebook & Reels İndirme Desteği: Facebook videoları, Reels ve watch bağlantıları için yt-dlp destekli akıllı medya tanıma ve indirme altyapısı tamamlandı.\n" +
                   "• 🔄 Tek Tıkla Canlı Eklenti Güncelleme: Tarayıcı eklentisi güncellendiğinde açılan bildirim penceresinden tek tıkla tarayıcıdaki eklentiyi diskten anında yenileme imkânı.\n" +
                   "• 🧩 Otomatik Eklenti Sürüm Denetimi: Program açıldığında tarayıcı eklentisi eski ise kullanıcıya otomatik bildirim sunulması ve güncellemeyi kolaylaştıran yönlendirme.\n" +
                   "• 🛡️ Kararlılık & İndirme İyileştirmeleri: Medya akış analizindeki yönlendirme ve bağlantı hataları giderildi.";
        }
        if (clean.StartsWith("2.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return "### 🚀 FDownloader v2.0.1 Yenilikleri:\n\n" +
                   "• 🌐 Facebook, Instagram ve Reels Desteği: Katmanları delen akıllı video yakalama motoru ile overlay engelleri aşıldı ve indirme butonu eklendi.\n" +
                   "• 🔄 Tek Tıkla Canlı Eklenti Yenileme: Ayarlar menüsünden tarayıcıyı kapatmaya gerek kalmadan eklentiyi diskten anında güncelleme imkanı.\n" +
                   "• 🎨 Çok Çözünürlüklü Resmi Logolar: 16x16, 32x32, 48x48 ve 128x128 boyutlarında orijinal FDownloader ikonları entegre edildi.\n" +
                   "• 🛡️ Güvenlik ve Kararlılık: Çerez aktarımı ve köprü bağlantı kararlılığı artırıldı.";
        }
        if (clean.StartsWith("2.0.0", StringComparison.OrdinalIgnoreCase))
        {
            return "### 🚀 FDownloader v2.0.0 Yenilikleri:\n\n" +
                   "• 🔒 Şifreli arşiv desteği (Şifre hafızası, tek tıkla silme ve otomatik çıkarma)\n" +
                   "• ⚡ Çok parçalı indirmede erken kopma ve 429 Rate Limit stabilizasyonu\n" +
                   "• 🥞 Bildirim (Toast) yığılması: Bildirimlerin dikey olarak sağ altta yığılması\n" +
                   "• 🛡️ Aynı kök domain/subdomain için güvenli çerez aktarımı\n" +
                   "• 💤 Aktif indirmede Windows uyku modunu engelleme (PowerManagement)\n" +
                   "• 🔐 Dosya bütünlüğü doğrulama (MD5, SHA-1, SHA-256 Checksum)";
        }
        return "• Yeni özellikler ve hata düzeltmeleri yapıldı.\n• Kararlılık ve performans iyileştirmeleri sağlandı.";
    }

    /// <summary>
    /// Mevcut güncellemeyi indirir ve ilerleme durumunu bildirir.
    /// </summary>
    public async Task DownloadUpdatesAsync(UpdateInfo updateInfo, Action<int>? progressHandler = null)
    {
        try
        {
            await _updateManager.DownloadUpdatesAsync(updateInfo, progressHandler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güncelleme paketi indirilemedi.");
            throw;
        }
    }

    /// <summary>
    /// İndirilen güncellemeyi uygular ve uygulamayı yeniden başlatır.
    /// </summary>
    public void ApplyUpdatesAndRestart(UpdateInfo updateInfo)
    {
        try
        {
            // Eski versiyondan kalan masaüstü simgesi ve kısayolunu güncelle
            DesktopShortcutHelper.UpdateDesktopShortcuts();
            _updateManager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güncelleme uygulanırken hata oluştu.");
            throw;
        }
    }
}
