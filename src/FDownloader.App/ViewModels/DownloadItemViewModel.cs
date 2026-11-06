using CommunityToolkit.Mvvm.ComponentModel;

using FDownloader.Core.Config;

using FDownloader.Core.Domain;

using FDownloader.Core.Utilities;



namespace FDownloader.App.ViewModels;



/// <summary>

/// İndirme listesindeki tek bir satırı temsil eden ViewModel.

/// Motor olayları geldiğinde UI thread'de güncellenir.

/// </summary>

public partial class DownloadItemViewModel : ViewModelBase

{

    [ObservableProperty] private long _id;

    [ObservableProperty] private string _fileName = string.Empty;

    [ObservableProperty] private string _originalUrl = string.Empty;

    [ObservableProperty] private long _totalBytes;

    [ObservableProperty] private long _downloadedBytes;

    [ObservableProperty] private double _progressPercent;

    [ObservableProperty] private double _speedBytesPerSecond;

    [ObservableProperty] private double _maxSpeedBytesPerSecond;

    [ObservableProperty] private double _minSpeedBytesPerSecond;

    [ObservableProperty] private string _etaDisplay = "-";

    [ObservableProperty] private DownloadStatus _status;

    [ObservableProperty] private DownloadCategory _category;

    [ObservableProperty] private DateTimeOffset _createdAt;

    [ObservableProperty] private string? _lastErrorMessage;

    [ObservableProperty] private int _seedsCount;

    [ObservableProperty] private int _leechesCount;

    [ObservableProperty] private double _uploadSpeedBytesPerSecond;

    [ObservableProperty] private string _savePath = string.Empty;

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private bool _isStalled;

    [ObservableProperty] private bool _isSequentialDownload;



    public int ConsecutiveZeroSpeedCount { get; set; }



    // Segment haritası için (her parçanın ilerleme yüzdesi)

    [ObservableProperty] private IReadOnlyList<double> _segmentProgresses = Array.Empty<double>();



    // ─── Hesaplanan özellikler ────────────────────────────────────────────



    public bool IsTorrent => Category == DownloadCategory.Torrent;

    public bool IsMedia => Category is DownloadCategory.Video or DownloadCategory.Music;

    public bool IsArchiveFile => Status == DownloadStatus.Completed &&

        (ArchiveExtractorService.IsArchive(FileName) || ArchiveExtractorService.IsArchive(SavePath));



    public bool IsDownloadedTorrentFile => Status == DownloadStatus.Completed &&

        Category != DownloadCategory.Torrent &&

