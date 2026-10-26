using FDownloader.Core.Domain;
using FDownloader.Core.Engine.Writer;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Engine.Worker;

/// <summary>
/// Tek bir segment (byte aralığı) için bağımsız indirme çalışanı.
/// Kendi yeniden deneme sayacını yönetir.
/// </summary>
public sealed class SegmentWorker
{
    private readonly Segment _segment;
    private readonly string _url;
    private readonly FileWriter _writer;
    private readonly HttpClient _http;
    private readonly ILogger<SegmentWorker> _logger;
    private readonly SpeedCalculator _speedCalc;

    // Worker ayarları
    private readonly int _maxRetries;
    private readonly TimeSpan _stallTimeout;
    private readonly string? _userAgent;
    private readonly string? _referer;
    private readonly string? _cookies;
    private readonly SpeedLimiter? _speedLimiter;

    // İlerleme callback (DownloadedBytes delta olarak gelir)
    public event Action<long>? BytesWritten;

    private const int ReadBufferSize = 65536; // 64 KB — ağ okuma tamponu

    public SegmentWorker(
        Segment segment,
        string url,
        FileWriter writer,
        HttpClient http,
        ILogger<SegmentWorker> logger,
        int maxRetries = 5,
        TimeSpan? stallTimeout = null,
        string? userAgent = null,
        string? referer = null,
        string? cookies = null,
        SpeedLimiter? speedLimiter = null)
    {
        _segment = segment;
        _url = url;
        _writer = writer;
        _http = http;
        _logger = logger;
        _maxRetries = maxRetries;
        _stallTimeout = stallTimeout ?? TimeSpan.FromSeconds(30);
        _userAgent = userAgent;
        _referer = referer;
        _cookies = cookies;
        _speedLimiter = speedLimiter;
        _speedCalc = new SpeedCalculator();
    }

    /// <summary>
    /// Segmenti indirir. Hata durumunda üstel backoff ile yeniden dener.
    /// </summary>
    /// <returns>Başarılıysa true.</returns>
    public async Task<bool> RunAsync(CancellationToken ct)
    {
        _segment.Status = SegmentStatus.Downloading;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            if (ct.IsCancellationRequested) return false;

            try
            {
                await DownloadSegmentAsync(ct);
                _segment.Status = SegmentStatus.Completed;
                _logger.LogInformation(
                    "Segment {Index} tamamlandı: {Bytes} byte", _segment.Index, _segment.TotalBytes);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _segment.Status = SegmentStatus.Paused;
                return false;
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                _segment.RetryCount++;
                var delay = ExponentialBackoff(attempt);
                _logger.LogWarning(ex,
                    "Segment {Index} hata (deneme {Attempt}/{Max}), {Delay}sn sonra yeniden deniyor",
                    _segment.Index, attempt + 1, _maxRetries, delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _segment.Status = SegmentStatus.Failed;
                _logger.LogError(ex, "Segment {Index} kalıcı olarak başarısız", _segment.Index);
                return false;
            }
        }

        return false;
    }

    private async Task DownloadSegmentAsync(CancellationToken ct)
    {
        // Kaldığı yerden devam için offset hesapla
        long startFrom = _segment.CurrentOffset;

        using var request = BuildRequest(startFrom, _segment.EndByte);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        // 206 Partial Content bekliyoruz; 200 de olabilir (range desteksiz, ama okumaya devam)
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Sunucu {(int)response.StatusCode} döndü. Segment {_segment.Index}");
        }

        // 416: İstenen aralık geçersiz — segment zaten tamamlanmış olabilir
        if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            _logger.LogWarning("Segment {Index}: 416 Range Not Satisfiable — zaten tamamlanmış sayılıyor", _segment.Index);
            return;
        }

        // 200 OK geldiğinde (sunucu Range desteklemiyor) sıfırdan başla; 206 Partial Content ise kaldığı offset'ten devam et
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            startFrom = 0;
            _segment.DownloadedBytes = 0;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[ReadBufferSize];
        var writeOffset = startFrom;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // Stall timeout ile okuma
            int bytesRead;
            using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            stallCts.CancelAfter(_stallTimeout);

            try
            {
                bytesRead = await stream.ReadAsync(buffer, stallCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Segment {_segment.Index} veri akışı durdu (stall).");
            }

            if (bytesRead == 0) break; // Akış bitti

            // Hız limiti aktifse bekle
            if (_speedLimiter != null)
            {
                await _speedLimiter.ThrottleAsync(bytesRead, ct);
            }

            // Diske yaz
            await _writer.WriteAtAsync(writeOffset, buffer, bytesRead, ct);

            // İlerlemeyi güncelle — DISKE YAZILMIŞdaki miktarı güncelle
            _segment.DownloadedBytes += bytesRead;
            writeOffset += bytesRead;

            _speedCalc.AddSample(bytesRead);
            BytesWritten?.Invoke(bytesRead);

            // Beklenen sınırı aştıysak dur (sunucu fazla veri göndermiş olabilir)
            if (_segment.EndByte > 0 && _segment.EndByte < long.MaxValue && writeOffset > _segment.EndByte + 1)
            {
                _logger.LogWarning("Segment {Index} beklenen sınırı aştı, durduruluyor", _segment.Index);
                break;
            }
        }
    }

    private HttpRequestMessage BuildRequest(long from, long to)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _url);

        var userAgent = !string.IsNullOrEmpty(_userAgent) ? _userAgent : Network.NetworkConstants.DefaultUserAgent;
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (!string.IsNullOrEmpty(_referer))
            request.Headers.TryAddWithoutValidation("Referer", _referer);
        if (!string.IsNullOrEmpty(_cookies))
            request.Headers.TryAddWithoutValidation("Cookie", _cookies);

        // Sıkıştırma isteme
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");

        // Range başlığı (to == long.MaxValue ise açık uçlu range)
        if (to == long.MaxValue)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, null);
        else
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);

        return request;
    }

    /// <summary>Üstel backoff + jitter hesabı.</summary>
    private static TimeSpan ExponentialBackoff(int attempt)
    {
        double baseSeconds = Math.Min(Math.Pow(2, attempt) * 2, 60); // Maks 60sn
        double jitter = Random.Shared.NextDouble() * 2;              // 0-2sn rastgele sapma
        return TimeSpan.FromSeconds(baseSeconds + jitter);
    }
}
