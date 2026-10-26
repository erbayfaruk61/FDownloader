using System.Diagnostics;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine.Probe;
using FDownloader.Core.Engine.Segmenter;
using FDownloader.Core.Engine.Worker;
using FDownloader.Core.Engine.Writer;
using FDownloader.Core.Events;
using FDownloader.Core.Storage;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Engine;

/// <summary>
/// Tek bir HTTP/HTTPS indirme görevinin yaşam döngüsünü yöneten motor.
/// Probe -> Plan -> Preallocate -> Multi-worker indirme -> Finalize adımlarını yürütür.
/// </summary>
public sealed class HttpDownloader
{
    private readonly DownloadTask _task;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HttpDownloader> _logger;
    private readonly SpeedLimiter? _speedLimiter;

    private readonly SpeedCalculator _speedCalculator = new();
    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    public DownloadTask Task => _task;
    public bool IsRunning => _runningTask != null && !_runningTask.IsCompleted;

    public HttpDownloader(
        DownloadTask task,
        DownloadRepository repository,
        DownloadEventBus eventBus,
        HttpClient httpClient,
        ILoggerFactory loggerFactory,
        SpeedLimiter? speedLimiter = null)
    {
        _task = task;
        _repository = repository;
        _eventBus = eventBus;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _speedLimiter = speedLimiter;
        _logger = loggerFactory.CreateLogger<HttpDownloader>();
    }