        (FileName.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ||

         SavePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase));



    /// <summary>

    /// TotalBytes bilinmiyorken (streaming indirme) progress bar'ı belirsiz modda göster.

    /// </summary>

    public bool IsProgressIndeterminate => IsActive && TotalBytes <= 0;



    // Satır arka planı ve çerçeve vurgusu (Stall veya hata durumunda belirgin sarı-turuncu)

    public string RowBackground => (IsStalled || Status == DownloadStatus.Failed)

        ? "#22F59E0B"

        : "Transparent";



    public string RowBorderBrush => (IsStalled || Status == DownloadStatus.Failed)

        ? "#F59E0B"

        : "Transparent";



    // En soldaki durum ikonu (Tamamlandıysa yeşil tik, kopma/hata ise sarı ünlem, vb.)

    public string StatusBadgeIcon => (Status, IsStalled) switch

    {

        (DownloadStatus.Completed, _) => "✓",

        (_, true) => "⚠️",

        (DownloadStatus.Failed, _) => "⚠️",

        (DownloadStatus.Downloading, _) => "↓",

        (DownloadStatus.Connecting, _) => "⏳",

        (DownloadStatus.Paused, _) => "⏸",

        (DownloadStatus.Merging, _) => "⚙",

        (DownloadStatus.Cancelled, _) => "⊘",

        _ => "•"

    };



    public string StatusBadgeColor => (Status, IsStalled) switch

    {

        (DownloadStatus.Completed, _) => "#10B981", // Yeşil

        (_, true) => "#F59E0B",                     // Belirgin Sarı-Turuncu (Bağlantı koptu / Hız sıfırlandı)

        (DownloadStatus.Failed, _) => "#F59E0B",    // Belirgin Sarı-Turuncu (Bağlantı kesildi)

        (DownloadStatus.Downloading, _) => "#3B82F6", // Mavi

        (DownloadStatus.Paused, _) => "#F59E0B",    // Turuncu/Sarı

        (DownloadStatus.Connecting, _) => "#94A3B8", // Gri

        (DownloadStatus.Merging, _) => "#8B5CF6",   // Mor

        _ => "#64748B"

    };



    public string TotalBytesDisplay

    {

        get

        {

            if (TotalBytes > 0) return SpeedCalculator.FormatBytes(TotalBytes);

            if (DownloadedBytes > 0) return $"{DownloadedBytesDisplay} (Akış)";

            return IsTorrent ? "Bilinmiyor" : "-";

        }

    }

    public string DownloadedBytesDisplay => SpeedCalculator.FormatBytes(DownloadedBytes);

    public string SpeedDisplay => SpeedBytesPerSecond > 0 ? SpeedCalculator.FormatSpeed(SpeedBytesPerSecond) : "-";

    public string MaxSpeedDisplay => MaxSpeedBytesPerSecond > 0 ? SpeedCalculator.FormatSpeed(MaxSpeedBytesPerSecond) : "-";

    public string MinSpeedDisplay => MinSpeedBytesPerSecond > 0 ? SpeedCalculator.FormatSpeed(MinSpeedBytesPerSecond) : "0 B/s";

    public string UploadSpeedDisplay => UploadSpeedBytesPerSecond > 0 ? "↑ " + SpeedCalculator.FormatSpeed(UploadSpeedBytesPerSecond) : "-";

    public string PeersDisplay => IsTorrent ? $"{SeedsCount} seed / {LeechesCount} peer" : "-";



    public static int MaxHistoryBars { get; set; } = 80;



    public System.Collections.ObjectModel.ObservableCollection<double> SpeedHistory { get; } = new();



    public DownloadItemViewModel()

    {

        for (int i = 0; i < MaxHistoryBars; i++)

        {

            SpeedHistory.Add(2.0);

        }

    }



    // ─── Hibrit Başlat / Duraklat Özellikleri ──────────────────────────────

    private static FDownloader.Core.Localization.LocalizationService Loc => FDownloader.Core.Localization.LocalizationService.Instance;



    public string HybridActionIcon => (IsActive && !IsStalled) ? "⏸" : "▶";

    public string HybridActionText => (IsActive && !IsStalled) 

        ? Loc["Common.Pause"] 

        : (IsCompleted ? Loc["Common.Restart"] : Loc["Common.Resume"]);

    public string HybridActionMenuHeader => (IsActive && !IsStalled) 

        ? $"⏸ {Loc["Common.Pause"]}" 

        : (IsCompleted ? $"🔄 {Loc["Common.Restart"]}" : $"▶ {Loc["Common.Resume"]}");

    public string HybridActionTooltip => (IsActive && !IsStalled) 

        ? Loc["Common.Pause"] 

        : (IsCompleted ? Loc["Common.Restart"] : Loc["Common.Resume"]);

    public string HybridActionColor => (IsActive && !IsStalled) ? "#F59E0B" : ((IsStalled || Status == DownloadStatus.Failed) ? "#F59E0B" : "#10B981");



    public bool IsTorrentMetaSearching => IsTorrent && (TotalBytes == 0 || FileName.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase));



    public string StatusDisplay

    {

        get

        {

            if (IsStalled)

            {

                return Loc["Common.ConnectionLost"];

            }

            if (Status == DownloadStatus.Failed)

            {

                return Loc["Common.ConnectionClosed"];

            }

            if (IsTorrentMetaSearching && Status == DownloadStatus.Downloading)

            {

                return Loc["Common.SearchingMeta"];

            }

            return Status switch

            {

                DownloadStatus.Queued => Loc["Common.Queued"],

                DownloadStatus.Connecting => Loc["Common.Connecting"],

                DownloadStatus.Downloading => Loc["Common.Downloading"],

                DownloadStatus.Paused => Loc["Common.Paused"],

                DownloadStatus.Merging => Loc["Common.Merging"],

                DownloadStatus.Verifying => Loc["Common.Verifying"],

                DownloadStatus.Completed => Loc["Common.Completed"],

                DownloadStatus.Retrying => Loc["Common.Retrying"],

                DownloadStatus.Cancelled => Loc["Common.Cancelled"],

                _ => Status.ToString()

            };

        }

    }



    public void RefreshTranslations()

    {

        OnPropertyChanged(nameof(StatusDisplay));

        OnPropertyChanged(nameof(HybridActionText));

        OnPropertyChanged(nameof(HybridActionTooltip));

        OnPropertyChanged(nameof(HybridActionMenuHeader));

        OnPropertyChanged(nameof(ResumeActionText));

        OnPropertyChanged(nameof(ResumeActionTooltip));

    }



    partial void OnIsStalledChanged(bool value)

    {

        OnPropertyChanged(nameof(StatusBadgeIcon));

        OnPropertyChanged(nameof(StatusBadgeColor));

        OnPropertyChanged(nameof(StatusDisplay));

        OnPropertyChanged(nameof(RowBackground));

        OnPropertyChanged(nameof(RowBorderBrush));

        OnPropertyChanged(nameof(HybridActionIcon));

        OnPropertyChanged(nameof(HybridActionText));

        OnPropertyChanged(nameof(HybridActionMenuHeader));

        OnPropertyChanged(nameof(HybridActionTooltip));

        OnPropertyChanged(nameof(HybridActionColor));

        OnPropertyChanged(nameof(CanPause));

        OnPropertyChanged(nameof(CanResume));

        OnPropertyChanged(nameof(CanRestart));

        OnPropertyChanged(nameof(CanRefreshLink));

        OnPropertyChanged(nameof(CanVerifyChecksum));

        OnPropertyChanged(nameof(ResumeActionText));

        OnPropertyChanged(nameof(ResumeActionIcon));

    }



    partial void OnStatusChanged(DownloadStatus value)

    {

        OnPropertyChanged(nameof(StatusDisplay));

        OnPropertyChanged(nameof(StatusBadgeIcon));

        OnPropertyChanged(nameof(StatusBadgeColor));

        OnPropertyChanged(nameof(RowBackground));

        OnPropertyChanged(nameof(RowBorderBrush));

        OnPropertyChanged(nameof(IsActive));

        OnPropertyChanged(nameof(IsCompleted));

        OnPropertyChanged(nameof(IsFailed));

        OnPropertyChanged(nameof(CanPause));

        OnPropertyChanged(nameof(CanResume));

        OnPropertyChanged(nameof(CanRestart));

        OnPropertyChanged(nameof(CanRefreshLink));

        OnPropertyChanged(nameof(CanVerifyChecksum));

        OnPropertyChanged(nameof(ResumeActionText));

        OnPropertyChanged(nameof(ResumeActionIcon));

        OnPropertyChanged(nameof(HybridActionIcon));

        OnPropertyChanged(nameof(HybridActionText));

        OnPropertyChanged(nameof(HybridActionMenuHeader));

        OnPropertyChanged(nameof(HybridActionTooltip));

        OnPropertyChanged(nameof(HybridActionColor));

        OnPropertyChanged(nameof(IsProgressIndeterminate));

        OnPropertyChanged(nameof(IsArchiveFile));

        OnPropertyChanged(nameof(IsDownloadedTorrentFile));

    }



    partial void OnSpeedBytesPerSecondChanged(double value)

    {

        if (value > MaxSpeedBytesPerSecond)

        {

            MaxSpeedBytesPerSecond = value;

            OnPropertyChanged(nameof(MaxSpeedDisplay));

        }



        if (value > 0)

        {

            if (MinSpeedBytesPerSecond == 0 || value < MinSpeedBytesPerSecond)

            {

                MinSpeedBytesPerSecond = value;

                OnPropertyChanged(nameof(MinSpeedDisplay));

            }

        }



        OnPropertyChanged(nameof(SpeedDisplay));

    }



    public void ResetSpeedHistory()
    {
        for (int i = 0; i < SpeedHistory.Count; i++)
        {
            SpeedHistory[i] = 2.0;
        }
    }

    public void PushSpeedBar(double speed)
    {
        double barHeight = 2.0;
        if (speed > 0)
        {
            double scale = Math.Max(64 * 1024.0, MaxSpeedBytesPerSecond);
            double ratio = Math.Clamp(speed / scale, 0.0, 1.0);
            double visualRatio = Math.Sqrt(ratio);
            barHeight = Math.Max(7.0, visualRatio * 38.0);
        }

        SpeedHistory.Add(barHeight);
        while (SpeedHistory.Count > MaxHistoryBars)
        {
            SpeedHistory.RemoveAt(0);
        }
    }



        partial void OnSeedsCountChanged(int value)
    {
        OnPropertyChanged(nameof(PeersDisplay));
    }

    partial void OnLeechesCountChanged(int value)
    {
        OnPropertyChanged(nameof(PeersDisplay));
    }

    partial void OnUploadSpeedBytesPerSecondChanged(double value)
    {
        OnPropertyChanged(nameof(UploadSpeedDisplay));
    }

