using MonoTorrent;
using MonoTorrent.Client;
using Microsoft.Extensions.Logging;
using FDownloader.Core.Domain;
using FDownloader.Core.Events;
using FDownloader.Core.Utilities;

namespace FDownloader.Core.Torrent;

/// <summary>
/// BitTorrent protokolü üzerinden dosya/klasör indirmelerini yönetir.
/// MonoTorrent kütüphanesini kullanarak gelişmiş torrent yönetimi sağlar.
/// </summary>
public sealed class TorrentService : IAsyncDisposable
{
    private readonly ClientEngine _engine;
    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<TorrentService> _logger;
    private readonly Dictionary<long, TorrentManager> _activeManagers = new();
    private readonly Dictionary<long, TorrentDetails> _lastKnownDetails = new();
    private readonly HashSet<long> _metadataNotifiedTasks = new();
    private readonly string _downloadDirectory;
    private readonly string _cacheDirectory;
    private readonly CancellationTokenSource _cts = new();

    public TorrentService(
        DownloadEventBus eventBus,
        ILogger<TorrentService> logger,
        string? downloadDirectory = null)
    {
        _eventBus = eventBus;
        _logger = logger;
        _downloadDirectory = downloadDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        _cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "torrent_cache");
        Directory.CreateDirectory(_cacheDirectory);

