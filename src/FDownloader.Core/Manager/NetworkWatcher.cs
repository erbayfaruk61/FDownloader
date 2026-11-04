using System.Net.NetworkInformation;
using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Manager;

/// <summary>
/// Ağ bağlantısı durumunu izler, kesintide indirmeleri duraklatır ve ağ geri geldiğinde otomatik devam ettirir.
/// </summary>
public sealed class NetworkWatcher : IDisposable
{
    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<NetworkWatcher> _logger;
    private readonly Func<Task<List<long>>> _onNetworkLost;
    private readonly Func<List<long>, Task> _onNetworkRestored;

    private readonly List<long> _pausedTaskIds = new();
    private readonly object _lock = new();
    private bool _isNetworkAvailable;

    public bool IsNetworkAvailable => _isNetworkAvailable;

    public NetworkWatcher(
        DownloadEventBus eventBus,
        ILogger<NetworkWatcher> logger,
        Func<Task<List<long>>> onNetworkLost,
        Func<List<long>, Task> onNetworkRestored)
    {
        _eventBus = eventBus;
        _logger = logger;
        _onNetworkLost = onNetworkLost;
        _onNetworkRestored = onNetworkRestored;

        _isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
        NetworkChange.NetworkAvailabilityChanged += HandleNetworkAvailabilityChanged;
    }

    private void HandleNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _ = HandleAvailabilityChangeAsync(e.IsAvailable);
    }

    /// <summary>
    /// Ağ durumu değişikliğini işler. Test edilebilirlik için public yapılmıştır.
    /// </summary>
    public async Task HandleAvailabilityChangeAsync(bool isAvailable)
    {
        lock (_lock)
        {
            if (_isNetworkAvailable == isAvailable) return;
            _isNetworkAvailable = isAvailable;
        }

        if (!isAvailable)
        {
            _logger.LogWarning("Ağ bağlantısı koptu! Devam eden indirmeler duraklatılıyor...");
            _eventBus.Publish(new NetworkLostEvent());

            var affectedIds = await _onNetworkLost();
            lock (_lock)
            {
                _pausedTaskIds.Clear();
                _pausedTaskIds.AddRange(affectedIds);
            }
        }
        else
        {
            _logger.LogInformation("Ağ bağlantısı yeniden sağlandı! Kesintiye uğrayan indirmeler devam ettiriliyor...");
            _eventBus.Publish(new NetworkRestoredEvent());

            List<long> toResume;
            lock (_lock)
            {
                toResume = _pausedTaskIds.ToList();
                _pausedTaskIds.Clear();
            }

            if (toResume.Count > 0)
            {
                await _onNetworkRestored(toResume);
            }
        }
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= HandleNetworkAvailabilityChanged;
    }
}

