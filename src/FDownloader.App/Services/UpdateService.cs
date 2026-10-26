using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace FDownloader.App.Services;

/// <summary>
/// Uygulama güncellemelerini GitHub Releases üzerinden kontrol eden ve uygulayan servis.
/// </summary>
public sealed class UpdateService
{
    public const string DefaultGitHubRepoUrl = "https://github.com/erbayfaruk61/FDownloader";

    private readonly ILogger<UpdateService> _logger;
    private readonly UpdateManager _updateManager;

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
            _updateManager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Güncelleme uygulanırken hata oluştu.");
            throw;
        }
    }
}