        var engineSettings = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AutoSaveLoadFastResume = true,
            CacheDirectory = _cacheDirectory
        }.ToSettings();

        _engine = new ClientEngine(engineSettings);
        _ = MonitorTorrentsLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Magnet linkinden veya .torrent dosyasından torrent görevi ekler ve başlatır.
    /// </summary>
    public async Task<TorrentManager> AddAndStartTorrentAsync(
        long taskId,
        string sourceUrlOrPath,
        string? saveDirectory = null,
        CancellationToken cancellationToken = default)
    {
        return await SetupTorrentInternalAsync(taskId, sourceUrlOrPath, saveDirectory, startImmediately: true, cancellationToken);
    }

    /// <summary>
    /// Program açıldığında veya görev duraklatılmışken torrenti motora restore eder.
    /// </summary>
    public async Task<TorrentManager?> RestoreTorrentTaskAsync(
        long taskId,
        string sourceUrlOrPath,
        string? saveDirectory = null,
        bool startImmediately = false)
    {
        try
        {
            return await SetupTorrentInternalAsync(taskId, sourceUrlOrPath, saveDirectory, startImmediately, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torrent görevi #{TaskId} geri yüklenemedi", taskId);
            return null;
        }
    }

    private async Task<TorrentManager> SetupTorrentInternalAsync(
        long taskId,
        string sourceUrlOrPath,
        string? saveDirectory,
        bool startImmediately,
        CancellationToken cancellationToken)
    {
        saveDirectory ??= _downloadDirectory;
        Directory.CreateDirectory(saveDirectory);

        lock (_activeManagers)
        {
            if (_activeManagers.TryGetValue(taskId, out var existing))
            {
                if (startImmediately && existing.State == TorrentState.Paused)
                {
                    _ = existing.StartAsync();
                }
                return existing;
            }
        }

        string cleanSource = sourceUrlOrPath.Trim().Trim('\"', '\'');
        if (cleanSource.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                cleanSource = new Uri(cleanSource).LocalPath;
            }
            catch { }
        }

        var localCachePath = Path.Combine(_cacheDirectory, $"{taskId}.torrent");
        TorrentManager manager;

        if (File.Exists(localCachePath))
        {
            var torrent = await MonoTorrent.Torrent.LoadAsync(localCachePath);
            manager = await _engine.AddAsync(torrent, saveDirectory);
        }
        else if (cleanSource.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            var magnetLink = MagnetLink.Parse(cleanSource);
            manager = await _engine.AddAsync(magnetLink, saveDirectory);
        }
        else if (File.Exists(cleanSource))
        {
            var torrent = await MonoTorrent.Torrent.LoadAsync(cleanSource);
            try { File.Copy(cleanSource, localCachePath, overwrite: true); } catch { }
            manager = await _engine.AddAsync(torrent, saveDirectory);
        }
        else if (cleanSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 cleanSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Uzak .torrent dosyası indiriliyor: {Url}", cleanSource);
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Network.NetworkConstants.DefaultUserAgent);

            var torrentBytes = await httpClient.GetByteArrayAsync(cleanSource, cancellationToken);
            var torrent = await MonoTorrent.Torrent.LoadAsync(torrentBytes);

            try
            {
                await File.WriteAllBytesAsync(localCachePath, torrentBytes, cancellationToken);
            }
            catch { }

            manager = await _engine.AddAsync(torrent, saveDirectory);
        }
        else
        {
            throw new ArgumentException("Geçerli bir magnet linki, indirilebilir .torrent URL'si veya var olan bir .torrent dosya yolu gereklidir.", nameof(sourceUrlOrPath));
        }

        lock (_activeManagers)
        {
            _activeManagers[taskId] = manager;
        }

        if (manager.Torrent != null && !_metadataNotifiedTasks.Contains(taskId))
        {
            NotifyMetadataLoaded(taskId, manager.Torrent, localCachePath);
        }

        if (startImmediately)
        {
            await manager.StartAsync();
            _eventBus.Publish(new TaskStartedEvent(taskId));
            _logger.LogInformation("Torrent başlatıldı: Task #{TaskId}, İsim: {Name}", taskId, manager.Torrent?.Name ?? "Magnet");
        }
        else
        {
            _logger.LogInformation("Torrent yüklendi (beklemede): Task #{TaskId}", taskId);
        }

        return manager;
    }

    private void NotifyMetadataLoaded(long taskId, MonoTorrent.Torrent torrent, string localCachePath)
    {
        _metadataNotifiedTasks.Add(taskId);
        _logger.LogInformation("Torrent meta verisi hazır: Task #{TaskId}, {Name}, {Size} Byte", taskId, torrent.Name, torrent.Size);
        _eventBus.Publish(new TorrentMetadataLoadedEvent(taskId, torrent.Name, torrent.Size, localCachePath));
    }

    /// <summary>
    /// Torrent indirmesini duraklatır.
    /// </summary>
    public async Task PauseTorrentAsync(long taskId)
    {
        TorrentManager? manager;
        lock (_activeManagers)
        {
            _activeManagers.TryGetValue(taskId, out manager);
        }

        if (manager != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var pauseTask = manager.PauseAsync();
                await System.Threading.Tasks.Task.WhenAny(pauseTask, System.Threading.Tasks.Task.Delay(3000, cts.Token));
                _eventBus.Publish(new TaskPausedEvent(taskId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Torrent duraklatılırken hata alındı: Görev #{TaskId}", taskId);
            }
        }
    }

    /// <summary>
    /// Torrent indirmesini devam ettirir.
    /// </summary>
    public async Task ResumeTorrentAsync(long taskId)
    {
        TorrentManager? manager;
        lock (_activeManagers)
        {
            _activeManagers.TryGetValue(taskId, out manager);
        }

        if (manager != null)
        {
            try
            {
                await manager.StartAsync();
                _eventBus.Publish(new TaskResumedEvent(taskId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Torrent devam ettirilirken hata alındı: Görev #{TaskId}", taskId);
            }
        }
    }

    /// <summary>
    /// Torrent indirmesini durdurur ve motor listesinden çıkarır.
    /// deleteFiles true ise indirilen dosyaları ve .torrent önbelleğini güvenli bir şekilde siler.
    /// </summary>
    public async Task RemoveTorrentAsync(long taskId, bool deleteFiles = false)
    {
        TorrentManager? manager;
        lock (_activeManagers)
        {
            _activeManagers.Remove(taskId, out manager);
            _lastKnownDetails.Remove(taskId);
            _metadataNotifiedTasks.Remove(taskId);
        }

        if (manager != null)
        {
            var savePath = manager.SavePath;
            var torrentName = manager.Torrent?.Name;
            var filePaths = new List<string>();

            if (manager.Files != null)
            {
                foreach (var f in manager.Files)
                {
                    try
                    {
                        var full = Path.GetFullPath(Path.Combine(savePath, f.Path));
                        filePaths.Add(full);
                    }
                    catch { }
                }
            }

            try
            {
                // Torrent'i 3 saniye içinde nazikçe durdur
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var stopTask = manager.StopAsync();
                await System.Threading.Tasks.Task.WhenAny(stopTask, System.Threading.Tasks.Task.Delay(3000, cts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Torrent durdurulurken istisna alındı: Görev #{TaskId}", taskId);
            }

            try
            {
                await _engine.RemoveAsync(manager);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Torrent motordan kaldırılırken hata alındı: Görev #{TaskId}", taskId);
            }

            if (deleteFiles)
            {
                // 1. Torrent'e ait dosyaları sil
                foreach (var fp in filePaths)
                {
                    PathSafetyHelper.SafeDeleteFile(fp);
                }

                // 2. Çoklu dosyalı torrent ise ve SavePath altında özel bir klasör açılmışsa,
                // sadece bu torrent'e ait alt klasörü sil (Downloads ana klasörüne asla dokunma!)
                if (!string.IsNullOrWhiteSpace(torrentName) && !string.IsNullOrWhiteSpace(savePath))
                {
                    var specificTorrentFolder = Path.Combine(savePath, torrentName);
                    if (Directory.Exists(specificTorrentFolder))
                    {
                        PathSafetyHelper.SafeDeleteDirectory(specificTorrentFolder, savePath);
                    }
                }
            }
        }

        // 3. Yerel .torrent önbelleğini temizle
        var localCachePath = Path.Combine(_cacheDirectory, $"{taskId}.torrent");
        PathSafetyHelper.SafeDeleteFile(localCachePath);

        _eventBus.Publish(new TaskCancelledEvent(taskId));
    }

    /// <summary>
    /// Torrent içerisindeki tek bir dosyanın indirme önceliğini ayarlar (İndir / İndirme).
    /// </summary>
    public async Task SetFilePriorityAsync(long taskId, int fileIndex, bool download)
    {
        TorrentManager? manager;
        lock (_activeManagers)
        {
            _activeManagers.TryGetValue(taskId, out manager);
        }

        if (manager != null && manager.Files != null && fileIndex >= 0 && fileIndex < manager.Files.Count)
        {
            var file = manager.Files[fileIndex];
            var priority = download ? Priority.Normal : Priority.DoNotDownload;
            await manager.SetFilePriorityAsync(file, priority);
            _logger.LogDebug("Torrent dosya önceliği güncellendi: Task #{TaskId}, Dosya #{Index} -> {Priority}", taskId, fileIndex, priority);
        }
    }

    /// <summary>
    /// Torrent içerisindeki tüm dosyaların indirme durumunu topluca ayarlar.
    /// </summary>
    public async Task SetAllFilesPriorityAsync(long taskId, bool downloadAll)
    {
        TorrentManager? manager;
        lock (_activeManagers)
        {
            _activeManagers.TryGetValue(taskId, out manager);
        }

        if (manager != null && manager.Files != null)
        {
            var priority = downloadAll ? Priority.Normal : Priority.DoNotDownload;
            foreach (var file in manager.Files)
            {
                await manager.SetFilePriorityAsync(file, priority);
            }
        }
    }

    /// <summary>
    /// İndirme başlatılmadan önce bir .torrent dosyasının veya URL'sinin içindeki dosyaları listeler.
    /// qBittorrent stili dosya seçimi için kullanılır.
    /// </summary>
    public async Task<List<TorrentFileInfo>> InspectTorrentFilesAsync(string sourceUrlOrPath)
    {
        var result = new List<TorrentFileInfo>();
        try
        {
            var clean = sourceUrlOrPath.Trim().Trim('\"', '\'');
            MonoTorrent.Torrent? torrent = null;

            if (File.Exists(clean))
            {
                torrent = await MonoTorrent.Torrent.LoadAsync(clean);
            }
            else if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var bytes = await httpClient.GetByteArrayAsync(clean);
                torrent = await MonoTorrent.Torrent.LoadAsync(bytes);
            }

            if (torrent != null && torrent.Files != null)
            {
                for (int i = 0; i < torrent.Files.Count; i++)
                {
                    var f = torrent.Files[i];
                    result.Add(new TorrentFileInfo
                    {
                        Index = i,
                        Path = f.Path,
                        Length = f.Length,
                        BytesDownloaded = 0,
                        Priority = "Normal",
                        IsSelected = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torrent dosyaları ön inceleme sırasında okunamadı: {Source}", sourceUrlOrPath);
        }
        return result;
    }

    /// <summary>
    /// UI için anlık veya önbellekten BitTorrent detaylarını döner.
    /// </summary>
    public TorrentDetails? GetTorrentDetails(long taskId)
    {
        lock (_activeManagers)
        {
            if (_lastKnownDetails.TryGetValue(taskId, out var cached))
            {
                return cached;
            }
            if (_activeManagers.TryGetValue(taskId, out var manager))
            {
                return BuildTorrentDetailsSync(manager);
            }
        }
        return null;
    }

    private TorrentDetails BuildTorrentDetailsSync(TorrentManager manager)
    {
        var files = new List<TorrentFileInfo>();
        if (manager.Files != null)
        {
            for (int i = 0; i < manager.Files.Count; i++)
            {
                var f = manager.Files[i];
                files.Add(new TorrentFileInfo
                {
                    Index = i,
                    Path = f.Path,
                    Length = f.Length,
                    BytesDownloaded = f.BytesDownloaded(),
                    Priority = f.Priority.ToString(),
                    IsSelected = f.Priority != Priority.DoNotDownload
                });
            }
        }

        var trackers = new List<TorrentTrackerInfo>();
        try
        {
            if (manager.TrackerManager != null)
            {
                foreach (var tier in manager.TrackerManager.Tiers)
                {
                    foreach (var tracker in tier.Trackers)
                    {
                        trackers.Add(new TorrentTrackerInfo(
                            Uri: tracker.Uri.ToString(),
                            Status: tracker.Status.ToString(),
                            Seeds: 0,
                            Leeches: 0
                        ));
                    }
                }
            }
        }
        catch { }

        long totalSize = manager.Torrent?.Size ?? 0;
        long downloaded = (long)(totalSize * (manager.Progress / 100.0));
        long uploaded = manager.Monitor.DataBytesSent;
        double ratio = downloaded > 0 ? (double)uploaded / downloaded : 0.0;

        TimeSpan? eta = null;
        if (manager.Monitor.DownloadRate > 0 && totalSize > downloaded)
        {
            eta = TimeSpan.FromSeconds((totalSize - downloaded) / (double)manager.Monitor.DownloadRate);
        }

        return new TorrentDetails
        {
            Name = manager.Torrent?.Name ?? "Meta verisi indiriliyor...",
            ProgressPercent = manager.Progress,
            DownloadSpeed = manager.Monitor.DownloadRate,
            UploadSpeed = manager.Monitor.UploadRate,
            TotalBytes = totalSize,
            DownloadedBytes = downloaded,
            UploadedBytes = uploaded,
            ShareRatio = Math.Round(ratio, 2),
            OpenConnections = manager.OpenConnections,
            Seeds = manager.Peers.Seeds,
            Leeches = manager.Peers.Leechs,
            State = TranslateState(manager.State),
            InfoHash = manager.InfoHashes.V1OrV2.ToHex(),
            SavePath = manager.SavePath,
            Eta = eta,
            Files = files,
            Peers = new List<TorrentPeerInfo>(),
            Trackers = trackers
        };
    }

    private static string TranslateState(TorrentState state) => state switch
    {
        TorrentState.Stopped => "Durduruldu",
        TorrentState.Paused => "Duraklatıldı",
        TorrentState.Starting => "Başlatılıyor",
        TorrentState.Downloading => "İndiriliyor",
        TorrentState.Seeding => "Gönderiliyor (Seed)",
        TorrentState.Hashing => "Dosyalar Doğrulanıyor",
        TorrentState.Metadata => "Meta Verisi Alınıyor",
        TorrentState.Error => "Hata",
        _ => state.ToString()
    };

    private async Task MonitorTorrentsLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(500, cancellationToken);

                List<KeyValuePair<long, TorrentManager>> list;
                lock (_activeManagers)
                {
                    list = _activeManagers.ToList();
                }

                foreach (var (taskId, manager) in list)
                {
                    // Metadata kontrolü: manager.Torrent dolduysa ve henüz bildirilmediyse
                    if (manager.Torrent != null && !_metadataNotifiedTasks.Contains(taskId))
                    {
                        var localCachePath = Path.Combine(_cacheDirectory, $"{taskId}.torrent");
                        try
                        {
                            if (!File.Exists(localCachePath))
                            {
                                // Metadata dosyasını engine fastresume önbelleğinden veya mevcut yoldan koru
                                _ = manager.SaveFastResumeAsync();
                            }
                        }
                        catch { }
                        NotifyMetadataLoaded(taskId, manager.Torrent, localCachePath);
                    }

                    // Ayrıntıları arka planda güncelle (Eşler ve Dosyalar dahil)
                    var details = await BuildDetailedStatsAsync(manager);
                    lock (_activeManagers)
                    {
                        _lastKnownDetails[taskId] = details;
                    }

                    var totalSize = manager.Torrent?.Size ?? 0;
                    var downloaded = (long)(totalSize * (manager.Progress / 100.0));
                    var speed = (double)manager.Monitor.DownloadRate;

                    TimeSpan? eta = null;
                    if (speed > 0 && totalSize > downloaded)
                    {
                        eta = TimeSpan.FromSeconds((totalSize - downloaded) / speed);
                    }

                    _eventBus.Publish(new ProgressUpdatedEvent(
                        taskId,
                        downloaded,
                        totalSize,
                        manager.Progress,
                        speed,
                        eta,
                        Array.Empty<SegmentProgressInfo>()
                    ));

                    if (manager.Complete)
                    {
                        string finalTorrentPath = manager.SavePath;
                        if (manager.Torrent != null && !string.IsNullOrWhiteSpace(manager.Torrent.Name))
                        {
                            finalTorrentPath = Path.Combine(manager.SavePath, manager.Torrent.Name);
                        }
                        else if (manager.Files != null && manager.Files.Count == 1)
                        {
                            finalTorrentPath = Path.Combine(manager.SavePath, manager.Files[0].Path);
                        }

                        _eventBus.Publish(new TaskCompletedEvent(
                            taskId,
                            finalTorrentPath,
                            totalSize,
                            TimeSpan.Zero
                        ));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Torrent izleme döngüsünde hata");
            }
        }
    }

    private async Task<TorrentDetails> BuildDetailedStatsAsync(TorrentManager manager)
    {
        var details = BuildTorrentDetailsSync(manager);

        // Asenkron eş listesini topla
        try
        {
            var peerList = await manager.GetPeersAsync();
            if (peerList != null)
            {
                var peers = new List<TorrentPeerInfo>();
                foreach (var p in peerList)
                {
                    var clientName = p.ClientApp.ToString();
                    peers.Add(new TorrentPeerInfo(
                        Address: p.Uri?.ToString() ?? "Bilinmiyor",
                        Client: string.IsNullOrWhiteSpace(clientName) ? "Bilinmeyen İstemci" : clientName,
                        DownloadSpeed: p.Monitor.DownloadRate,
                        UploadSpeed: p.Monitor.UploadRate,
                        ProgressPercent: 0
                    ));
                }
                details.Peers = peers;
            }
        }
        catch { }

        return details;
    }

    public void SetRateLimits(long downloadBytesPerSec, long uploadBytesPerSec)
    {
        try
        {
            var builder = new EngineSettingsBuilder(_engine.Settings)
            {
                MaximumDownloadRate = downloadBytesPerSec > 0 ? (int)Math.Min(int.MaxValue, downloadBytesPerSec) : 0,
                MaximumUploadRate = uploadBytesPerSec > 0 ? (int)Math.Min(int.MaxValue, uploadBytesPerSec) : 0
            };
            _ = _engine.UpdateSettingsAsync(builder.ToSettings());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torrent hız sınırı güncellenemedi");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        await _engine.StopAllAsync();
        _engine.Dispose();
    }
}

public class TorrentDetails
{
    public string Name { get; set; } = string.Empty;
    public double ProgressPercent { get; set; }
    public long DownloadSpeed { get; set; }
    public long UploadSpeed { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public long UploadedBytes { get; set; }
    public double ShareRatio { get; set; }
    public int OpenConnections { get; set; }
    public int Seeds { get; set; }
    public int Leeches { get; set; }
    public string State { get; set; } = string.Empty;
    public string InfoHash { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public TimeSpan? Eta { get; set; }
    public List<TorrentFileInfo> Files { get; set; } = new();
    public List<TorrentPeerInfo> Peers { get; set; } = new();
    public List<TorrentTrackerInfo> Trackers { get; set; } = new();
}

public class TorrentFileInfo
{
    public int Index { get; set; }
    public string Path { get; set; } = string.Empty;
    public long Length { get; set; }
    public long BytesDownloaded { get; set; }
    public double ProgressPercent => Length > 0 ? Math.Min(100.0, BytesDownloaded * 100.0 / Length) : 0;
    public string Priority { get; set; } = "Normal";
    public bool IsSelected { get; set; } = true;

    public string LengthDisplay
    {
        get
        {
            if (Length < 1024) return $"{Length} B";
            if (Length < 1024 * 1024) return $"{Length / 1024.0:F1} KB";
            if (Length < 1024 * 1024 * 1024) return $"{Length / (1024.0 * 1024):F1} MB";
            return $"{Length / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}

public record TorrentPeerInfo(string Address, string Client, long DownloadSpeed, long UploadSpeed, double ProgressPercent);
public record TorrentTrackerInfo(string Uri, string Status, int Seeds, int Leeches);
