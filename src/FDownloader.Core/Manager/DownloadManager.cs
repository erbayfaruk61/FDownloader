using FDownloader.Core.Domain;
using FDownloader.Core.Engine;
using FDownloader.Core.Events;
using FDownloader.Core.Storage;
using FDownloader.Core.Torrent;
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

    public string? GetSetting(string key) => _repository.GetSetting(key);
    public void SetSetting(string key, string value) => _repository.SetSetting(key, value);

    public DownloadManager(
        DownloadRepository repository,
        DownloadEventBus eventBus,
        TorrentService torrentService,
        YtDlpMediaService mediaService,
        HttpClient httpClient,
        ILoggerFactory loggerFactory)
    {
        _repository = repository;
        _eventBus = eventBus;
        _torrentService = torrentService;
        _mediaService = mediaService;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DownloadManager>();

        SubscribeToEvents();
        LoadExistingTasks();
    }

    private void SubscribeToEvents()
    {
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
                    // Uygulama kapatıldığında veya çöktüğünde 'Downloading' durumunda kalmış görevleri doğrula
                    if (task.Status == DownloadStatus.Downloading)
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
                            task.Status = DownloadStatus.Completed;
                            task.CompletedAt ??= DateTimeOffset.UtcNow;
                            _repository.UpdateTaskStatus(task);
                        }
                        else
                        {
                            // Yarım kalmış; şu an arkaplanda çalışan işlem olmadığı için Duraklatıldı yap
                            task.Status = DownloadStatus.Paused;
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
    /// Yeni bir HTTP/HTTPS indirme görevi ekler ve başlatır.
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
        bool startImmediately = true)
    {
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
            StartHttpTask(task);
        }

        return System.Threading.Tasks.Task.FromResult(task);
    }

    /// <summary>
    /// Yeni bir BitTorrent / Magnet indirme görevi ekler ve başlatır.
    /// </summary>
    public async Task<DownloadTask> AddTorrentDownloadAsync(
        string magnetOrTorrentPath,
        string? saveDirectory = null,
        bool startImmediately = true)
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
            await _torrentService.AddAndStartTorrentAsync(task.Id, magnetOrTorrentPath, saveDirectory);
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
        bool startImmediately = true)
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
                    await _mediaService.DownloadMediaAsync(task.Id, url, saveDirectory, isAudioOnlyMp3, isPlaylist, task.Name, cts.Token);
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
            await _torrentService.ResumeTorrentAsync(taskId);
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
                    await _mediaService.DownloadMediaAsync(task.Id, task.OriginalUrl, task.SaveDirectory, isAudioOnly, isPlaylist, task.Name, cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Medya indirme hatası: {TaskId}", task.Id);
                }
            });
            return;
        }

        StartHttpTask(task);
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
            cts.Cancel();
            task.Status = DownloadStatus.Paused;
            _repository.UpdateTaskStatus(task);
        }
    }

    /// <summary>
    /// Görevi iptal eder ve kaldırır.
    /// </summary>
    public async Task RemoveTaskAsync(long taskId, bool deleteFile = false)
    {
        await PauseTaskAsync(taskId);

        DownloadTask? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.Id == taskId);
            _tasks.RemoveAll(t => t.Id == taskId);
            _httpDownloaders.Remove(taskId);
            _mediaCtsList.Remove(taskId);
        }

        task ??= _repository.GetTask(taskId);

        if (deleteFile && task != null)
        {
            DeleteTaskFiles(task);
        }

        await _torrentService.RemoveTorrentAsync(taskId, deleteFile);
        _repository.DeleteTask(taskId);
        _eventBus.Publish(new TaskCancelledEvent(taskId));
    }

    private static void DeleteTaskFiles(DownloadTask task)
    {
        try
        {
            // 1. Tamamlanmış dosya
            if (!string.IsNullOrEmpty(task.FinalFilePath) && File.Exists(task.FinalFilePath))
            {
                try { File.Delete(task.FinalFilePath); } catch { }
            }

            // 2. Geçici indirme dosyası (.fdown / TempFilePath)
            if (!string.IsNullOrEmpty(task.TempFilePath) && File.Exists(task.TempFilePath))
            {
                try { File.Delete(task.TempFilePath); } catch { }
            }

            // 3. Olası geçici uzantılar (Sadece bu dosyanın adıyla biten: ornek.zip.fdown, ornek.zip.part vb.)
            if (!string.IsNullOrEmpty(task.FinalFilePath))
            {
                string[] tempExtensions = [".fdown", ".fdtmp", ".part", ".ytdl", ".tmp"];
                foreach (var ext in tempExtensions)
                {
                    var p = task.FinalFilePath + ext;
                    if (File.Exists(p))
                    {
                        try { File.Delete(p); } catch { }
                    }
                }

                // yt-dlp format birleştirme aşamasındaki geçici dosyalar (ornek.f137.mp4, ornek.temp.mp4 vb.)
                if (!string.IsNullOrEmpty(task.SaveDirectory) && Directory.Exists(task.SaveDirectory) && !string.IsNullOrEmpty(task.FileName))
                {
                    var exactName = task.FileName;
                    var nameOnly = Path.GetFileNameWithoutExtension(exactName);

                    try
                    {
                        // Sadece bu dosyanın birebir adına eklenen .part / .ytdl veya video format geçici parçaları
                        var exactMatches = Directory.GetFiles(task.SaveDirectory, $"{exactName}.*");
                        foreach (var f in exactMatches)
                        {
                            var fExt = Path.GetExtension(f).ToLowerInvariant();
                            if (fExt is ".fdown" or ".fdtmp" or ".part" or ".ytdl" or ".tmp")
                            {
                                try { File.Delete(f); } catch { }
                            }
                        }
                    }
                    catch { }
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
            if (_httpDownloaders.TryGetValue(task.Id, out var existing) && existing.IsRunning)
            {
                return;
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

    public Task SetAllTorrentFilesPriorityAsync(long taskId, bool downloadAll)
        => _torrentService.SetAllFilesPriorityAsync(taskId, downloadAll);

    public async ValueTask DisposeAsync()
    {
        foreach (var downloader in _httpDownloaders.Values)
        {
            await downloader.PauseAsync();
        }
        await _torrentService.DisposeAsync();
    }
}
