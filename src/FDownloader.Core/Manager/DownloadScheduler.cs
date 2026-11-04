using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Manager;

/// <summary>
/// Tüm indirmeler tamamlandığında veya zamanlayıcı durduğunda gerçekleştirilecek eylem.
/// </summary>
public enum SchedulerCompletionAction
{
    None = 0,
    Shutdown = 1,
    Sleep = 2
}

/// <summary>
/// İndirmeleri belirli saatler arasında otomatik başlatıp durduran ve bitince sistem eylemi yürüten zamanlayıcı.
/// </summary>
public sealed class DownloadScheduler : IDisposable
{
    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<DownloadScheduler> _logger;
    private readonly Func<Task> _onStartScheduled;
    private readonly Func<Task> _onStopScheduled;
    private readonly Action<SchedulerCompletionAction> _onExecuteAction;

    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private Task? _timerTask;

    public bool IsEnabled { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? StopTime { get; set; }
    public SchedulerCompletionAction OnCompleteAction { get; set; } = SchedulerCompletionAction.None;

    private bool _hasStartedToday;
    private bool _hasStoppedToday;
    private DateOnly _lastEvaluatedDate;

    public DownloadScheduler(
        DownloadEventBus eventBus,
        ILogger<DownloadScheduler> logger,
        Func<Task> onStartScheduled,
        Func<Task> onStopScheduled,
        Action<SchedulerCompletionAction>? onExecuteAction = null)
    {
        _eventBus = eventBus;
        _logger = logger;
        _onStartScheduled = onStartScheduled;
        _onStopScheduled = onStopScheduled;
        _onExecuteAction = onExecuteAction ?? DefaultExecuteAction;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(5)); // Her 5 saniyede bir kontrol et
        _lastEvaluatedDate = DateOnly.FromDateTime(DateTime.Now);

        StartTimerLoop();
    }

    private void StartTimerLoop()
    {
        _timerTask = Task.Run(async () =>
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(_cts.Token))
                {
                    await CheckScheduleAsync(DateTimeOffset.Now);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Zamanlayıcı döngüsünde hata oluştu.");
            }
        });
    }

    /// <summary>
    /// Belirtilen zaman dilimi için zamanlayıcı kurallarını denetler ve uygular.
    /// Test edilebilirlik için public yapılmıştır.
    /// </summary>
    public async Task CheckScheduleAsync(DateTimeOffset now)
    {
        if (!IsEnabled) return;

        var currentDate = DateOnly.FromDateTime(now.DateTime);
        if (currentDate != _lastEvaluatedDate)
        {
            _hasStartedToday = false;
            _hasStoppedToday = false;
            _lastEvaluatedDate = currentDate;
        }

        var currentTime = TimeOnly.FromTimeSpan(now.TimeOfDay);

        // Başlatma saati kontrolü
        if (StartTime.HasValue && !_hasStartedToday)
        {
            if (currentTime >= StartTime.Value && currentTime <= StartTime.Value.AddMinutes(5))
            {
                _logger.LogInformation("Zamanlayıcı başlatma saati tetiklendi: {Time}", StartTime.Value);
                _hasStartedToday = true;
                await _onStartScheduled();
            }
        }

        // Durdurma saati kontrolü
        if (StopTime.HasValue && !_hasStoppedToday)
        {
            if (currentTime >= StopTime.Value && currentTime <= StopTime.Value.AddMinutes(5))
            {
                _logger.LogInformation("Zamanlayıcı durdurma saati tetiklendi: {Time}", StopTime.Value);
                _hasStoppedToday = true;
                await _onStopScheduled();

                if (OnCompleteAction != SchedulerCompletionAction.None)
                {
                    _onExecuteAction(OnCompleteAction);
                }
            }
        }
    }

    /// <summary>
    /// Tüm indirmeler tamamlandığında çağrılır (kullanıcı bitince eylem seçmişse).
    /// </summary>
    public void NotifyAllDownloadsCompleted()
    {
        if (IsEnabled && OnCompleteAction != SchedulerCompletionAction.None)
        {
            _logger.LogInformation("Tüm indirmeler tamamlandı. Eylem yürütülüyor: {Action}", OnCompleteAction);
            _onExecuteAction(OnCompleteAction);
        }
    }

    private void DefaultExecuteAction(SchedulerCompletionAction action)
    {
        switch (action)
        {
            case SchedulerCompletionAction.Shutdown:
                _logger.LogWarning("Zamanlayıcı: Bilgisayar kapatılıyor (simülasyon/komut)...");
                break;
            case SchedulerCompletionAction.Sleep:
                _logger.LogWarning("Zamanlayıcı: Bilgisayar uyku moduna alınıyor (simülasyon/komut)...");
                break;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();
        _cts.Dispose();
    }
}