    /// <summary>
    /// İndirme işlemini asenkron olarak başlatır.
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _runningTask = System.Threading.Tasks.Task.Run(() => ExecuteDownloadAsync(_cts.Token));
    }

    /// <summary>
    /// İndirmeyi duraklatır.
    /// </summary>
    public async Task PauseAsync()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        if (_runningTask != null)
        {
            try
            {
                await _runningTask;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pause sırasında hata oluştu: Task {Id}", _task.Id);
            }
        }

        _task.Status = DownloadStatus.Paused;
        _repository.UpdateTaskStatus(_task);
        _eventBus.Publish(new TaskPausedEvent(_task.Id));
    }

    /// <summary>
    /// İndirmeyi iptal eder ve geçici dosyayı temizler.
    /// </summary>
    public async Task CancelAsync()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        if (_runningTask != null)
        {
            try
            {
                await _runningTask;
            }
            catch { }
        }

        _task.Status = DownloadStatus.Cancelled;
        _repository.UpdateTaskStatus(_task);
        _eventBus.Publish(new TaskCancelledEvent(_task.Id));

        if (File.Exists(_task.TempFilePath))
        {
            try { File.Delete(_task.TempFilePath); } catch { }
        }
    }

    private async Task ExecuteDownloadAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _task.Status = DownloadStatus.Connecting;
        _task.StartedAt ??= DateTimeOffset.UtcNow;
        _repository.UpdateTaskStatus(_task);
        _eventBus.Publish(new TaskStartedEvent(_task.Id));

        FileWriter? writer = null;

        try
        {
            // 1. ADIM: Sunucu Probe'u (Eğer daha önce yapılmadıysa)
            if (_task.TotalBytes <= 0 || string.IsNullOrEmpty(_task.FileName))
            {
                var prober = new DownloadProber(
                    _httpClient,
                    _loggerFactory.CreateLogger<DownloadProber>());

                var probeResult = await prober.ProbeAsync(
                    _task.OriginalUrl,
                    _task.Referer,
                    _task.Cookies,
                    _task.UserAgent,
                    cancellationToken);

                _task.FinalUrl = probeResult.FinalUrl;
                _task.FileName = FileNameSanitizer.Sanitize(probeResult.FileName);
                if (_task.Category == DownloadCategory.Other)
                {
                    _task.Category = DownloadCategoryExtensions.FromFileName(_task.FileName);
                }
                _task.TotalBytes = probeResult.ContentLength;
                _task.SupportsRange = probeResult.SupportsRange;
                _task.ETag = probeResult.ETag;
                _task.LastModified = probeResult.LastModified;

                // Hedef klasör ve geçici dosya yolu
                if (string.IsNullOrEmpty(_task.SaveDirectory))
                {
                    _task.SaveDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
                }
                Directory.CreateDirectory(_task.SaveDirectory);
                _task.TempFilePath = Path.Combine(_task.SaveDirectory, _task.FileName + ".fdown");

                // Disk alanı kontrolü
                if (_task.TotalBytes > 0 && !DiskSpaceChecker.HasEnoughSpace(_task.SaveDirectory, _task.TotalBytes))
                {
                    var availableBytes = DiskSpaceChecker.GetAvailableSpace(_task.SaveDirectory);
                    _eventBus.Publish(new DiskFullEvent(_task.Id, _task.SaveDirectory, _task.TotalBytes, availableBytes));
                    throw new IOException($"Yetersiz disk alanı! Gerekli: {_task.TotalBytes}, Mevcut: {availableBytes}");
                }

                _eventBus.Publish(new ProbeCompletedEvent(
                    _task.Id,
                    _task.FileName,
                    _task.TotalBytes,
                    _task.SupportsRange,
                    _task.SegmentCount));
            }

            // 2. ADIM: Segmentlerin Planlanması (Yoksa oluştur)
            if (_task.Segments.Count == 0)
            {
                var segmentCount = _task.SupportsRange && _task.TotalBytes > 1024 * 1024
                    ? _task.SegmentCount
                    : 1;

                _task.Segments = SegmentPlanner.Plan(_task.Id, _task.TotalBytes, _task.SupportsRange, segmentCount);
                _task.SegmentCount = _task.Segments.Count;
                _repository.AddSegments(_task.Segments);
            }

            _repository.UpdateTaskStatus(_task);

            // 3. ADIM: FileWriter Başlatma
            writer = new FileWriter(
                _task.TempFilePath,
                _loggerFactory.CreateLogger<FileWriter>());

            await writer.InitializeAsync(_task.TotalBytes, cancellationToken);

            _task.Status = DownloadStatus.Downloading;
            _repository.UpdateTaskStatus(_task);

            // 4. ADIM: Segment Worker'larının Çalıştırılması
            var workerLogger = _loggerFactory.CreateLogger<SegmentWorker>();
            var workerTasks = new List<Task<bool>>();

            // İlerleme yayınlama döngüsü için timer
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressReportingTask = StartProgressReportingLoopAsync(progressCts.Token);

            foreach (var segment in _task.Segments)
            {
                if (segment.IsCompleted) continue;

                var worker = new SegmentWorker(
                    segment,
                    _task.FinalUrl,
                    writer,
                    _httpClient,
                    workerLogger,
                    maxRetries: 5,
                    stallTimeout: TimeSpan.FromSeconds(30),
                    userAgent: _task.UserAgent,
                    referer: _task.Referer,
                    cookies: _task.Cookies,
                    speedLimiter: _speedLimiter);

                worker.BytesWritten += bytes =>
                {
                    lock (_speedCalculator)
                    {
                        _speedCalculator.AddSample(bytes);
                        _task.DownloadedBytes += bytes;
                    }
                };

                workerTasks.Add(worker.RunAsync(cancellationToken));
            }

            var workerResults = await System.Threading.Tasks.Task.WhenAll(workerTasks);
            progressCts.Cancel();
            try { await progressReportingTask; } catch { }

            cancellationToken.ThrowIfCancellationRequested();

            if (workerResults.Any(success => !success) || _task.Segments.Any(s => s.Status != SegmentStatus.Completed))
            {
                throw new InvalidOperationException("Segmentlerin biri veya birkaçı indirilemedi.");
            }

            // 5. ADIM: Tamamlama ve Atomic Rename
            _task.Status = DownloadStatus.Merging;
            _repository.UpdateTaskStatus(_task);

            await writer.CommitAsync(_task.FinalFilePath, cancellationToken);

            _task.Status = DownloadStatus.Completed;
            _task.CompletedAt = DateTimeOffset.UtcNow;
            _task.DownloadedBytes = _task.TotalBytes > 0 ? _task.TotalBytes : _task.DownloadedBytes;
            _repository.UpdateTaskStatus(_task);
            _repository.SaveSegmentProgress(_task.Segments);

            stopwatch.Stop();
            _eventBus.Publish(new TaskCompletedEvent(
                _task.Id,
                _task.FinalFilePath,
                _task.TotalBytes > 0 ? _task.TotalBytes : _task.DownloadedBytes,
                stopwatch.Elapsed));

            _logger.LogInformation("İndirme başarıyla tamamlandı: {File}", _task.FileName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("İndirme duraklatıldı veya iptal edildi: {Task}", _task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İndirme başarısız oldu: {Task}", _task.Id);
            _task.Status = DownloadStatus.Failed;
            _task.LastErrorMessage = ex.Message;
            _repository.UpdateTaskStatus(_task);
            _eventBus.Publish(new TaskFailedEvent(_task.Id, ex.Message, false));
        }
        finally
        {
            if (writer != null)
            {
                await writer.DisposeAsync();
            }
        }
    }

    private async Task StartProgressReportingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(400, cancellationToken);

                double speed;
                TimeSpan? eta;
                lock (_speedCalculator)
                {
                    speed = _speedCalculator.BytesPerSecond;
                    eta = _speedCalculator.GetEta(_task.TotalBytes - _task.DownloadedBytes);
                }

                var segmentInfos = _task.Segments.Select(s => new SegmentProgressInfo(
                    s.Index,
                    s.StartByte,
                    s.EndByte,
                    s.DownloadedBytes,
                    s.Status.ToString()
                )).ToList();

                _eventBus.Publish(new ProgressUpdatedEvent(
                    _task.Id,
                    _task.DownloadedBytes,
                    _task.TotalBytes,
                    _task.ProgressPercent,
                    speed,
                    eta,
                    segmentInfos
                ));
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
