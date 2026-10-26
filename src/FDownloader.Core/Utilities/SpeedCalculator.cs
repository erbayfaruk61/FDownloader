namespace FDownloader.Core.Utilities;

/// <summary>
/// Hız hesaplama yardımcısı — ani dalgalanmaları yumuşatmak için hareketli ortalama kullanır.
/// </summary>
public sealed class SpeedCalculator
{
    private readonly int _windowSeconds;
    private readonly Queue<(DateTimeOffset Timestamp, long Bytes)> _samples = new();
    private long _totalBytesInWindow;

    /// <param name="windowSeconds">Hareketli ortalama pencere süresi (saniye). Varsayılan: 4 sn.</param>
    public SpeedCalculator(int windowSeconds = 4)
    {
        _windowSeconds = windowSeconds;
    }

    /// <summary>
    /// Yeni indirilen byte miktarını kaydeder.
    /// </summary>
    /// <param name="deltaBytes">Son çağrıdan bu yana indirilen byte sayısı.</param>
    public void AddSample(long deltaBytes)
    {
        if (deltaBytes <= 0) return;

        var now = DateTimeOffset.UtcNow;
        _samples.Enqueue((now, deltaBytes));
        _totalBytesInWindow += deltaBytes;

        // Pencere dışına çıkan örnekleri çıkar
        var cutoff = now.AddSeconds(-_windowSeconds);
        while (_samples.Count > 0 && _samples.Peek().Timestamp < cutoff)
        {
            var old = _samples.Dequeue();
            _totalBytesInWindow -= old.Bytes;
        }
    }

    /// <summary>
    /// Mevcut indirme hızı (byte/sn). Hareketli ortalama.
    /// </summary>
    public double BytesPerSecond
    {
        get
        {
            if (_samples.Count < 2) return 0;

            double elapsed = (DateTimeOffset.UtcNow - _samples.Peek().Timestamp).TotalSeconds;
            return elapsed > 0 ? _totalBytesInWindow / elapsed : 0;
        }
    }

    /// <summary>
    /// Tahmini kalan süre (ETA).
    /// </summary>
    /// <param name="remainingBytes">Kalan byte sayısı.</param>
    public TimeSpan? GetEta(long remainingBytes)
    {
        double bps = BytesPerSecond;
        if (bps <= 0 || remainingBytes <= 0) return null;
        return TimeSpan.FromSeconds(remainingBytes / bps);
    }

    /// <summary>Hızı sıfırla (duraklama sonrası).</summary>
    public void Reset()
    {
        _samples.Clear();
        _totalBytesInWindow = 0;
    }

    /// <summary>Okunabilir hız dizgesi (örn. "12.4 MB/s").</summary>
    public static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
        if (bytesPerSecond < 1024 * 1024 * 1024) return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        return $"{bytesPerSecond / (1024 * 1024 * 1024):F2} GB/s";
    }

    /// <summary>Okunabilir boyut dizgesi (örn. "1.2 GB").</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    /// <summary>Okunabilir ETA dizgesi (örn. "2s 30dk" veya "Hesaplanıyor...").</summary>
    public static string FormatEta(TimeSpan? eta)
    {
        if (eta is null) return "Hesaplanıyor...";
        if (eta.Value.TotalSeconds < 0) return "Tamamlanıyor...";

        if (eta.Value.TotalHours >= 1)
            return $"{(int)eta.Value.TotalHours}s {eta.Value.Minutes}dk";
        if (eta.Value.TotalMinutes >= 1)
            return $"{(int)eta.Value.TotalMinutes}dk {eta.Value.Seconds}sn";
        return $"{(int)eta.Value.TotalSeconds}sn";
    }
}
