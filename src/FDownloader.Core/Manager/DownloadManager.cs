using FDownloader.Core.Batch;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine;
using FDownloader.Core.Events;
using FDownloader.Core.Network;
using FDownloader.Core.Storage;
using FDownloader.Core.Torrent;
using FDownloader.Core.Utilities;
using FDownloader.Core.Video;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Manager;

/// <summary>
/// Tüm HTTP, Torrent ve Medya indirmelerini yöneten merkezi orkestratör.
/// Görevlerin durumlarını saklar, kuyruğu yönetir ve alt motorlarla haberleşir.
/// </summary>
public sealed class DownloadManager : IAsyncDisposable
{
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly TorrentService _torrentService;
    private readonly YtDlpMediaService _mediaService;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DownloadManager> _logger;

    private readonly Dictionary<long, HttpDownloader> _httpDownloaders = new();
    private readonly Dictionary<long, CancellationTokenSource> _mediaCtsList = new();
    private readonly List<DownloadTask> _tasks = new();
    private readonly object _lock = new();

    public int MaxConcurrentDownloads { get; set; } = 4;
    public IReadOnlyList<DownloadTask> Tasks { get { lock (_lock) { return _tasks.ToList(); } } }
    public SpeedLimiter SpeedLimiter { get; } = new();
    public ArchiveExtractorService ArchiveExtractor { get; }
    public DownloadScheduler Scheduler { get; }
    public NetworkWatcher NetworkWatcher { get; }
    public SiteCredentialManager CredentialManager { get; }

    public string? GetSetting(string key) => _repository.GetSetting(key);
    public void SetSetting(string key, string value) => _repository.SetSetting(key, value);
    public Dictionary<string, string> GetAllSettings() => _repository.GetAllSettings();
    public void ClearAllSettings() => _repository.ClearAllSettings();
    public void ImportSettings(IDictionary<string, string> settings) => _repository.ImportSettings(settings);

    public DownloadManager(
        DownloadRepository repository,
        DownloadEventBus eventBus,
        TorrentService torrentService,
        YtDlpMediaService mediaService,
        HttpClient httpClient,
        ILoggerFactory loggerFactory,
        SiteCredentialManager? credentialManager = null)
    {
        _repository = repository;
        _eventBus = eventBus;
        _torrentService = torrentService;
        _mediaService = mediaService;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DownloadManager>();
        ArchiveExtractor = new ArchiveExtractorService(loggerFactory.CreateLogger<ArchiveExtractorService>());

        CredentialManager = credentialManager ?? new SiteCredentialManager(loggerFactory.CreateLogger<SiteCredentialManager>());
        CredentialManager.LoadFromRepository(_repository);

        Scheduler = new DownloadScheduler(
            eventBus,
            loggerFactory.CreateLogger<DownloadScheduler>(),
            onStartScheduled: async () => await StartAllTasksAsync(),
            onStopScheduled: async () => await PauseAllTasksAsync());

        NetworkWatcher = new NetworkWatcher(
            eventBus,
            loggerFactory.CreateLogger<NetworkWatcher>(),
            onNetworkLost: async () =>
            {
                List<long> activeIds;
                lock (_lock)
                {
                    activeIds = _tasks.Where(t => t.Status == DownloadStatus.Downloading).Select(t => t.Id).ToList();
                }
                foreach (var id in activeIds)
                {
                    await PauseTaskAsync(id);
                }
                return activeIds;
            },
            onNetworkRestored: async (taskIds) =>
            {
                foreach (var id in taskIds)
                {
                    await StartOrResumeTaskAsync(id);
                }
            });

        SubscribeToEvents();
        LoadExistingTasks();
        _ = _torrentService.ApplySettingsAsync(GetTorrentSettings());
        _ = ProcessQueueAsync();
    }

