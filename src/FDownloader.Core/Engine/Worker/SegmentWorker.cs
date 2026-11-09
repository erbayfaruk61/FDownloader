using FDownloader.Core.Domain;
using FDownloader.Core.Engine.Exceptions;
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
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _stallTimeout;
    private readonly string? _userAgent;
    private readonly string? _referer;
    private readonly string? _cookies;
    private readonly SpeedLimiter? _speedLimiter;

    // İlerleme callback (DownloadedBytes delta olarak gelir)
    public event Action<long>? BytesWritten;

    private const int ReadBufferSize = 65536; // 64 KB — ağ okuma tamponu

    private readonly Action? _onRateLimitDetected;
    private int _rateLimitRetryCount = 0;

    public SegmentWorker(
        Segment segment,
        string url,
        FileWriter writer,
        HttpClient http,
        ILogger<SegmentWorker> logger,
        int maxRetries = 5,
        TimeSpan? connectTimeout = null,
        TimeSpan? stallTimeout = null,
        string? userAgent = null,
        string? referer = null,
        string? cookies = null,
        SpeedLimiter? speedLimiter = null,
        Action? onRateLimitDetected = null)
    {
        _segment = segment;
        _url = url;
        _writer = writer;
        _http = http;
        _logger = logger;
        _maxRetries = maxRetries;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(15);
        _stallTimeout = stallTimeout ?? TimeSpan.FromSeconds(30);
        _userAgent = userAgent;
        _referer = referer;
        _cookies = cookies;
        _speedLimiter = speedLimiter;
        _onRateLimitDetected = onRateLimitDetected;
        _speedCalc = new SpeedCalculator();
    }

    /// <summary>
    /// Segmenti indirir. Hata sınıflandırmasına göre uygun tepkiyi verir:
    /// - Geçici hatalar (Transient): Üstel backoff + jitter ile yeniden dener.
    /// - 429 TooManyRequests: Retry-After başlığına uyar.
    /// - Kalıcı hatalar (404, 410, 401, 403, Disk/İzin): Yeniden denemez, durur.
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
            catch (Exception rawEx)
            {
                var classified = DownloadErrorClassifier.ClassifyException(rawEx);

                // Kalıcı hatalar: 404, 410, 401, 403
                if (classified.Kind is DownloadErrorKind.PermanentNotFound or DownloadErrorKind.PermanentAuth)
                {
                    _segment.Status = SegmentStatus.Failed;
                    _logger.LogError(rawEx, "Segment {Index} kalıcı hata ({Kind}): {Message}", _segment.Index, classified.Kind, classified.Message);
                    throw classified;
                }

                // Disk dolu veya erişim izni yok: Duraklat
                if (classified.Kind == DownloadErrorKind.DiskFullOrPermission)
                {
                    _segment.Status = SegmentStatus.Paused;
                    _logger.LogError(rawEx, "Segment {Index} disk veya erişim hatası: {Message}", _segment.Index, classified.Message);
                    throw classified;
                }

                _segment.RetryCount++;

                // 429 TooManyRequests: Retry-After başlığına uy
                TimeSpan delay;
                if (classified.Kind == DownloadErrorKind.TooManyRequests)
                {
                    _onRateLimitDetected?.Invoke();
                    delay = classified.RetryAfter ?? TimeSpan.FromSeconds(10);
                    _logger.LogWarning("Segment {Index} 429 Rate Limit aldı. Retry-After süresi ({Delay}sn) bekleniyor",
                        _segment.Index, delay.TotalSeconds);

                    // 429 bir ağ çökmesi değil, geçici hız sınırıdır; attempt hakkını tüketme
                    if (_rateLimitRetryCount < 10)
                    {
                        _rateLimitRetryCount++;
                        attempt = Math.Max(-1, attempt - 1);
                    }
                }
                else
                {
                    if (attempt >= _maxRetries)
                    {
                        _segment.Status = SegmentStatus.Failed;
                        _logger.LogError(rawEx, "Segment {Index} maksimum deneme ({Max}) aşıldı", _segment.Index, _maxRetries);
                        throw classified;
                    }

                    delay = ExponentialBackoff(attempt);
                    _logger.LogWarning(rawEx,
                        "Segment {Index} geçici hata (deneme {Attempt}/{Max}), {Delay}sn sonra yeniden deniyor",
                        _segment.Index, attempt + 1, _maxRetries, delay.TotalSeconds);
                }

                await Task.Delay(delay, ct);
            }
        }

        return false;
    }

    private async Task DownloadSegmentAsync(CancellationToken ct)
    {
        // Kaldığı yerden devam için offset hesapla
        long startFrom = _segment.CurrentOffset;

        using var request = BuildRequest(startFrom, _segment.EndByte);
        HttpResponseMessage response;

        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            connectCts.CancelAfter(_connectTimeout);
            try
            {
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Segment {_segment.Index} sunucu bağlantı zaman aşımına uğradı (Connect timeout: {_connectTimeout.TotalSeconds}s).");
            }
        }

        // 416: İstenen aralık geçersiz — segment zaten tamamlanmış olabilir
        if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            _logger.LogWarning("Segment {Index}: 416 Range Not Satisfiable — zaten tamamlanmış sayılıyor", _segment.Index);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw DownloadErrorClassifier.ClassifyResponse(response);
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

            // Dinamik parça bölme ve sınır aşımı koruması (çift yazımı kesin olarak önler)
            int effectiveBytesToWrite = bytesRead;
            if (_segment.EndByte > 0 && _segment.EndByte < long.MaxValue)
            {
                long maxAllowed = _segment.EndByte - writeOffset + 1;
                if (maxAllowed <= 0)
                {
                    break;
                }
                if (effectiveBytesToWrite > maxAllowed)
                {
                    effectiveBytesToWrite = (int)maxAllowed;
                }
            }

            // Diske yaz
            await _writer.WriteAtAsync(writeOffset, buffer, effectiveBytesToWrite, ct);

            // İlerlemeyi güncelle — DISKE YAZILMIŞ miktarı güncelle
            _segment.DownloadedBytes += effectiveBytesToWrite;
            writeOffset += effectiveBytesToWrite;

            _speedCalc.AddSample(effectiveBytesToWrite);
            BytesWritten?.Invoke(effectiveBytesToWrite);

            // Parça bittiyse veya dinamik olarak küçültülen sınıra ulaştıysa döngüyü sonlandır
            if (_segment.EndByte > 0 && _segment.EndByte < long.MaxValue && writeOffset > _segment.EndByte)
            {
                break;
            }
        }

        // Segmentin eksiksiz indirildiğini kesin olarak doğrula (erken bağlantı kopmalarını önle)
        if (_segment.EndByte > 0 && _segment.EndByte < long.MaxValue && _segment.TotalBytes > 0)
        {
            if (_segment.DownloadedBytes < _segment.TotalBytes)
            {
                throw new IOException($"Segment {_segment.Index} akışı sunucu tarafından beklenenden erken kapatıldı! İndirilen: {_segment.DownloadedBytes:N0}/{_segment.TotalBytes:N0} byte");
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
        {
            // Cross-domain veya HTTPS->HTTP kontrolü: Eğer referer varsa ve hedef farklı bir domain ise çerezleri gönderme
            bool allowCookies = true;
            if (!string.IsNullOrEmpty(_referer) &&
                Uri.TryCreate(_referer, UriKind.Absolute, out var refUri) &&
                Uri.TryCreate(_url, UriKind.Absolute, out var targetUri))
            {
                allowCookies = Network.NetworkSecurityPolicy.ShouldPreserveSensitiveHeaders(refUri, targetUri);
            }

            if (allowCookies)
            {
                request.Headers.TryAddWithoutValidation("Cookie", _cookies);
            }
            else
            {
                _logger.LogWarning("Segment {Index}: Farklı hedef domaine istek atılıyor, çerezler güvenlik nedeniyle çıkarıldı.", _segment.Index);
            }
        }

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
