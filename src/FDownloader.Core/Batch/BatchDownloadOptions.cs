namespace FDownloader.Core.Batch;

/// <summary>
/// Toplu indirme ayrıştırma ve kuyruğa ekleme seçenekleri.
/// </summary>
public sealed class BatchDownloadOptions
{
    /// <summary>
    /// İndirmelerin kaydedileceği varsayılan hedef klasör.
    /// </summary>
    public string? DestinationDirectory { get; set; }

    /// <summary>
    /// Tekrarlayan URL'lerin filtrelenip elenmesi (varsayılan: true).
    /// </summary>
    public bool DeduplicateUrls { get; set; } = true;

    /// <summary>
    /// Sayı veya harf deseni açıldığında üretilebilecek azami URL adedi (aşırı yük koruması, varsayılan: 5000).
    /// </summary>
    public int MaxGeneratedItems { get; set; } = 5000;

    /// <summary>
    /// Özel dosya adı şablonu (Örn: "Ders_{index:D2}_{name}.mp4").
    /// </summary>
    public string? FileNamePattern { get; set; }

    /// <summary>
    /// İndirme segment sayısı (varsayılan: 8).
    /// </summary>
    public int Segments { get; set; } = 8;
}