    private void SubscribeToEvents()
    {
        _eventBus.Subscribe<TaskRenamedEvent>(e =>
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == e.TaskId);
                if (task != null && !string.IsNullOrWhiteSpace(e.NewName))
                {
                    task.Name = e.NewName;
                    task.FileName = e.NewName;
                    _repository.UpdateTaskStatus(task);
                }
            }
        });

        _eventBus.Subscribe<TaskCompletedEvent>(e =>
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == e.TaskId);
                if (task != null)
                {
                    task.Status = DownloadStatus.Completed;
                    task.CompletedAt = DateTimeOffset.UtcNow;
                    if (!string.IsNullOrEmpty(e.FilePath))
                    {
                        var name = Path.GetFileName(e.FilePath);
                        if (!string.IsNullOrWhiteSpace(name)) task.FileName = name;

                        if (File.Exists(e.FilePath))
                        {
                            var fi = new FileInfo(e.FilePath);
                            task.TotalBytes = fi.Length;
                            task.DownloadedBytes = fi.Length;
                        }
                        else if (e.TotalBytes > 0)
                        {
                            task.TotalBytes = e.TotalBytes;
                            task.DownloadedBytes = e.TotalBytes;
                        }
                    }
                    task.LastErrorMessage = null;
                    _repository.UpdateTaskStatus(task);
                    _logger.LogInformation("Görev #{TaskId} tamamlandı ve SQLite veritabanına kaydedildi: {File}", task.Id, task.FileName);
                }
            }

            lock (_httpDownloaders)
            {
                if (_httpDownloaders.Remove(e.TaskId, out var dl))
                {
                    dl.Dispose();
                }
            }
            lock (_mediaCtsList)
            {
                if (_mediaCtsList.Remove(e.TaskId, out var mCts))
                {
                    try { mCts.Cancel(); } catch { }
                    mCts.Dispose();
                }
            }

            _ = ProcessQueueAsync();
            Scheduler.NotifyAllDownloadsCompleted();
        });

        _eventBus.Subscribe<TaskFailedEvent>(e =>
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == e.TaskId);
                if (task != null)
                {
                    task.Status = DownloadStatus.Failed;
                    task.LastErrorMessage = e.Reason;
                    _repository.UpdateTaskStatus(task);
                }
            }
            _ = ProcessQueueAsync();
        });

        _eventBus.Subscribe<TaskPausedEvent>(e =>
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == e.TaskId);
                if (task != null)
                {
                    task.Status = DownloadStatus.Paused;
                    _repository.UpdateTaskStatus(task);
                }
            }
            _ = ProcessQueueAsync();
        });

        _eventBus.Subscribe<TaskCancelledEvent>(e =>
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == e.TaskId);
                if (task != null)
                {
                    task.Status = DownloadStatus.Paused;
                    _repository.UpdateTaskStatus(task);
                }
            }
        });

        _eventBus.Subscribe<TorrentMetadataLoadedEvent>(e =>
        {
            lock (_lock)
            {
                var task = _tasks.FirstOrDefault(t => t.Id == e.TaskId);
                if (task != null)
                {
                    task.TotalBytes = e.TotalBytes;
                    if (!string.IsNullOrWhiteSpace(e.Name))
                    {
                        task.FileName = e.Name;
                        task.Name = e.Name;
                    }
                    _repository.UpdateTaskStatus(task);
                    _logger.LogInformation("Torrent meta verisi veritabanına işlendi: Task #{TaskId} -> {Name}, {Size} Byte", task.Id, task.FileName, task.TotalBytes);
                }
            }
        });
    }

    private void LoadExistingTasks()
    {
        try
        {
            var existing = _repository.GetAllTasks();
            lock (_lock)
            {
                _tasks.Clear();
                foreach (var task in existing)
                {
                    // Uygulama kapatıldığında veya çöktüğünde aktif durumda kalmış görevleri doğrula
                    if (task.Status is DownloadStatus.Downloading or DownloadStatus.Connecting or DownloadStatus.Merging or DownloadStatus.Verifying)
                    {
                        bool isCompletedOnDisk = false;

                        if (!string.IsNullOrEmpty(task.FinalFilePath) && File.Exists(task.FinalFilePath))
                        {
                            var fi = new FileInfo(task.FinalFilePath);
                            if (fi.Length > 0)
                            {
                                isCompletedOnDisk = true;
                                task.TotalBytes = fi.Length;
                                task.DownloadedBytes = fi.Length;
                            }
                        }
                        else if (!string.IsNullOrEmpty(task.SaveDirectory) && Directory.Exists(task.SaveDirectory))
                        {
                            var candidatePath = Path.Combine(task.SaveDirectory, task.FileName);
                            if (File.Exists(candidatePath))
                            {
                                var fi = new FileInfo(candidatePath);
                                if (fi.Length > 0)
                                {
                                    isCompletedOnDisk = true;
                                    task.TotalBytes = fi.Length;
                                    task.DownloadedBytes = fi.Length;
                                }
                            }
                        }

                        if (isCompletedOnDisk)
                        {
                            task.SetStatusDirect(DownloadStatus.Completed);
                            task.CompletedAt ??= DateTimeOffset.UtcNow;
                            _repository.UpdateTaskStatus(task);
                        }
                        else
                        {
                            // Yarım kalmış; şu an arkaplanda çalışan işlem olmadığı için Duraklatıldı yap
                            task.SetStatusDirect(DownloadStatus.Paused);
                            _repository.UpdateTaskStatus(task);
                        }
                    }

                    _tasks.Add(task);
                }

                // BitTorrent görevlerini MonoTorrent motoruna restore et (dosya boyutu ve durumu aktif kalsın)
                foreach (var torrentTask in existing.Where(t => t.Category == DownloadCategory.Torrent))
                {
                    _ = _torrentService.RestoreTorrentTaskAsync(
                        torrentTask.Id,
                        torrentTask.OriginalUrl,
                        torrentTask.SaveDirectory,
                        startImmediately: false);
                }
            }
            _logger.LogInformation("Veritabanından {Count} adet indirme görevi yüklendi.", existing.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mevcut indirme görevleri veritabanından yüklenemedi.");
        }
    }

    /// <summary>
    /// Verilen URL'e sahip mevcut aktif veya bekleyen bir görev olup olmadığını arar.
    /// </summary>
    public DownloadTask? FindDuplicateTask(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var cleanUrl = url.Trim();
        lock (_lock)
        {
            return _tasks.FirstOrDefault(t =>
                string.Equals(t.OriginalUrl?.Trim(), cleanUrl, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(t.FinalUrl) && string.Equals(t.FinalUrl.Trim(), cleanUrl, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// Yeni bir HTTP/HTTPS indirme görevi ekler ve başlatır.
    /// allowDuplicate false ise ve aynı URL mevcutsa, var olan görevi döner.
    /// </summary>
    public Task<DownloadTask> AddHttpDownloadAsync(
        string url,
        string? saveDirectory = null,
        int segmentCount = 8,
        DownloadCategory category = DownloadCategory.Other,
        string? fileName = null,
        string? referer = null,
        string? cookies = null,
        string? userAgent = null,
        bool startImmediately = true,
        bool allowDuplicate = true)
    {
        if (!allowDuplicate)
        {
            var existing = FindDuplicateTask(url);
            if (existing != null)
            {
                _logger.LogInformation("Tekrar eden URL tespit edildi, mevcut görev döndürülüyor: Task #{Id} ({Url})", existing.Id, url);
                return System.Threading.Tasks.Task.FromResult(existing);
            }
        }

        saveDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        if (category == DownloadCategory.Other)
        {
            var candidateName = !string.IsNullOrWhiteSpace(fileName) ? fileName : Path.GetFileName(url);
            category = DownloadCategoryExtensions.FromFileName(candidateName);
        }

        var task = new DownloadTask
        {
            OriginalUrl = url,
            Name = !string.IsNullOrWhiteSpace(fileName) ? fileName : (Path.GetFileName(url) ?? "download"),
            FileName = fileName ?? string.Empty,
            SaveDirectory = saveDirectory,
            SegmentCount = segmentCount,
            Category = category,
            Referer = referer,
            Cookies = cookies,
            UserAgent = userAgent,
            Status = DownloadStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _repository.AddTask(task);

        lock (_lock)
        {
            _tasks.Insert(0, task);
        }

        _eventBus.Publish(new TaskAddedEvent(task.Id, task.FileName));

        if (startImmediately)
        {
            lock (_lock)
            {
                int activeCount = _tasks.Count(t => t.Status is DownloadStatus.Downloading or DownloadStatus.Connecting);
                if (activeCount < MaxConcurrentDownloads)
                {
                    task.Status = DownloadStatus.Connecting;
                    _repository.UpdateTaskStatus(task);
                    StartHttpTask(task);
                }
                else
                {
                    task.Status = DownloadStatus.Queued;
                    _repository.UpdateTaskStatus(task);
                    _logger.LogInformation("Eşzamanlı indirme sınırı ({Max}) dolu. Görev #{Id} kuyruğa alındı.", MaxConcurrentDownloads, task.Id);
                }
            }
        }

        return System.Threading.Tasks.Task.FromResult(task);
    }

    /// <summary>
    /// Yeni bir BitTorrent / Magnet indirme görevi ekler ve başlatır.
    /// </summary>
    public async Task<DownloadTask> AddTorrentDownloadAsync(
        string magnetOrTorrentPath,
        string? saveDirectory = null,
        bool startImmediately = true,
        string? cookies = null,
        string? referrer = null,
        string? userAgent = null)
    {
        saveDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        var cleanPath = magnetOrTorrentPath.Split('?')[0].Trim('\"', '\'');
        var defaultName = magnetOrTorrentPath.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)
            ? "Magnet Torrent"
            : (Path.GetFileName(cleanPath) is { Length: > 0 } fn ? fn : "BitTorrent İndirmesi");

        var task = new DownloadTask
        {
            OriginalUrl = magnetOrTorrentPath,
            Name = defaultName,
            FileName = defaultName,
            SaveDirectory = saveDirectory,
            Category = DownloadCategory.Torrent,
            Status = DownloadStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _repository.AddTask(task);

        lock (_lock)
        {
            _tasks.Insert(0, task);
        }

        _eventBus.Publish(new TaskAddedEvent(task.Id, task.Name));

        if (startImmediately)
        {
            var manager = await _torrentService.AddAndStartTorrentAsync(
                task.Id, magnetOrTorrentPath, saveDirectory, cookies, referrer, userAgent);
            if (manager.Torrent != null)
            {
                if (!string.IsNullOrWhiteSpace(manager.Torrent.Name))
                {
                    task.Name = manager.Torrent.Name;
                    task.FileName = manager.Torrent.Name;
                }
                if (manager.Torrent.Size > 0)
                {
                    task.TotalBytes = manager.Torrent.Size;
                }
            }
            task.Status = DownloadStatus.Downloading;
            _repository.UpdateTaskStatus(task);
        }

        return task;
    }

    /// <summary>
    /// Medya indirme görevi ekler ve çalıştırır (MP4 veya MP3, tekil veya oynatma listesi).
    /// </summary>
    public Task<DownloadTask> AddMediaDownloadAsync(
        string url,
        string? saveDirectory = null,
        bool isAudioOnlyMp3 = false,
        bool isPlaylist = false,
        string? title = null,
        bool startImmediately = true,
        string? formatSpecifier = null)
    {
        saveDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        var defaultName = isPlaylist
            ? (isAudioOnlyMp3 ? "Çalma Listesi (MP3)" : "Çalma Listesi (MP4)")
            : (isAudioOnlyMp3 ? "Ses İndirme (MP3)" : "Video İndirme (MP4)");

        var task = new DownloadTask
        {
            OriginalUrl = url,
            Name = title ?? defaultName,
            FileName = title ?? (isAudioOnlyMp3 ? "Audio.mp3" : "Video.mp4"),
            SaveDirectory = saveDirectory,
            Category = isAudioOnlyMp3 ? DownloadCategory.Music : DownloadCategory.Video,
            Status = DownloadStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            FormatSpecifier = formatSpecifier
        };

        _repository.AddTask(task);

        lock (_lock)
        {
            _tasks.Insert(0, task);
        }

        _eventBus.Publish(new TaskAddedEvent(task.Id, task.Name));

        if (startImmediately)
        {
            var cts = new CancellationTokenSource();
            lock (_mediaCtsList)
            {
                _mediaCtsList[task.Id] = cts;
            }

            task.Status = DownloadStatus.Downloading;
            _repository.UpdateTaskStatus(task);

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await _mediaService.DownloadMediaAsync(task.Id, url, saveDirectory, isAudioOnlyMp3, isPlaylist, task.Name, task.FormatSpecifier, cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Medya indirme hatası: {TaskId}", task.Id);
                }
            });
        }

        return System.Threading.Tasks.Task.FromResult(task);
    }

    /// <summary>
    /// Çok satırlı metin veya aralık deseni içeren girdiyi ayrıştırarak toplu indirme görevleri oluşturur.
    /// </summary>
    /// <param name="batchText">URL listesi veya sayı/harf deseni (Örn: file_[01-20].zip).</param>
    /// <param name="options">Toplu indirme seçenekleri (hedef klasör, tekilleştirme, ad şablonu vb.).</param>
    /// <param name="startImmediately">Görevlerin hemen kuyruk sınırına göre başlatılıp başlatılmayacağı.</param>
    /// <returns>Oluşturulan ve kuyruğa eklenen geçerli DownloadTask listesi.</returns>
    public async Task<List<DownloadTask>> AddBatchDownloadsAsync(
        string batchText,
        BatchDownloadOptions? options = null,
        bool startImmediately = true)
    {
        options ??= new BatchDownloadOptions();
        var items = BatchDownloadParser.Parse(batchText, options);
        var createdTasks = new List<DownloadTask>();

        foreach (var item in items)
        {
            if (!item.IsValid)
            {
                _logger.LogWarning("Toplu indirme satırı atlandı: {Error} -> {Url}", item.ErrorMessage, item.Url);
                continue;
            }

            var task = await AddHttpDownloadAsync(
                url: item.Url,
                fileName: item.SuggestedFileName,
                saveDirectory: options.DestinationDirectory,
                segmentCount: options.Segments,
                startImmediately: startImmediately,
                allowDuplicate: !options.DeduplicateUrls);

            createdTasks.Add(task);
        }

        _logger.LogInformation("Toplu indirme ayrıştırıldı: {Count} geçerli görev oluşturuldu ve kuyruğa eklendi.", createdTasks.Count);
        return createdTasks;
    }

    /// <summary>
    /// Bir görevi başlatır veya devam ettirir.
    /// </summary>
    public async Task StartOrResumeTaskAsync(long taskId)
    {
        DownloadTask? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.Id == taskId);
        }
        if (task == null) return;

        if (task.Category == DownloadCategory.Torrent)
        {
            var resumed = await _torrentService.ResumeTorrentAsync(taskId);
            if (!resumed)
            {
                var restored = await _torrentService.RestoreTorrentTaskAsync(taskId, task.OriginalUrl, task.SaveDirectory, startImmediately: true);
                if (restored == null)
                {
                    task.Status = DownloadStatus.Failed;
                    task.LastErrorMessage = "Torrent motoru başlatılamadı.";
                    _repository.UpdateTaskStatus(task);
                    return;
                }
            }
            task.Status = DownloadStatus.Downloading;
            _repository.UpdateTaskStatus(task);
            return;
        }

        if (task.Category is DownloadCategory.Video or DownloadCategory.Music || YtDlpMediaService.IsMediaUrl(task.OriginalUrl))
        {
            var cts = new CancellationTokenSource();
            lock (_mediaCtsList)
            {
                _mediaCtsList[task.Id] = cts;
            }

            task.Status = DownloadStatus.Downloading;
            _repository.UpdateTaskStatus(task);

            bool isAudioOnly = task.Category == DownloadCategory.Music || task.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
            bool isPlaylist = task.OriginalUrl.Contains("list=", StringComparison.OrdinalIgnoreCase);

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await _mediaService.DownloadMediaAsync(task.Id, task.OriginalUrl, task.SaveDirectory, isAudioOnly, isPlaylist, task.Name, task.FormatSpecifier, cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Medya indirme hatası: {TaskId}", task.Id);
                }
            });
            return;
        }

        if (task.Status != DownloadStatus.Downloading && task.Status != DownloadStatus.Connecting)
        {
            task.Status = DownloadStatus.Connecting;
            _repository.UpdateTaskStatus(task);
        }

        StartHttpTask(task);
    }

    /// <summary>
    /// Kuyrukta bekleyen (Queued) görevleri MaxConcurrentDownloads sınırına göre çalıştırır.
    /// </summary>
    public async Task ProcessQueueAsync()
    {
        List<long> taskIdsToStart = new();

        lock (_lock)
        {
            int activeCount = _tasks.Count(t => t.Status is DownloadStatus.Downloading or DownloadStatus.Connecting);
            int availableSlots = MaxConcurrentDownloads - activeCount;

            if (availableSlots > 0)
            {
                var candidates = _tasks
                    .Where(t => t.Status == DownloadStatus.Queued)
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.CreatedAt)
                    .Take(availableSlots)
                    .Select(t => t.Id)
                    .ToList();

                taskIdsToStart.AddRange(candidates);
            }

            int queuedCount = _tasks.Count(t => t.Status == DownloadStatus.Queued);
            _eventBus.Publish(new QueueChangedEvent(activeCount, queuedCount));
        }

        foreach (var id in taskIdsToStart)
        {
            await StartOrResumeTaskAsync(id);
        }
    }

    /// <summary>
    /// Bir görevin kuyruk önceliğini günceller ve kuyruğu yeniden işler.
    /// </summary>
    public void SetTaskPriority(long taskId, int priority)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.Priority = priority;
                _repository.UpdateTaskStatus(task);
            }
        }
        _ = ProcessQueueAsync();
    }

    /// <summary>
    /// Kuyruktaki görevlerin sırasını değiştirir (sürükle-bırak desteği).
    /// </summary>
    public void MoveTaskInQueue(long taskId, int newIndex)
    {
        lock (_lock)
        {
            int currentIndex = _tasks.FindIndex(t => t.Id == taskId);
            if (currentIndex >= 0 && newIndex >= 0 && newIndex < _tasks.Count)
            {
                var task = _tasks[currentIndex];
                _tasks.RemoveAt(currentIndex);
                _tasks.Insert(newIndex, task);
            }
        }
        _ = ProcessQueueAsync();
    }

    /// <summary>
    /// Bekleyen veya duraklatılmış tüm görevleri başlatır.
    /// </summary>
    public async Task StartAllTasksAsync()
    {
        List<long> taskIds;
        lock (_lock)
        {
            taskIds = _tasks.Where(t => t.Status is DownloadStatus.Paused or DownloadStatus.Queued or DownloadStatus.Failed)
                            .Select(t => t.Id).ToList();
        }

        foreach (var id in taskIds)
        {
            await StartOrResumeTaskAsync(id);
        }
    }

    /// <summary>
    /// Çalışan tüm görevleri duraklatır.
    /// </summary>
    public async Task PauseAllTasksAsync()
    {
        List<long> taskIds;
        lock (_lock)
        {
            taskIds = _tasks.Where(t => t.IsActive).Select(t => t.Id).ToList();
        }

        foreach (var id in taskIds)
        {
            await PauseTaskAsync(id);
        }
    }

    /// <summary>
    /// Süresi dolmuş bir görevin indirme bağlantısını (URL) yeniler ve gerekirse devam ettirir (Link Refresh).
    /// </summary>
    public async Task UpdateTaskUrlAsync(long taskId, string newUrl, bool resumeImmediately = true)
    {
        DownloadTask? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.Id == taskId);
        }
        if (task == null) return;

        await PauseTaskAsync(taskId);

        lock (_lock)
        {
            task.OriginalUrl = newUrl;
            task.FinalUrl = newUrl;
            task.LastErrorMessage = null;
            _repository.UpdateTaskUrl(taskId, newUrl);
            if (_httpDownloaders.Remove(taskId, out var oldDownloader))
            {
                oldDownloader.Dispose();
            }
        }

        if (resumeImmediately)
        {
            await StartOrResumeTaskAsync(taskId);
        }
    }

    /// <summary>
    /// Downloads klasöründeki veritabanında aktif olmayan yetim geçici dosyaları (.fdown, .part, .tmp) tarar.
    /// </summary>
    public List<FileInfo> ScanOrphanTempFiles(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(directory)) return new List<FileInfo>();

        var activeTempFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        lock (_lock)
        {
            foreach (var t in _tasks)
            {
                if (!string.IsNullOrEmpty(t.TempFilePath))
                    activeTempFiles.Add(Path.GetFullPath(t.TempFilePath));
                if (!string.IsNullOrEmpty(t.SaveDirectory) && !string.IsNullOrEmpty(t.FileName))
                {
                    activeTempFiles.Add(Path.GetFullPath(Path.Combine(t.SaveDirectory, t.FileName)));
                    activeTempFiles.Add(Path.GetFullPath(Path.Combine(t.SaveDirectory, t.FileName + ".fdown")));
                    activeTempFiles.Add(Path.GetFullPath(Path.Combine(t.SaveDirectory, t.FileName + ".part")));
                }
            }
        }

        var results = new List<FileInfo>();
        try
        {
            var di = new DirectoryInfo(directory);
            foreach (var fi in di.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                var ext = fi.Extension.ToLowerInvariant();
                if (ext is ".fdown" or ".part" or ".tmp")
                {
                    if (!activeTempFiles.Contains(fi.FullName))
                    {
                        results.Add(fi);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yetim geçici dosyalar taranırken hata oluştu.");
        }

        return results;
    }

    /// <summary>
    /// Seçilen yetim dosyaları siler ve serbest bırakılan bayt miktarını döner.
    /// </summary>
    public long CleanOrphanTempFiles(IEnumerable<string> filePaths)
    {
        long freedBytes = 0;
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var len = new FileInfo(path).Length;
                    File.Delete(path);
                    freedBytes += len;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Yetim dosya silinemedi: {Path}", path);
            }
        }
        return freedBytes;
    }

    /// <summary>
    /// Bir görevi sıfırdan en baştan indirmek üzere sıfırlar ve başlatır.
    /// </summary>
    public async Task RestartTaskAsync(long taskId)
    {
        DownloadTask? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.Id == taskId);
        }
        if (task == null) return;

        // Önce çalışan görevi duraklat / durdur
        await PauseTaskAsync(taskId);

        if (task.Category == DownloadCategory.Torrent)
        {
            var resumed = await _torrentService.ResumeTorrentAsync(taskId);
            if (!resumed)
            {
                var restored = await _torrentService.RestoreTorrentTaskAsync(taskId, task.OriginalUrl, task.SaveDirectory, startImmediately: true);
                if (restored == null)
                {
                    task.Status = DownloadStatus.Failed;
                    task.LastErrorMessage = "Torrent motoru başlatılamadı.";
                    _repository.UpdateTaskStatus(task);
                    return;
                }
            }
            task.Status = DownloadStatus.Downloading;
            _repository.UpdateTaskStatus(task);
            return;
        }

        // HTTP veya Medya: indirilen boyut ve durum sıfırlanır
        lock (_lock)
        {
            task.DownloadedBytes = 0;
            task.Status = DownloadStatus.Connecting;
            task.LastErrorMessage = null;
            _repository.UpdateTaskStatus(task);
        }

        // Varsa kısmi dosya (.part) veya hedef dosya silinerek temiz başlangıç sağlanır
        try
        {
            var partFile = Path.Combine(task.SaveDirectory, task.FileName + ".part");
            if (File.Exists(partFile))
            {
                File.Delete(partFile);
            }
        }
        catch { }

        await StartOrResumeTaskAsync(taskId);
    }

    /// <summary>
    /// Bir görevi duraklatır.
    /// </summary>
    public async Task PauseTaskAsync(long taskId)
    {
        DownloadTask? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.Id == taskId);
        }
        if (task == null) return;

        if (task.Category == DownloadCategory.Torrent)
        {
            await _torrentService.PauseTorrentAsync(taskId);
            task.Status = DownloadStatus.Paused;
            _repository.UpdateTaskStatus(task);
            return;
        }

        if (_httpDownloaders.TryGetValue(taskId, out var downloader))
        {
            await downloader.PauseAsync();
        }

        if (_mediaCtsList.TryGetValue(taskId, out var cts))
        {
            try { cts.Cancel(); } catch { }
            _mediaService.StopDownload(taskId);
            task.Status = DownloadStatus.Paused;
            _repository.UpdateTaskStatus(task);
        }
    }

    /// <summary>
    /// Görevi iptal eder ve kaldırır.
    /// </summary>
    public async Task RemoveTaskAsync(long taskId, bool deleteFile = false)
    {
        DownloadTask? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.Id == taskId);
            _tasks.RemoveAll(t => t.Id == taskId);
            if (_httpDownloaders.Remove(taskId, out var removedDownloader))
            {
                removedDownloader.Dispose();
            }
            if (_mediaCtsList.Remove(taskId, out var removedCts))
            {
                removedCts.Cancel();
                removedCts.Dispose();
            }
        }

        task ??= _repository.GetTask(taskId);

        if (task != null && task.Status != DownloadStatus.Paused && task.Status != DownloadStatus.Completed && task.Status != DownloadStatus.Failed)
        {
            try
            {
                await PauseTaskAsync(taskId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Görev duraklatılırken hata alındı (#{TaskId}), kaldırmaya devam ediliyor.", taskId);
            }
        }

        _mediaService.StopDownload(taskId);

        if (deleteFile && task != null)
        {
            // Windows'ta sürecin dosya tanıtıcısını tamamen bırakması için kısa bekleme
            await System.Threading.Tasks.Task.Delay(150);
            DeleteTaskFiles(task);
        }

        try
        {
            await _torrentService.RemoveTorrentAsync(taskId, deleteFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torrent motorundan kaldırılırken hata alındı (#{TaskId})", taskId);
        }

        try
        {
            _repository.DeleteTask(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Görev veritabanından silinirken hata alındı (#{TaskId})", taskId);
        }

        _eventBus.Publish(new TaskCancelledEvent(taskId));
    }

    private static void DeleteTaskFiles(DownloadTask task)
    {
        try
        {
            // 1. Tamamlanmış dosya
            if (!string.IsNullOrEmpty(task.FinalFilePath))
            {
                PathSafetyHelper.SafeDeleteFile(task.FinalFilePath);
            }

            // 2. Geçici indirme dosyası (.fdown / TempFilePath)
            if (!string.IsNullOrEmpty(task.TempFilePath))
            {
                PathSafetyHelper.SafeDeleteFile(task.TempFilePath);
            }

            // 3. Olası geçici uzantılar (Sadece bu dosyanın adıyla biten: ornek.zip.fdown, ornek.zip.part vb.)
            if (!string.IsNullOrEmpty(task.FinalFilePath))
            {
                string[] tempExtensions = [".fdown", ".fdtmp", ".part", ".ytdl", ".tmp"];
                foreach (var ext in tempExtensions)
                {
                    var p = task.FinalFilePath + ext;
                    PathSafetyHelper.SafeDeleteFile(p);
                }
            }

            // 4. SaveDirectory içindeki göreve ait parçalar (.part, .ytdl, yt-dlp akışları .f*.mp4, .f*.m4a vb.)
            var saveDir = task.SaveDirectory ?? (!string.IsNullOrEmpty(task.FinalFilePath) ? Path.GetDirectoryName(task.FinalFilePath) : null);
            var targetName = !string.IsNullOrEmpty(task.FileName) ? task.FileName : (!string.IsNullOrEmpty(task.FinalFilePath) ? Path.GetFileName(task.FinalFilePath) : null);

            if (!string.IsNullOrEmpty(saveDir) && Directory.Exists(saveDir) && !string.IsNullOrEmpty(targetName))
            {
                try
                {
                    // Doğrudan hedef dosya
                    var directTarget = Path.Combine(saveDir, targetName);
                    PathSafetyHelper.SafeDeleteFile(directTarget);

                    // Uzantısız temel isim (örneğin "VideoTitle.mp4" -> "VideoTitle")
                    var baseName = Path.GetFileNameWithoutExtension(targetName);
                    if (!string.IsNullOrWhiteSpace(baseName) && baseName.Length >= 3)
                    {
                        var candidateFiles = Directory.GetFiles(saveDir, $"{baseName}*");
                        foreach (var f in candidateFiles)
                        {
                            var fName = Path.GetFileName(f);
                            // Sadece bu göreve ait akış ve geçici dosyaları sil (ör. baseName.f400.mp4, baseName.f140.m4a, baseName.part vb.)
                            if (fName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase) ||
                                fName.StartsWith(baseName + "-", StringComparison.OrdinalIgnoreCase) ||
                                fName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                            {
                                PathSafetyHelper.SafeDeleteFile(f);
                            }
                        }
                    }
                }
                catch { }
            }

            // 5. Torrent görevi ise ve FinalFilePath henüz atanmamışsa SaveDirectory + FileName kontrolü
            if (task.Category == DownloadCategory.Torrent && !string.IsNullOrEmpty(task.SaveDirectory) && !string.IsNullOrEmpty(task.FileName))
            {
                var candidateSingleFile = Path.Combine(task.SaveDirectory, task.FileName);
                PathSafetyHelper.SafeDeleteFile(candidateSingleFile);

                var candidateDir = Path.Combine(task.SaveDirectory, task.FileName);
                if (Directory.Exists(candidateDir))
                {
                    PathSafetyHelper.SafeDeleteDirectory(candidateDir, task.SaveDirectory);
                }
            }
        }
        catch { }
    }

    public TorrentDetails? GetTorrentDetails(long taskId)
    {
        return _torrentService.GetTorrentDetails(taskId);
    }

    private void StartHttpTask(DownloadTask task)
    {
        lock (_httpDownloaders)
        {
            if (_httpDownloaders.TryGetValue(task.Id, out var existing))
            {
                if (existing.IsRunning)
                {
                    return;
                }
                existing.Dispose();
                _httpDownloaders.Remove(task.Id);
            }

            var downloader = new HttpDownloader(
                task,
                _repository,
                _eventBus,
                _httpClient,
                _loggerFactory,
                speedLimiter: SpeedLimiter);

            _httpDownloaders[task.Id] = downloader;
            downloader.Start();
        }
    }

    public void SetSpeedLimits(long downloadBytesPerSec, long uploadBytesPerSec)
    {
        SpeedLimiter.LimitBytesPerSecond = downloadBytesPerSec;
        _torrentService.SetRateLimits(downloadBytesPerSec, uploadBytesPerSec);
    }

    public Task SetTorrentFilePriorityAsync(long taskId, int fileIndex, bool download)
        => _torrentService.SetFilePriorityAsync(taskId, fileIndex, download);

    public Task SetTorrentFilePriorityAsync(long taskId, int fileIndex, MonoTorrent.Priority priority)
        => _torrentService.SetFilePriorityAsync(taskId, fileIndex, priority);

    public Task SetAllTorrentFilesPriorityAsync(long taskId, bool downloadAll)
        => _torrentService.SetAllFilesPriorityAsync(taskId, downloadAll);

    public Task<List<TorrentFileInfo>> InspectTorrentFilesAsync(
        string sourceUrlOrPath,
        string? cookies = null,
        string? referrer = null,
        string? userAgent = null)
        => _torrentService.InspectTorrentFilesAsync(sourceUrlOrPath, cookies, referrer, userAgent);

    public Task StopTorrentSeedingAsync(long taskId)
        => _torrentService.StopSeedingAsync(taskId);

    public Task CleanUnattachedTorrentManagersAsync()
        => _torrentService.CleanUnattachedManagersAsync();

    public Task<bool> ToggleTorrentSequentialDownloadAsync(long taskId)
        => _torrentService.ToggleSequentialDownloadAsync(taskId);

    public bool IsTorrentSequentialDownload(long taskId)
        => _torrentService.IsSequentialDownload(taskId);

    public Task ForceRecheckTorrentAsync(long taskId)
        => _torrentService.ForceRecheckAsync(taskId);

    public Task<int> AddTrackersToTorrentAsync(long taskId, IEnumerable<string> trackerUrls)
        => _torrentService.AddTrackersAsync(taskId, trackerUrls);

    public Task ForceReannounceTorrentAsync(long taskId)
        => _torrentService.ForceReannounceAsync(taskId);

    public TorrentSettings GetTorrentSettings()
    {
        var settings = new TorrentSettings();

        if (int.TryParse(_repository.GetSetting("Torrent.ListeningPort"), out var port) && port > 0)
            settings.ListeningPort = port;

        if (bool.TryParse(_repository.GetSetting("Torrent.EnableUpnp"), out var upnp))
            settings.EnableUpnp = upnp;

        if (bool.TryParse(_repository.GetSetting("Torrent.EnableDht"), out var dht))
            settings.EnableDht = dht;

        if (bool.TryParse(_repository.GetSetting("Torrent.EnablePeX"), out var pex))
            settings.EnablePeX = pex;

        if (bool.TryParse(_repository.GetSetting("Torrent.EnableLsd"), out var lsd))
            settings.EnableLsd = lsd;

        if (int.TryParse(_repository.GetSetting("Torrent.EncryptionMode"), out var enc))
            settings.EncryptionMode = enc;

        if (int.TryParse(_repository.GetSetting("Torrent.MaxConnectionsTotal"), out var maxConn) && maxConn > 0)
            settings.MaxConnectionsTotal = maxConn;

        if (int.TryParse(_repository.GetSetting("Torrent.MaxConnectionsPerTorrent"), out var maxPer) && maxPer > 0)
            settings.MaxConnectionsPerTorrent = maxPer;

        if (bool.TryParse(_repository.GetSetting("Torrent.EnableAutoSequentialForVideo"), out var autoSeq))
            settings.EnableAutoSequentialForVideo = autoSeq;

        if (double.TryParse(_repository.GetSetting("Torrent.SeedRatioLimit"), System.Globalization.CultureInfo.InvariantCulture, out var ratio))
            settings.SeedRatioLimit = ratio;

        if (bool.TryParse(_repository.GetSetting("Torrent.StopSeedingWhenRatioReached"), out var stopSeed))
            settings.StopSeedingWhenRatioReached = stopSeed;

        settings.NetworkInterfaceId = _repository.GetSetting("Torrent.NetworkInterfaceId");

        return settings;
    }

    public async Task SaveTorrentSettingsAsync(TorrentSettings settings)
    {
        _repository.SetSetting("Torrent.ListeningPort", settings.ListeningPort.ToString());
        _repository.SetSetting("Torrent.EnableUpnp", settings.EnableUpnp.ToString());
        _repository.SetSetting("Torrent.EnableDht", settings.EnableDht.ToString());
        _repository.SetSetting("Torrent.EnablePeX", settings.EnablePeX.ToString());
        _repository.SetSetting("Torrent.EnableLsd", settings.EnableLsd.ToString());
        _repository.SetSetting("Torrent.EncryptionMode", settings.EncryptionMode.ToString());
        _repository.SetSetting("Torrent.MaxConnectionsTotal", settings.MaxConnectionsTotal.ToString());
        _repository.SetSetting("Torrent.MaxConnectionsPerTorrent", settings.MaxConnectionsPerTorrent.ToString());
        _repository.SetSetting("Torrent.EnableAutoSequentialForVideo", settings.EnableAutoSequentialForVideo.ToString());
        _repository.SetSetting("Torrent.SeedRatioLimit", settings.SeedRatioLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _repository.SetSetting("Torrent.StopSeedingWhenRatioReached", settings.StopSeedingWhenRatioReached.ToString());
        _repository.SetSetting("Torrent.NetworkInterfaceId", settings.NetworkInterfaceId ?? "");

        await _torrentService.ApplySettingsAsync(settings);
    }

    /// <summary>
    /// Kayıtlı proxy yapılandırmasını döner.
    /// </summary>
    public ProxyConfig GetProxyConfig()
    {
        var config = new ProxyConfig();
        var modeStr = _repository.GetSetting("Proxy.Mode");
        if (Enum.TryParse<ProxyMode>(modeStr, out var mode))
        {
            config.Mode = mode;
        }
        else
        {
            var useSystem = _repository.GetSetting("Proxy.UseSystemProxy");
            config.Mode = (useSystem == null || useSystem == "1" || useSystem.Equals("true", StringComparison.OrdinalIgnoreCase))
                ? ProxyMode.System
                : ProxyMode.Manual;
        }

        var protoStr = _repository.GetSetting("Proxy.Protocol");
        if (Enum.TryParse<ProxyProtocol>(protoStr, out var proto))
        {
            config.Protocol = proto;
        }

        config.Server = _repository.GetSetting("Proxy.Server") ?? _repository.GetSetting("Proxy.ManualProxyUrl");
        if (int.TryParse(_repository.GetSetting("Proxy.Port"), out var port))
        {
            config.Port = port;
        }
        config.Username = _repository.GetSetting("Proxy.Username");
        config.Password = _repository.GetSetting("Proxy.Password");

        var bypassLocal = _repository.GetSetting("Proxy.BypassLocal");
        config.BypassLocal = bypassLocal == null || bypassLocal == "1" || bypassLocal.Equals("true", StringComparison.OrdinalIgnoreCase);

        var bypassList = _repository.GetSetting("Proxy.BypassList");
        if (!string.IsNullOrWhiteSpace(bypassList))
        {
            config.BypassList = bypassList.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return config;
    }

    /// <summary>
    /// Proxy yapılandırmasını güvenli olarak kaydeder.
    /// </summary>
    public void SaveProxyConfig(ProxyConfig config)
    {
        _repository.SetSetting("Proxy.Mode", config.Mode.ToString());
        _repository.SetSetting("Proxy.Protocol", config.Protocol.ToString());
        _repository.SetSetting("Proxy.Server", config.Server ?? string.Empty);
        _repository.SetSetting("Proxy.Port", config.Port.ToString());
        _repository.SetSetting("Proxy.Username", config.Username ?? string.Empty);
        _repository.SetSetting("Proxy.Password", config.Password ?? string.Empty);
        _repository.SetSetting("Proxy.BypassLocal", config.BypassLocal.ToString());
        _repository.SetSetting("Proxy.BypassList", string.Join(";", config.BypassList));
    }

    public async ValueTask DisposeAsync()
    {
        List<HttpDownloader> downloaders;
        lock (_lock)
        {
            downloaders = _httpDownloaders.Values.ToList();
            _httpDownloaders.Clear();
        }

        foreach (var downloader in downloaders)
        {
            try
            {
                await downloader.PauseAsync();
            }
            catch { }
            downloader.Dispose();
        }

        List<CancellationTokenSource> mediaCts;
        lock (_mediaCtsList)
        {
            mediaCts = _mediaCtsList.Values.ToList();
            _mediaCtsList.Clear();
        }

        foreach (var cts in mediaCts)
        {
            try
            {
                cts.Cancel();
            }
            catch { }
            cts.Dispose();
        }

        Scheduler.Dispose();
        NetworkWatcher.Dispose();
        await _torrentService.DisposeAsync();
    }
}