partial void OnTotalBytesChanged(long value)

    {

        OnPropertyChanged(nameof(TotalBytesDisplay));

        OnPropertyChanged(nameof(IsProgressIndeterminate));

        OnPropertyChanged(nameof(IsTorrentMetaSearching));

        OnPropertyChanged(nameof(StatusDisplay));

    }

    partial void OnDownloadedBytesChanged(long value)

    {

        OnPropertyChanged(nameof(DownloadedBytesDisplay));

        OnPropertyChanged(nameof(TotalBytesDisplay));

    }



    public bool IsActive =>

        Status is DownloadStatus.Downloading or DownloadStatus.Connecting or DownloadStatus.Retrying;



    public bool IsCompleted => Status == DownloadStatus.Completed;

    public bool IsFailed => Status is DownloadStatus.Failed or DownloadStatus.Cancelled;



    // ─── Sağ Tık Dinamik Menü Durum Kontrolleri ─────────────────────────────

    public bool CanPause => IsActive && !IsStalled;

    public bool CanResume => Status is DownloadStatus.Paused or DownloadStatus.Failed or DownloadStatus.Queued || IsStalled;

    public bool CanRestart => Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled || IsStalled;

    public bool CanRefreshLink => !IsTorrent && (Status == DownloadStatus.Failed || Status == DownloadStatus.Paused || IsStalled);

    public bool CanVerifyChecksum => IsCompleted;



    public string ResumeActionText => (IsStalled || Status == DownloadStatus.Failed) 

        ? Loc["Common.RetryResume"] 

        : (Status == DownloadStatus.Queued ? Loc["Toolbar.StartShort"] : Loc["Common.Resume"]);



    public string ResumeActionIcon => (IsStalled || Status == DownloadStatus.Failed) ? "⚡" : "▶";

    public string ResumeActionTooltip => (IsStalled || Status == DownloadStatus.Failed) 

        ? Loc["Common.RetryTooltip"] 

        : (Status == DownloadStatus.Queued ? Loc["Common.StartDownloadTooltip"] : Loc["Common.ResumeTooltip"]);



    public bool CanCancel => !IsCompleted && Status != DownloadStatus.Cancelled;



    partial void OnFileNameChanged(string value)

    {

        if (Category == DownloadCategory.Other && !string.IsNullOrWhiteSpace(value))

        {

            var detected = DownloadCategoryExtensions.FromFileName(value);

            if (detected != DownloadCategory.Other)

            {

                Category = detected;

            }

        }

        OnPropertyChanged(nameof(CategoryIcon));

    }



    partial void OnCategoryChanged(DownloadCategory value)

    {

        OnPropertyChanged(nameof(CategoryIcon));

        OnPropertyChanged(nameof(IsTorrent));

        OnPropertyChanged(nameof(IsMedia));

    }



    public string CategoryIcon

    {

        get

        {

            var cat = Category;

            if (cat == DownloadCategory.Other && !string.IsNullOrWhiteSpace(FileName))

            {

                cat = DownloadCategoryExtensions.FromFileName(FileName);

            }

            return cat switch

            {

                DownloadCategory.Video => "🎬",

                DownloadCategory.Music => "🎵",

                DownloadCategory.Document => "📄",

                DownloadCategory.Archive => "📦",

                DownloadCategory.Program => "⚙️",

                DownloadCategory.Image => "🖼️",

                DownloadCategory.Torrent => "🌊",

                _ => "📁"

            };

        }

    }



    // Factory: DownloadTask'tan oluştur

    public static DownloadItemViewModel FromTask(DownloadTask task)

    {

        return new DownloadItemViewModel

        {

            Id = task.Id,

            FileName = task.FileName,

            OriginalUrl = task.OriginalUrl,

            TotalBytes = task.TotalBytes,

            DownloadedBytes = task.DownloadedBytes,

            ProgressPercent = task.ProgressPercent,

            Status = task.Status,

            Category = task.Category,

            CreatedAt = task.CreatedAt,

            LastErrorMessage = task.LastErrorMessage

        };

    }



    // İlerleme güncellemesi geldiğinde çağrılır (motor olayı)

    public void UpdateProgress(long downloadedBytes, long totalBytes, double speedBps, TimeSpan? eta, IReadOnlyList<double> segmentProgresses)

    {

        DownloadedBytes = downloadedBytes;

        TotalBytes = totalBytes;

        ProgressPercent = totalBytes > 0 ? Math.Min(100, downloadedBytes * 100.0 / totalBytes) : 0;

        SpeedBytesPerSecond = speedBps;

        EtaDisplay = SpeedCalculator.FormatEta(eta);

        SegmentProgresses = segmentProgresses;



        // Hesaplanan özellikler değişti — notify

        OnPropertyChanged(nameof(TotalBytesDisplay));

        OnPropertyChanged(nameof(DownloadedBytesDisplay));

        OnPropertyChanged(nameof(SpeedDisplay));

    }

}

