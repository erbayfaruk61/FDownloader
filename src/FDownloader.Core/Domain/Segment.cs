namespace FDownloader.Core.Domain;

/// <summary>
/// Bir dosya parçasının (segment) alabileceği durumlar.
/// </summary>
public enum SegmentStatus
{
    Pending = 0,
    Downloading = 1,
    Completed = 2,
    Failed = 3,
    Paused = 4
}

/// <summary>
/// Dosyanın belirli bir byte aralığını temsil eden indirme parçası.
/// Her parça bağımsız bir worker tarafından indirilir.
/// </summary>
public class Segment
{
    /// <summary>Veritabanı birincil anahtarı (0 = henüz kaydedilmedi).</summary>
    public long Id { get; set; }

    /// <summary>Ait olduğu görevin Id'si.</summary>
    public long TaskId { get; set; }

    /// <summary>Parça sıra numarası (0'dan başlar).</summary>
    public int Index { get; set; }

    /// <summary>Bu parçanın başladığı byte (dahil).</summary>
    public long StartByte { get; set; }

    /// <summary>Bu parçanın bittiği byte (dahil).</summary>
    public long EndByte { get; set; }

    /// <summary>Şimdiye kadar diske yazılmış byte sayısı. Bu değer HER ZAMAN gerçek disk yazımını yansıtmalı.</summary>
    public long DownloadedBytes { get; set; }

    /// <summary>Parçanın mevcut durumu.</summary>
    public SegmentStatus Status { get; set; } = SegmentStatus.Pending;

    /// <summary>Bu parça için yapılan yeniden deneme sayısı.</summary>
    public int RetryCount { get; set; }

    // ─── Hesaplanan özellikler ───────────────────────────────────────────

    /// <summary>Parçanın toplam boyutu (byte).</summary>
    public long TotalBytes => EndByte - StartByte + 1;

    /// <summary>Henüz indirilmemiş byte sayısı.</summary>
    public long RemainingBytes => TotalBytes - DownloadedBytes;

    /// <summary>Parçanın tamamlandığı byte offset'i (devam etmek için kullanılır).</summary>
    public long CurrentOffset => StartByte + DownloadedBytes;

    /// <summary>Parça tamamlandı mı?</summary>
    public bool IsCompleted => Status == SegmentStatus.Completed;

    /// <summary>Parça aktif olarak indiriliyor mu?</summary>
    public bool IsActive => Status == SegmentStatus.Downloading;

    public override string ToString() =>
        $"Segment[{Index}] {StartByte}-{EndByte} ({DownloadedBytes}/{TotalBytes} bytes, {Status})";
}
