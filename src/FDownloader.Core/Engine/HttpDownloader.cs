using System.Diagnostics;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine.Exceptions;
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
public sealed class HttpDownloader : IDisposable
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

                // FAT32 dosya sistemi 4 GB sınırı kontrolü
                if (_task.TotalBytes > 0 && DiskSpaceChecker.ExceedsFat32Limit(_task.SaveDirectory, _task.TotalBytes))
                {
                    throw new NotSupportedException($"Hedef sürücü dosya sistemi (FAT32) 4 GB'tan büyük dosyaları desteklemez ({_task.TotalBytes:N0} byte).");
                }

                // Disk alanı kontrolü
                if (_task.TotalBytes > 0 && !DiskSpaceChecker.HasEnoughSpace(_task.SaveDirectory, _task.TotalBytes))
                {
                    var availableBytes = DiskSpaceChecker.GetAvailableSpace(_task.SaveDirectory);
                    _eventBus.Publish(new DiskFullEvent(_task.Id, _task.SaveDirectory, _task.TotalBytes, availableBytes));
                    throw new IOException($"Yetersiz disk alanı! Gerekli: {_task.TotalBytes:N0} byte, Mevcut: {availableBytes:N0} byte");
                }

                _eventBus.Publish(new ProbeCompletedEvent(
                    _task.Id,
                    _task.FileName,
                    _task.TotalBytes,
                    _task.SupportsRange,
                    _task.SegmentCount));
            }
            else if (_task.Segments.Any(s => s.DownloadedBytes > 0))
            {
                // 1.5. ADIM: Resume Öncesi Dosya Değişim Kontrolü (P2-03)
                // Sunucudaki dosya değişmiş mi kontrol et (ETag veya LastModified)
                try
                {
                    var prober = new DownloadProber(
                        _httpClient,
                        _loggerFactory.CreateLogger<DownloadProber>());

                    var currentProbe = await prober.ProbeAsync(
                        _task.OriginalUrl,
                        _task.Referer,
                        _task.Cookies,
                        _task.UserAgent,
                        cancellationToken);

                    bool etagChanged = !string.IsNullOrEmpty(_task.ETag) &&
                                       !string.IsNullOrEmpty(currentProbe.ETag) &&
                                       !string.Equals(_task.ETag.Trim('\"'), currentProbe.ETag.Trim('\"'), StringComparison.OrdinalIgnoreCase);

                    bool modifiedChanged = !string.IsNullOrEmpty(_task.LastModified) &&
                                           !string.IsNullOrEmpty(currentProbe.LastModified) &&
                                           !string.Equals(_task.LastModified, currentProbe.LastModified, StringComparison.OrdinalIgnoreCase);

                    bool sizeChanged = _task.TotalBytes > 0 && currentProbe.ContentLength > 0 && _task.TotalBytes != currentProbe.ContentLength;

                    if (etagChanged || modifiedChanged || sizeChanged)
                    {
                        _logger.LogWarning("Resume öncesi dosya sunucuda değişmiş tespit edildi! Task {Id}. Eski ETag: {OldEtag}, Yeni ETag: {NewEtag}",
                            _task.Id, _task.ETag, currentProbe.ETag);

                        _eventBus.Publish(new FileChangedOnServerEvent(
                            _task.Id,
                            _task.ETag,
                            currentProbe.ETag,
                            _task.LastModified,
                            currentProbe.LastModified));

                        throw new InvalidOperationException("Dosya sunucuda değişmiş. Resume işlemi iptal edildi.");
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Resume öncesi dosya doğrulama probe'u çalıştırılamadı, mevcut verilerle devam ediliyor.");
                }
            }

            if (string.IsNullOrEmpty(_task.FinalUrl))
            {
                _task.FinalUrl = _task.OriginalUrl;
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
            if (string.IsNullOrEmpty(_task.TempFilePath))
            {
                if (string.IsNullOrEmpty(_task.SaveDirectory))
                {
                    _task.SaveDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
                }
                Directory.CreateDirectory(_task.SaveDirectory);
                _task.TempFilePath = Path.Combine(_task.SaveDirectory, _task.FileName + ".fdown");
            }

            if (_task.TotalBytes > 0)
            {
                if (DiskSpaceChecker.ExceedsFat32Limit(_task.SaveDirectory, _task.TotalBytes))
                {
                    throw new NotSupportedException($"Hedef sürücü dosya sistemi (FAT32) 4 GB'tan büyük dosyaları desteklemez ({_task.TotalBytes:N0} byte).");
                }

                if (!DiskSpaceChecker.HasEnoughSpace(_task.SaveDirectory, _task.TotalBytes))
                {
                    var available = DiskSpaceChecker.GetAvailableSpace(_task.SaveDirectory);
                    _eventBus.Publish(new DiskFullEvent(_task.Id, _task.SaveDirectory, _task.TotalBytes, available));
                    throw new IOException($"Yetersiz disk alanı! Gerekli: {_task.TotalBytes:N0} byte, Mevcut: {available:N0} byte");
                }
            }

            writer = new FileWriter(
                _task.TempFilePath,
                _loggerFactory.CreateLogger<FileWriter>());

            await writer.InitializeAsync(_task.TotalBytes, cancellationToken);

            _task.Status = DownloadStatus.Downloading;
            _repository.UpdateTaskStatus(_task);

            // 4. ADIM: Segment Worker'larının Çalıştırılması (Dinamik Parça Bölme / Work-Stealing)
            var workerLogger = _loggerFactory.CreateLogger<SegmentWorker>();
            var workerTasks = new List<Task<bool>>();

            // İlerleme yayınlama döngüsü için timer
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressReportingTask = StartProgressReportingLoopAsync(progressCts.Token);

            int concurrency = Math.Clamp(_task.SegmentCount > 0 ? _task.SegmentCount : 8, 1, 16);
            if (!_task.SupportsRange || _task.TotalBytes <= 0)
            {
                concurrency = 1; // Range desteksiz veya boyutu bilinmeyen indirmeler tek bağlantı
            }

            for (int workerId = 0; workerId < concurrency; workerId++)
            {
                workerTasks.Add(System.Threading.Tasks.Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Segment? segmentToDownload = null;

                        lock (_task.Segments)
                        {
                            // 1. Önce henüz tamamlanmamış ve başlanmamış (Pending / Paused) parça var mı?
                            segmentToDownload = _task.Segments.FirstOrDefault(s => s.Status != SegmentStatus.Completed && s.Status != SegmentStatus.Downloading);
                            if (segmentToDownload != null)
                            {
                                segmentToDownload.Status = SegmentStatus.Downloading;
                            }
                            else if (_task.SupportsRange && _task.TotalBytes > 0)
                            {
                                // 2. Boşta kalan (idle) worker: Aktif en büyük parçayı böl ve yardım et (Work-Stealing)
                                segmentToDownload = SegmentPlanner.SplitLargestActiveSegment(_task.Segments, minSplitSizeBytes: 512 * 1024);
                                if (segmentToDownload != null)
                                {
                                    segmentToDownload.Status = SegmentStatus.Downloading;
                                    try
                                    {
                                        _repository.SaveSegmentProgress(new[] { segmentToDownload });
                                    }
                                    catch { }
                                    _logger.LogInformation(
                                        "Dinamik parça bölme: Yeni Segment {Index} [{Start}-{End}] oluşturuldu",
                                        segmentToDownload.Index, segmentToDownload.StartByte, segmentToDownload.EndByte);
                                }
                            }
                        }

                        if (segmentToDownload == null)
                        {
                            // İndirilecek veya bölünecek parça kalmadı, bu worker tamamlandı
                            break;
                        }

                        var worker = new SegmentWorker(
                            segmentToDownload,
                            _task.FinalUrl,
                            writer,
                            _httpClient,
                            workerLogger,
                            maxRetries: 5,
                            connectTimeout: TimeSpan.FromSeconds(15),
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

                        bool success = await worker.RunAsync(cancellationToken);
                        if (!success && !cancellationToken.IsCancellationRequested)
                        {
                            return false;
                        }
                    }

                    return true;
                }, cancellationToken));
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
            try
            {
                if (writer != null)
                {
                    await writer.FlushAsync();
                }
                _repository.SaveSegmentProgress(_task.Segments);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "İndirme duraklatılırken segment ilerleme checkpoint'i kaydedilemedi: {Task}", _task.Id);
            }
        }
        catch (Exception ex)
        {
            var downloadEx = ex as DownloadException ??
                             (ex as AggregateException)?.InnerExceptions.OfType<DownloadException>().FirstOrDefault() ??
                             (ex.InnerException as DownloadException);

            if (downloadEx != null && downloadEx.Kind == DownloadErrorKind.DiskFullOrPermission)
            {
                _logger.LogError(downloadEx, "Disk veya erişim hatası nedeniyle indirme duraklatıldı: {Task}", _task.Id);
                _task.Status = DownloadStatus.Paused;
                _task.LastErrorMessage = downloadEx.Message;
                _repository.UpdateTaskStatus(_task);
                _eventBus.Publish(new TaskFailedEvent(_task.Id, downloadEx.Message, false));
            }
            else
            {
                string msg = downloadEx?.Message ?? ex.Message;
                bool isRetrying = downloadEx?.Kind == DownloadErrorKind.Transient || downloadEx?.Kind == DownloadErrorKind.TooManyRequests;

                _logger.LogError(ex, "İndirme başarısız oldu: {Task} ({Msg})", _task.Id, msg);
                _task.Status = DownloadStatus.Failed;
                _task.LastErrorMessage = msg;
                _repository.UpdateTaskStatus(_task);
                _eventBus.Publish(new TaskFailedEvent(_task.Id, msg, isRetrying));
            }
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
                // 400ms aralıklarla hem UI'ya bildir hem de DB'ye checkpoint al
                await System.Threading.Tasks.Task.Delay(400, cancellationToken);

                // Periyodik checkpoint: diske flush ve DB'ye kaydet
                try
                {
                    _repository.SaveSegmentProgress(_task.Segments);
                }
                catch { }

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

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
