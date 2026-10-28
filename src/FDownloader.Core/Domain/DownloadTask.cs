namespace FDownloader.Core.Domain;

/// <summary>
/// Tek bir indirme görevini temsil eden ana entity.
/// Veritabanına kalıcı olarak kaydedilir.
/// </summary>
public class DownloadTask
{
    // ─── Kimlik ───────────────────────────────────────────────────────────

    /// <summary>Veritabanı birincil anahtarı.</summary>
    public long Id { get; set; }

    /// <summary>Göreve verilen kullanıcı dostu isim (dosya adıyla aynı olabilir).</summary>
    public string Name { get; set; } = string.Empty;

    // ─── URL Bilgileri ────────────────────────────────────────────────────

    /// <summary>Kullanıcının girdiği orijinal URL.</summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>Tüm yönlendirmeler takip edildikten sonra bulunan nihai URL.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    // ─── Dosya Bilgileri ──────────────────────────────────────────────────

    /// <summary>Kaydedilecek dosya adı (temizlenmiş, güvenli).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Dosyanın kaydedileceği klasör yolu.</summary>
    public string SaveDirectory { get; set; } = string.Empty;

    /// <summary>İndirme sırasında kullanılan geçici dosya uzantısı eklenerek oluşturulan tam yol.</summary>
    public string TempFilePath { get; set; } = string.Empty;

    /// <summary>Tamamlandıktan sonraki kalıcı dosya yolu.</summary>
    public string FinalFilePath => Path.Combine(SaveDirectory, FileName);

    /// <summary>Dosyanın toplam boyutu (byte). Bilinmiyorsa -1.</summary>
    public long TotalBytes { get; set; } = -1;

    /// <summary>Dosya kategorisi (video, müzik, belge vb.).</summary>
    public DownloadCategory Category { get; set; } = DownloadCategory.Other;

    // ─── İlerleme ─────────────────────────────────────────────────────────

    /// <summary>Şimdiye kadar indirilen toplam byte. Tüm segment ilerlemelerinin toplamı.</summary>
    public long DownloadedBytes { get; set; }

    /// <summary>İndirme ilerleme yüzdesi (0.0 - 100.0).</summary>
    public double ProgressPercent =>
        TotalBytes > 0 ? Math.Min(100.0, DownloadedBytes * 100.0 / TotalBytes) : 0;

    // ─── Durum ────────────────────────────────────────────────────────────

    /// <summary>Görevin mevcut durumu.</summary>
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;

    /// <summary>Toplam yeniden deneme sayısı.</summary>
    public int RetryCount { get; set; }

    /// <summary>En son hata mesajı (kullanıcıya gösterilir).</summary>
    public string? LastErrorMessage { get; set; }

    // ─── Ayarlar ──────────────────────────────────────────────────────────

    /// <summary>Bu görev için kullanılacak paralel parça sayısı.</summary>
    public int SegmentCount { get; set; } = 8;

    /// <summary>Sunucu Range isteğini destekliyor mu?</summary>
    public bool SupportsRange { get; set; }

    /// <summary>Bu görev için kişisel hız limiti (byte/sn). 0 = sınırsız.</summary>
    public long SpeedLimitBytesPerSecond { get; set; }

    /// <summary>Öncelik sırası (düşük sayı = yüksek öncelik).</summary>
    public int Priority { get; set; } = 100;

    // ─── HTTP Meta Bilgileri ──────────────────────────────────────────────

    /// <summary>Sunucudan gelen ETag değeri (devam etme doğrulaması için).</summary>
    public string? ETag { get; set; }

    /// <summary>Sunucudan gelen Last-Modified değeri.</summary>
    public string? LastModified { get; set; }

    /// <summary>Referer başlığı (bazı siteler için zorunlu).</summary>
    public string? Referer { get; set; }

    /// <summary>User-Agent başlığı.</summary>
    public string? UserAgent { get; set; }

    /// <summary>İsteklerle gönderilecek cookie dizgesi.</summary>
    public string? Cookies { get; set; }

    // ─── Zaman Damgaları ──────────────────────────────────────────────────

    /// <summary>Görevin oluşturulma zamanı.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>İndirmenin başladığı zaman.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>İndirmenin tamamlandığı (veya başarısız olduğu) zaman.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    // ─── Parça Listesi ────────────────────────────────────────────────────

    /// <summary>Bu göreve ait indirme parçaları. Veritabanından yüklendiğinde doldurulur.</summary>
    public List<Segment> Segments { get; set; } = new();

    // ─── Yardımcı metodlar ────────────────────────────────────────────────

    /// <summary>Görev aktif durumda mı? (indiriyor veya bağlanıyor)</summary>
    public bool IsActive =>
        Status is DownloadStatus.Connecting or DownloadStatus.Downloading or DownloadStatus.Retrying;

    /// <summary>Görev tamamlandı mı veya başarısız mı?</summary>
    public bool IsFinished =>
        Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled;

    /// <summary>Devam ettirilebilir mi?</summary>
    public bool IsResumable =>
        SupportsRange && Status is DownloadStatus.Paused or DownloadStatus.Failed;

    public override string ToString() =>
        $"[{Id}] {FileName} ({Status}, {ProgressPercent:F1}%)";
}
