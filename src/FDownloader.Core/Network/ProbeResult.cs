namespace FDownloader.Core.Network;

/// <summary>
/// Sunucuyu inceleyerek dosya hakkında bilgi toplayan probe işleminin sonucu.
/// </summary>
public record ProbeResult
{
    /// <summary>Tüm yönlendirmeler takip edildikten sonra ulaşılan nihai URL.</summary>
    public required string FinalUrl { get; init; }

    /// <summary>Dosyanın toplam boyutu (byte). Bilinmiyorsa -1 (örn. chunked transfer).</summary>
    public long ContentLength { get; init; } = -1;

    /// <summary>Sunucu Range isteklerini destekliyor mu? (Accept-Ranges: bytes)</summary>
    public bool SupportsRange { get; init; }

    /// <summary>Belirlenen dosya adı (temizlenmemiş ham değer).</summary>
    public required string FileName { get; init; }

    /// <summary>Tespit edilen MIME türü.</summary>
    public string? ContentType { get; init; }

    /// <summary>Sunucu ETag değeri (devam ettirme doğrulaması için).</summary>
    public string? ETag { get; init; }

    /// <summary>Sunucu Last-Modified değeri.</summary>
    public string? LastModified { get; init; }

    /// <summary>Sunucu Content-Encoding kullanıyor mu? (gzip vb. — parçalı indirmeyi engeller)</summary>
    public bool HasContentEncoding { get; init; }

    /// <summary>Probe başarılı mı?</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Başarısız ise hata açıklaması.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Dosya boyutu biliniyorsa ve yeterince büyükse parçalı indirme yapılabilir.</summary>
    public bool CanUseSegmentedDownload =>
        SupportsRange && ContentLength > 0 && !HasContentEncoding;
}
