namespace FDownloader.Core.Events;

/// <summary>
/// Motor bileşenlerinin yayınladığı tüm olay türleri.
/// Arayüz bu olaylara abone olur ve UI'yı günceller.
/// </summary>

// ─── Görev Olayları ──────────────────────────────────────────────────────────

public record TaskAddedEvent(long TaskId, string FileName);

public record TaskStartedEvent(long TaskId);

public record TaskPausedEvent(long TaskId);

public record TaskResumedEvent(long TaskId);

public record TaskCancelledEvent(long TaskId);

public record TaskCompletedEvent(long TaskId, string FilePath, long TotalBytes, TimeSpan Duration);

public record TaskFailedEvent(long TaskId, string Reason, bool IsRetrying);

public record TaskRetryingEvent(long TaskId, int AttemptNumber, TimeSpan Delay);

// ─── İlerleme Olayları ───────────────────────────────────────────────────────

/// <summary>
/// Periyodik ilerleme güncellemesi. UI bu olayı yakalayıp görüntüyü yeniler.
/// Sıklığı ayarlanabilir (varsayılan 400ms).
/// </summary>
public record ProgressUpdatedEvent(
    long TaskId,
    long DownloadedBytes,
    long TotalBytes,
    double ProgressPercent,
    double SpeedBytesPerSecond,
    TimeSpan? Eta,
    IReadOnlyList<SegmentProgressInfo> Segments
);

/// <summary>Tek bir parçanın ilerleme bilgisi (segment haritası için).</summary>
public record SegmentProgressInfo(
    int Index,
    long StartByte,
    long EndByte,
    long DownloadedBytes,
    string Status
);

// ─── Bağlantı Olayları ───────────────────────────────────────────────────────

public record ProbeCompletedEvent(
    long TaskId,
    string FileName,
    long TotalBytes,
    bool SupportsRange,
    int SegmentCount
);

public record SegmentCompletedEvent(long TaskId, int SegmentIndex);

public record SegmentFailedEvent(long TaskId, int SegmentIndex, string Reason);

// ─── Sistem Olayları ─────────────────────────────────────────────────────────

public record NetworkLostEvent();

public record NetworkRestoredEvent();

public record DiskFullEvent(long TaskId, string Directory, long RequiredBytes, long AvailableBytes);

public record QueueChangedEvent(int ActiveCount, int QueuedCount);

// ─── Pano / Yakalama Olayları ───────────────────────────────────────────────

public enum ClipboardUrlType
{
    DirectFile,
    MediaVideo,
    TorrentMagnet,
    TorrentFile
}

public record ClipboardUrlDetectedEvent(string Url, ClipboardUrlType UrlType, string? Title = null);

// ─── Tarayıcı Köprüsü Olayları ───────────────────────────────────────────────

public record BrowserDownloadCapturedEvent(
    string Url,
    string? FileName = null,
    string? Referrer = null,
    string? Cookies = null,
    string? UserAgent = null,
    long? TotalBytes = null
);

public record BrowserExtensionHeartbeatEvent(
    string Version,
    DateTime Timestamp
);

public record TorrentMetadataLoadedEvent(
    long TaskId,
    string Name,
    long TotalBytes,
    string? TorrentFilePath
);

