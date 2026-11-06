using System.Diagnostics;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine;
using FDownloader.Core.Events;
using FDownloader.Core.Manager;
using FDownloader.Core.Storage;
using FDownloader.Core.Torrent;
using FDownloader.Core.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class QueueAndSchedulerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Database _database;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly HttpClient _httpClient;
    private readonly TorrentService _torrentService;
    private readonly YtDlpMediaService _mediaService;

    public QueueAndSchedulerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_QueueTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var dbPath = Path.Combine(_tempDir, "queue_test.db");
        _database = new Database(dbPath, NullLogger<Database>.Instance);
        _database.Initialize();

        _repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);
        _eventBus = new DownloadEventBus();
        _httpClient = new HttpClient();
        _torrentService = new TorrentService(
            _eventBus,
            NullLogger<TorrentService>.Instance,
            Path.Combine(_tempDir, "torrent_downloads"),
            new FDownloader.Core.Torrent.TorrentSettings { ListeningPort = 0, EnableDht = false, EnableUpnp = false, EnableLsd = false, EnableFallbackTrackers = false },
            Path.Combine(_tempDir, "torrent_cache"));
        _mediaService = new YtDlpMediaService(_eventBus, NullLogger<YtDlpMediaService>.Instance, _httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _torrentService.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { _database.Dispose(); } catch { }
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task SpeedLimiter_ThrottleAsync_EnforcesLimitAndAllowsDynamicChange()
    {
        var limiter = new SpeedLimiter(limitBytesPerSecond: 100_000); // 100 KB/s
        Assert.Equal(100_000, limiter.LimitBytesPerSecond);

        // Dinamik olarak 200 KB/s yap
        limiter.LimitBytesPerSecond = 200_000;
        Assert.Equal(200_000, limiter.LimitBytesPerSecond);

        // Limit varken 50 KB tüketim hızlıca gerçekleşir
        var sw = Stopwatch.StartNew();
        await limiter.ThrottleAsync(50_000);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500);

        // Limiti sınırsıza çek (0 = sınırsız)
        limiter.LimitBytesPerSecond = 0;
        await limiter.ThrottleAsync(500_000); // Beklemesiz geçmeli
    }

    [Fact]
    public async Task DownloadManager_QueueLimit_EnforcesMaxConcurrentDownloads()
    {
        await using var manager = new DownloadManager(
            _repository,
            _eventBus,
            _torrentService,
            _mediaService,
            _httpClient,
            NullLoggerFactory.Instance);

        manager.MaxConcurrentDownloads = 2;

        // 3 görev ekleyelim
        var task1 = await manager.AddHttpDownloadAsync("http://example.com/file1.zip", _tempDir, startImmediately: true);
        var task2 = await manager.AddHttpDownloadAsync("http://example.com/file2.zip", _tempDir, startImmediately: true);
        var task3 = await manager.AddHttpDownloadAsync("http://example.com/file3.zip", _tempDir, startImmediately: true);

        // İlk 2'si aktif (Connecting veya Downloading), 3.sü Queued olmalı
        Assert.True(task1.Status is DownloadStatus.Downloading or DownloadStatus.Connecting);
        Assert.True(task2.Status is DownloadStatus.Downloading or DownloadStatus.Connecting);
        Assert.Equal(DownloadStatus.Queued, task3.Status);

        // task1 tamamlandığında kuyruk tetiklenmeli ve task3 aktif (Connecting veya Downloading) olmalı
        _eventBus.Publish(new TaskCompletedEvent(task1.Id, task1.FinalFilePath, 1000, TimeSpan.FromSeconds(1)));

        // Kısa bir bekleme ile ProcessQueueAsync'in çalışmasına izin ver
        await Task.Delay(100);

        Assert.Equal(DownloadStatus.Completed, task1.Status);
        Assert.True(task3.Status is DownloadStatus.Downloading or DownloadStatus.Connecting);
    }

    [Fact]
    public async Task DownloadManager_PriorityOrdering_PicksHighestPriorityFirst()
    {
        await using var manager = new DownloadManager(
            _repository,
            _eventBus,
            _torrentService,
            _mediaService,
            _httpClient,
            NullLoggerFactory.Instance);

        manager.MaxConcurrentDownloads = 1;

        var task1 = await manager.AddHttpDownloadAsync("http://example.com/file1.zip", _tempDir, startImmediately: true);
        var taskLow = await manager.AddHttpDownloadAsync("http://example.com/low.zip", _tempDir, startImmediately: true);
        var taskHigh = await manager.AddHttpDownloadAsync("http://example.com/high.zip", _tempDir, startImmediately: true);

        manager.SetTaskPriority(taskLow.Id, priority: 10);
        manager.SetTaskPriority(taskHigh.Id, priority: 200);

        Assert.True(task1.Status is DownloadStatus.Downloading or DownloadStatus.Connecting);
        Assert.Equal(DownloadStatus.Queued, taskLow.Status);
        Assert.Equal(DownloadStatus.Queued, taskHigh.Status);

        // task1 bittiğinde, kuyruktan priority'si 200 olan taskHigh önce başlamalı!
        _eventBus.Publish(new TaskCompletedEvent(task1.Id, task1.FinalFilePath, 1000, TimeSpan.FromSeconds(1)));
        await Task.Delay(250);

        Assert.True(taskHigh.Status is DownloadStatus.Downloading or DownloadStatus.Connecting);
        Assert.Equal(DownloadStatus.Queued, taskLow.Status);
    }

    [Fact]
    public async Task DownloadScheduler_StartTimeTrigger_TriggersScheduledStart()
    {
        bool started = false;
        bool stopped = false;

        using var scheduler = new DownloadScheduler(
            _eventBus,
            NullLogger<DownloadScheduler>.Instance,
            onStartScheduled: () => { started = true; return Task.CompletedTask; },
            onStopScheduled: () => { stopped = true; return Task.CompletedTask; });

        scheduler.IsEnabled = true;
        var now = DateTimeOffset.Now;
        scheduler.StartTime = TimeOnly.FromTimeSpan(now.TimeOfDay); // Şimdi başlat

        await scheduler.CheckScheduleAsync(now);

        Assert.True(started, "Zamanlayıcı eşleştiğinde başlatma callback'i tetiklenmeli");
        Assert.False(stopped);
    }

    [Fact]
    public async Task NetworkWatcher_WhenNetworkLostAndRestored_PausesAndResumesTasks()
    {
        List<long> pausedIds = new();
        List<long> resumedIds = new();

        using var watcher = new NetworkWatcher(
            _eventBus,
            NullLogger<NetworkWatcher>.Instance,
            onNetworkLost: () =>
            {
                pausedIds.Add(999);
                return Task.FromResult(new List<long> { 999 });
            },
            onNetworkRestored: ids =>
            {
                resumedIds.AddRange(ids);
                return Task.CompletedTask;
            });

        // 1. Ağ koptu simülasyonu
        await watcher.HandleAvailabilityChangeAsync(false);
        Assert.False(watcher.IsNetworkAvailable);
        Assert.Contains(999, pausedIds);

        // 2. Ağ geri geldi simülasyonu
        await watcher.HandleAvailabilityChangeAsync(true);
        Assert.True(watcher.IsNetworkAvailable);
        Assert.Contains(999, resumedIds);
    }
}
