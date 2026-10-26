using System.Diagnostics;

namespace FDownloader.Core.Engine;

/// <summary>
/// İndirme hızını sınırlamak için Token Bucket algoritması tabanlı hız sınırlayıcı.
/// Global veya görev bazında hız sınırlamayı thread-safe olarak uygular.
/// </summary>
public sealed class SpeedLimiter
{
    private long _limitBytesPerSecond; // 0 = sınırsız
    private double _availableTokens;
    private long _lastRefillTimestamp;
    private readonly object _lock = new();

    public SpeedLimiter(long limitBytesPerSecond = 0)
    {
        _limitBytesPerSecond = Math.Max(0, limitBytesPerSecond);
        _availableTokens = _limitBytesPerSecond;
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Saniye başına izin verilen maksimum byte (0 = sınırsız).
    /// </summary>
    public long LimitBytesPerSecond
    {
        get => _limitBytesPerSecond;
        set
        {
            lock (_lock)
            {
                _limitBytesPerSecond = Math.Max(0, value);
                _availableTokens = _limitBytesPerSecond;
                _lastRefillTimestamp = Stopwatch.GetTimestamp();
            }
        }
    }

    /// <summary>
    /// Belirtilen byte kadar veriyi okumak/yazmak için hız limitine uygun şekilde bekler.
    /// </summary>
    public async ValueTask ThrottleAsync(int bytes, CancellationToken ct = default)
    {
        if (_limitBytesPerSecond <= 0 || bytes <= 0)
            return;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            double delayMs = 0;
            lock (_lock)
            {
                if (_limitBytesPerSecond <= 0)
                    return;

                var now = Stopwatch.GetTimestamp();
                var elapsedSeconds = (double)(now - _lastRefillTimestamp) / Stopwatch.Frequency;
                if (elapsedSeconds > 0)
                {
                    _availableTokens = Math.Min(_limitBytesPerSecond, _availableTokens + elapsedSeconds * _limitBytesPerSecond);
                    _lastRefillTimestamp = now;
                }

                if (_availableTokens >= bytes)
                {
                    _availableTokens -= bytes;
                    return;
                }

                double needed = bytes - _availableTokens;
                delayMs = (needed / _limitBytesPerSecond) * 1000.0;
            }

            if (delayMs > 0)
            {
                int waitTime = Math.Clamp((int)delayMs, 10, 250);
                await Task.Delay(waitTime, ct);
            }
        }
    }
}
