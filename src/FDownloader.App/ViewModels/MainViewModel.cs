using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDownloader.Core.Config;
using FDownloader.Core.Domain;
using FDownloader.Core.Events;
using FDownloader.Core.Manager;
using FDownloader.Core.Torrent;
using FDownloader.Core.Utilities;
using FDownloader.Core.Video;
using FDownloader.App.Views;
using FDownloader.App.Services;

namespace FDownloader.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DownloadManager _downloadManager;
    private readonly DownloadEventBus _eventBus;
    private readonly ClipboardWatcher _clipboardWatcher;
    private readonly YtDlpMediaService _mediaService;
    private readonly SiteGrabberService _siteGrabberService;
    private readonly UpdateService _updateService;

    // ─── Otomatik Güncelleme (Velopack) ───────────────────────────────────
    [ObservableProperty] private bool _isUpdateAvailable = false;
    [ObservableProperty] private string _updateVersionText = string.Empty;
    [ObservableProperty] private bool _isUpdating = false;
    [ObservableProperty] private int _updateDownloadProgress = 0;
    [ObservableProperty] private string _updateStatusText = string.Empty;

    // ─── İndirme Listesi ve Filtreleme ─────────────────────────────────────
    [ObservableProperty] private ObservableCollection<DownloadItemViewModel> _downloads = new();
    [ObservableProperty] private ObservableCollection<DownloadItemViewModel> _filteredDownloads = new();
    [ObservableProperty] private DownloadItemViewModel? _selectedDownload;
    [ObservableProperty] private ObservableCollection<DownloadItemViewModel> _selectedDownloads = new();
    [ObservableProperty] private int _selectedCategoryIndex = 0; // 0: Tümü, 1: İndiriliyor, 2: Tamamlandı, 3: Duraklatıldı, 4: BitTorrent, 5: Video, 6: Müzik, 7: Sıkıştırılmış, 8: Belgeler
    [ObservableProperty] private string _searchQuery = string.Empty;

    // ─── Sütun Görünürlüğü ve Genişlikleri (Boyutlandırma & Gizleme/Gösterme) ──
    [ObservableProperty] private bool _showColCategory = true;
    [ObservableProperty] private bool _showColSize = true;
    [ObservableProperty] private bool _showColProgress = true;
    [ObservableProperty] private bool _showColSpeed = true;
    [ObservableProperty] private bool _showColMaxSpeed = true;
    [ObservableProperty] private bool _showColEta = true;
    [ObservableProperty] private bool _showColPeers = true;
    [ObservableProperty] private bool _showColStatus = true;

    [ObservableProperty] private double _colNameWidth = 260;
    [ObservableProperty] private double _colSizeWidth = 95;
    [ObservableProperty] private double _colProgressWidth = 130;
    [ObservableProperty] private double _colSpeedWidth = 90;
    [ObservableProperty] private double _colMaxSpeedWidth = 90;
    [ObservableProperty] private double _colEtaWidth = 80;
    [ObservableProperty] private double _colPeersWidth = 85;
    [ObservableProperty] private double _colStatusWidth = 95;

    // ─── Alt Panel (BitTorrent & IDM detayları) ───────────────────────────
    [ObservableProperty] private int _selectedDetailTabIndex = 0; // 0: Genel, 1: Parçalar, 2: Torrent Eşleri
    [ObservableProperty] private TorrentDetails? _currentTorrentDetails;

    // ─── Pano İzleyici ────────────────────────────────────────────────────
    [ObservableProperty] private bool _isClipboardMonitoringEnabled = true;

    // ─── Yeni İndirme Ekleme Diyaloğu ─────────────────────────────────────
    [ObservableProperty] private bool _isAddDialogOpen;
    [ObservableProperty] private string _addDialogUrl = string.Empty;
    [ObservableProperty] private string _addDialogTitle = string.Empty;
    [ObservableProperty] private string _addDialogSavePath = string.Empty;
    [ObservableProperty] private bool _addDialogIsMp3 = true; // Varsayılan MP3
    [ObservableProperty] private bool _addDialogIsMp4 = false;
    [ObservableProperty] private bool _addDialogIsMediaDetected = false;
    [ObservableProperty] private bool _addDialogIsTorrentDetected = false;
    [ObservableProperty] private bool _addDialogIsLoading = false;
    [ObservableProperty] private string? _addDialogThumbnailUrl;
    [ObservableProperty] private string _addDialogStatusText = string.Empty;

    // Playlist seçenekleri
    [ObservableProperty] private bool _addDialogIsPlaylistDetected = false;
    [ObservableProperty] private bool _addDialogDownloadSingleOnly = true; // Sadece seçili şarkı/video
    [ObservableProperty] private bool _addDialogDownloadWholePlaylist = false; // Tüm playlist
    [ObservableProperty] private string _addDialogPlaylistNoticeText = string.Empty;

    // Tarayıcıdan aktarılan başlıklar (Çerezler ve Referanslar)
    [ObservableProperty] private string? _addDialogReferrer;
    [ObservableProperty] private string? _addDialogCookies;
    [ObservableProperty] private string? _addDialogUserAgent;

    // ─── Site Yakalayıcı (Site Grabber - hDM Özelliği) ────────────────────
    [ObservableProperty] private bool _isSiteGrabberDialogOpen;
    [ObservableProperty] private string _siteGrabberUrl = string.Empty;
    [ObservableProperty] private bool _siteGrabberIsLoading;
    [ObservableProperty] private string _siteGrabberStatusText = string.Empty;
    [ObservableProperty] private ObservableCollection<GrabbedFileItem> _grabbedFiles = new();

    // ─── qBittorrent İbre & Hız Sınırları ─────────────────────────────────
    [ObservableProperty] private bool _isAltRateLimitActive = false; // İbre butonu durumu
    [ObservableProperty] private string _altRateLimitTooltip = "Yüksek Hız (Sınırsız): Tıklayarak Yavaş Hız (Limitli) moduna geçin";
    [ObservableProperty] private long _globalDownloadLimitKiB = 0; // 0 = Sonsuz (∞)
    [ObservableProperty] private long _globalUploadLimitKiB = 0;
    [ObservableProperty] private long _altDownloadLimitKiB = 8000;  // 8.0 MiB/s
    [ObservableProperty] private long _altUploadLimitKiB = 3000;    // 3.0 MiB/s
    [ObservableProperty] private bool _isScheduleEnabled = false;
    [ObservableProperty] private string _scheduleStartTime = "08:00";
    [ObservableProperty] private string _scheduleEndTime = "20:00";

    // ─── Belirgin Hız Göstergesi Özellikleri (Sabit Konum, Renkli Gösterge) ─
    public string RateLimitStatusIcon => IsAltRateLimitActive ? "⏱️" : "⚡";
    public string RateLimitStatusText => IsAltRateLimitActive ? "Yavaş Hız" : "Yüksek Hız";
    public string RateLimitStatusSubtext => IsAltRateLimitActive ? "[Limitli]" : "[Sınırsız]";
    public string RateLimitStatusColor => IsAltRateLimitActive ? "#EF4444" : "#10B981";
    public string RateLimitBadgeBackground => IsAltRateLimitActive ? "#30EF4444" : "#3010B981";
    public string RateLimitBadgeBorder => IsAltRateLimitActive ? "#EF4444" : "#10B981";

    // ─── Hibrit Başlat / Duraklat Butonu Özellikleri ───────────────────────
    [ObservableProperty] private string _selectedHybridIcon = "▶";
    [ObservableProperty] private string _selectedHybridText = "Başlat";
    [ObservableProperty] private string _selectedHybridTooltip = "Seçili İndirmeyi Başlat / Devam Et";
    [ObservableProperty] private string _selectedHybridColor = "#10B981";
    // ─── İlerleme Çubiğuğu Stili ────────────────────────────────────────────
    private ProgressBarStyleKey _progressBarStyleKey = ProgressBarStyleKey.Klasik;

    public bool IsProgressStyleKlasik
    {
        get => _progressBarStyleKey == ProgressBarStyleKey.Klasik;
        set { if (value) SetProgressBarStyle(ProgressBarStyleKey.Klasik); }
    }

    public bool IsProgressStyleGradyan
    {
        get => _progressBarStyleKey == ProgressBarStyleKey.Gradyan;
        set { if (value) SetProgressBarStyle(ProgressBarStyleKey.Gradyan); }
    }

    public bool IsProgressStyleNeon
    {
        get => _progressBarStyleKey == ProgressBarStyleKey.Neon;
        set { if (value) SetProgressBarStyle(ProgressBarStyleKey.Neon); }
    }

    public bool IsProgressStyleInce
    {
        get => _progressBarStyleKey == ProgressBarStyleKey.Ince;
        set { if (value) SetProgressBarStyle(ProgressBarStyleKey.Ince); }
    }

    // ─── Tema Modu Sabitleri ve Renk Paletleri ────────────────────────────
    // 1. Koyu Grafit
    private const string DarkGraphiteWindowBg = "#0B0F17";
    private const string DarkGraphiteToolbarBg = "#131A26";
    private const string DarkGraphiteSidebarBg = "#0F1520";
    private const string DarkGraphiteCardBg = "#182232";
    private const string DarkGraphiteTableHeaderBg = "#151E2D";
    private const string DarkGraphiteDetailBg = "#0D131C";
    private const string DarkGraphiteStatusBarBg = "#131A26";
    private const string DarkGraphiteBorderColor = "#1F2E43";
    private const string DarkGraphiteForeground = "#F1F5F9";
    private const string DarkGraphiteTextSecondary = "#94A3B8";
    private const string DarkGraphiteAccent = "#38BDF8";
    private const string DarkGraphiteAccentFg = "#FFFFFF";
    private const string DarkGraphiteSpeedGraphPanelBg = "#0F1622";
    private const string DarkGraphiteSpeedGraphInnerBg = "#090D15";

    // 2. Okyanus Mavisi
    private const string OceanBlueWindowBg = "#06121E";
    private const string OceanBlueToolbarBg = "#0C2238";
    private const string OceanBlueSidebarBg = "#091B2E";
    private const string OceanBlueCardBg = "#102F4E";
    private const string OceanBlueTableHeaderBg = "#0E2741";
    private const string OceanBlueDetailBg = "#081726";
    private const string OceanBlueStatusBarBg = "#0C2238";
    private const string OceanBlueBorderColor = "#0284C7";
    private const string OceanBlueForeground = "#E0F2FE";
    private const string OceanBlueTextSecondary = "#7DD3FC";
    private const string OceanBlueAccent = "#0EA5E9";
    private const string OceanBlueAccentFg = "#FFFFFF";
    private const string OceanBlueSpeedGraphPanelBg = "#0A1C2E";
    private const string OceanBlueSpeedGraphInnerBg = "#05101C";

    // 3. Siber Mor
    private const string CyberPurpleWindowBg = "#12071C";
    private const string CyberPurpleToolbarBg = "#210E36";
    private const string CyberPurpleSidebarBg = "#19092B";
    private const string CyberPurpleCardBg = "#2D1449";
    private const string CyberPurpleTableHeaderBg = "#240F3D";
    private const string CyberPurpleDetailBg = "#150824";
    private const string CyberPurpleStatusBarBg = "#210E36";
    private const string CyberPurpleBorderColor = "#8B5CF6";
    private const string CyberPurpleForeground = "#F5EEFD";
    private const string CyberPurpleTextSecondary = "#D8B4FE";
    private const string CyberPurpleAccent = "#A855F7";
    private const string CyberPurpleAccentFg = "#FFFFFF";
    private const string CyberPurpleSpeedGraphPanelBg = "#1B0A2E";
    private const string CyberPurpleSpeedGraphInnerBg = "#0D0317";

    // 4. Aydınlık (Ferah & Temiz)
    private const string LightCleanWindowBg = "#F8FAFC";
    private const string LightCleanToolbarBg = "#FFFFFF";
    private const string LightCleanSidebarBg = "#F1F5F9";
    private const string LightCleanCardBg = "#FFFFFF";
    private const string LightCleanTableHeaderBg = "#E2E8F0";
    private const string LightCleanDetailBg = "#F8FAFC";
    private const string LightCleanStatusBarBg = "#FFFFFF";
    private const string LightCleanBorderColor = "#CBD5E1";
    private const string LightCleanForeground = "#0F172A";
    private const string LightCleanTextSecondary = "#64748B";
    private const string LightCleanAccent = "#0284C7";
    private const string LightCleanAccentFg = "#FFFFFF";
    private const string LightCleanSpeedGraphPanelBg = "#E2E8F0";
    private const string LightCleanSpeedGraphInnerBg = "#FFFFFF";

    // Canlı Tema Özellikleri (UI Bindingleri)
    [ObservableProperty] private string _themeWindowBg = DarkGraphiteWindowBg;
    [ObservableProperty] private string _themeToolbarBg = DarkGraphiteToolbarBg;
    [ObservableProperty] private string _themeSidebarBg = DarkGraphiteSidebarBg;
    [ObservableProperty] private string _themeCardBg = DarkGraphiteCardBg;
    [ObservableProperty] private string _themeTableHeaderBg = DarkGraphiteTableHeaderBg;
    [ObservableProperty] private string _themeDetailBg = DarkGraphiteDetailBg;
    [ObservableProperty] private string _themeStatusBarBg = DarkGraphiteStatusBarBg;
    [ObservableProperty] private string _themeBorderColor = DarkGraphiteBorderColor;
    [ObservableProperty] private string _themeForeground = DarkGraphiteForeground;
    [ObservableProperty] private string _themeTextSecondary = DarkGraphiteTextSecondary;
    [ObservableProperty] private string _themeAccentBg = DarkGraphiteAccent;
    [ObservableProperty] private string _themeAccentFg = DarkGraphiteAccentFg;
    [ObservableProperty] private string _themeSpeedGraphPanelBg = DarkGraphiteSpeedGraphPanelBg;
    [ObservableProperty] private string _themeSpeedGraphInnerBg = DarkGraphiteSpeedGraphInnerBg;

    // ─── Tema Modu (4 Farklı Renk Paleti) ──────────────────────────────────
    private AppThemeMode _currentThemeMode = AppThemeMode.DarkGraphite;
    private AppThemeMode _savedThemeMode = AppThemeMode.DarkGraphite;

    public bool IsThemeDarkGraphite
    {
        get => _currentThemeMode == AppThemeMode.DarkGraphite;
        set { if (value) SetThemeMode(AppThemeMode.DarkGraphite); }
    }

    public bool IsThemeOceanBlue
    {
        get => _currentThemeMode == AppThemeMode.OceanBlue;
        set { if (value) SetThemeMode(AppThemeMode.OceanBlue); }
    }

    public bool IsThemeCyberPurple
    {
        get => _currentThemeMode == AppThemeMode.CyberPurple;
        set { if (value) SetThemeMode(AppThemeMode.CyberPurple); }
    }

    public bool IsThemeLightClean
    {
        get => _currentThemeMode == AppThemeMode.LightClean;
        set { if (value) SetThemeMode(AppThemeMode.LightClean); }
    }

    // ─── Tamamlanma Zil Sesi Seçenekleri ────────────────────────────────────
    private CompletionSound _currentCompletionSound = CompletionSound.Notify;

    public bool IsSoundNotify
    {
        get => _currentCompletionSound == CompletionSound.Notify;
        set { if (value) SetCompletionSound(CompletionSound.Notify); }
    }

    public bool IsSoundChimes
    {
        get => _currentCompletionSound == CompletionSound.Chimes;
        set { if (value) SetCompletionSound(CompletionSound.Chimes); }
    }

    public bool IsSoundDing
    {
        get => _currentCompletionSound == CompletionSound.Ding;
        set { if (value) SetCompletionSound(CompletionSound.Ding); }
    }

    public bool IsSoundTada
    {
        get => _currentCompletionSound == CompletionSound.Tada;
        set { if (value) SetCompletionSound(CompletionSound.Tada); }
    }

    public bool IsSoundChord
    {
        get => _currentCompletionSound == CompletionSound.Chord;
        set { if (value) SetCompletionSound(CompletionSound.Chord); }
    }

    public bool IsSoundSessiz
    {
        get => _currentCompletionSound == CompletionSound.Sessiz;
        set { if (value) SetCompletionSound(CompletionSound.Sessiz); }
    }

    private const string SettingKeyProgressBarStyle = "Ui.ProgressBarStyle";
    private const string SettingKeyThemeMode = "Ui.ThemeMode";
    private const string SettingKeyCompletionSound = "Ui.CompletionSound";

    private void SetProgressBarStyle(ProgressBarStyleKey style)
    {
        _progressBarStyleKey = style;
        OnPropertyChanged(nameof(IsProgressStyleKlasik));
        OnPropertyChanged(nameof(IsProgressStyleGradyan));
        OnPropertyChanged(nameof(IsProgressStyleNeon));
        OnPropertyChanged(nameof(IsProgressStyleInce));
        _downloadManager.SetSetting(SettingKeyProgressBarStyle, ((int)style).ToString());
    }

    [RelayCommand]
    public void SelectProgressStyle(string styleName)
    {
        if (Enum.TryParse<ProgressBarStyleKey>(styleName, true, out var style))
        {
            SetProgressBarStyle(style);
        }
    }

    [RelayCommand]
    public void SelectTheme(string themeName)
    {
        if (Enum.TryParse<AppThemeMode>(themeName, true, out var mode))
        {
            SetThemeMode(mode);
        }
    }

    public void SetThemeMode(AppThemeMode mode)
    {
        _currentThemeMode = mode;
        OnPropertyChanged(nameof(IsThemeDarkGraphite));
        OnPropertyChanged(nameof(IsThemeOceanBlue));
        OnPropertyChanged(nameof(IsThemeCyberPurple));
        OnPropertyChanged(nameof(IsThemeLightClean));
        ApplyThemeMode(mode);
    }

    public void SetCompletionSound(CompletionSound sound)
    {
        _currentCompletionSound = sound;
        OnPropertyChanged(nameof(IsSoundNotify));
        OnPropertyChanged(nameof(IsSoundChimes));
        OnPropertyChanged(nameof(IsSoundDing));
        OnPropertyChanged(nameof(IsSoundTada));
        OnPropertyChanged(nameof(IsSoundChord));
        OnPropertyChanged(nameof(IsSoundSessiz));
        _downloadManager.SetSetting(SettingKeyCompletionSound, ((int)sound).ToString());
    }

    [RelayCommand]
    public void PlayTestCurrentSound()
    {
        SoundPlayerService.Play(_currentCompletionSound);
    }

    [RelayCommand]
    public void PlaySpecificSound(string soundName)
    {
        if (Enum.TryParse<CompletionSound>(soundName, out var sound))
        {
            SetCompletionSound(sound);
            SoundPlayerService.Play(sound);
        }
    }

    private void ApplyThemeMode(AppThemeMode mode)
    {
        switch (mode)
        {
            case AppThemeMode.OceanBlue:
                ThemeWindowBg = OceanBlueWindowBg;
                ThemeToolbarBg = OceanBlueToolbarBg;
                ThemeSidebarBg = OceanBlueSidebarBg;
                ThemeCardBg = OceanBlueCardBg;
                ThemeTableHeaderBg = OceanBlueTableHeaderBg;
                ThemeDetailBg = OceanBlueDetailBg;
                ThemeStatusBarBg = OceanBlueStatusBarBg;
                ThemeBorderColor = OceanBlueBorderColor;
                ThemeForeground = OceanBlueForeground;
                ThemeTextSecondary = OceanBlueTextSecondary;
                ThemeAccentBg = OceanBlueAccent;
                ThemeAccentFg = OceanBlueAccentFg;
                ThemeSpeedGraphPanelBg = OceanBlueSpeedGraphPanelBg;
                ThemeSpeedGraphInnerBg = OceanBlueSpeedGraphInnerBg;
                break;

            case AppThemeMode.CyberPurple:
                ThemeWindowBg = CyberPurpleWindowBg;
                ThemeToolbarBg = CyberPurpleToolbarBg;
                ThemeSidebarBg = CyberPurpleSidebarBg;
                ThemeCardBg = CyberPurpleCardBg;
                ThemeTableHeaderBg = CyberPurpleTableHeaderBg;
                ThemeDetailBg = CyberPurpleDetailBg;
                ThemeStatusBarBg = CyberPurpleStatusBarBg;
                ThemeBorderColor = CyberPurpleBorderColor;
                ThemeForeground = CyberPurpleForeground;
                ThemeTextSecondary = CyberPurpleTextSecondary;
                ThemeAccentBg = CyberPurpleAccent;
                ThemeAccentFg = CyberPurpleAccentFg;
                ThemeSpeedGraphPanelBg = CyberPurpleSpeedGraphPanelBg;
                ThemeSpeedGraphInnerBg = CyberPurpleSpeedGraphInnerBg;
                break;

            case AppThemeMode.LightClean:
                ThemeWindowBg = LightCleanWindowBg;
                ThemeToolbarBg = LightCleanToolbarBg;
                ThemeSidebarBg = LightCleanSidebarBg;
                ThemeCardBg = LightCleanCardBg;
                ThemeTableHeaderBg = LightCleanTableHeaderBg;
                ThemeDetailBg = LightCleanDetailBg;
                ThemeStatusBarBg = LightCleanStatusBarBg;
                ThemeBorderColor = LightCleanBorderColor;
                ThemeForeground = LightCleanForeground;
                ThemeTextSecondary = LightCleanTextSecondary;
                ThemeAccentBg = LightCleanAccent;
                ThemeAccentFg = LightCleanAccentFg;
                ThemeSpeedGraphPanelBg = LightCleanSpeedGraphPanelBg;
                ThemeSpeedGraphInnerBg = LightCleanSpeedGraphInnerBg;
                break;

            case AppThemeMode.DarkGraphite:
            default:
                ThemeWindowBg = DarkGraphiteWindowBg;
                ThemeToolbarBg = DarkGraphiteToolbarBg;
                ThemeSidebarBg = DarkGraphiteSidebarBg;
                ThemeCardBg = DarkGraphiteCardBg;
                ThemeTableHeaderBg = DarkGraphiteTableHeaderBg;
                ThemeDetailBg = DarkGraphiteDetailBg;
                ThemeStatusBarBg = DarkGraphiteStatusBarBg;
                ThemeBorderColor = DarkGraphiteBorderColor;
                ThemeForeground = DarkGraphiteForeground;
                ThemeTextSecondary = DarkGraphiteTextSecondary;
                ThemeAccentBg = DarkGraphiteAccent;
                ThemeAccentFg = DarkGraphiteAccentFg;
                ThemeSpeedGraphPanelBg = DarkGraphiteSpeedGraphPanelBg;
                ThemeSpeedGraphInnerBg = DarkGraphiteSpeedGraphInnerBg;
                break;
        }

        try
        {
            if (Avalonia.Application.Current is Avalonia.Application app)
            {
                app.RequestedThemeVariant = mode == AppThemeMode.LightClean 
                    ? Avalonia.Styling.ThemeVariant.Light 
                    : Avalonia.Styling.ThemeVariant.Dark;
            }
        }
        catch { }
    }


    public event Action? RequestBringToFront;
    public event Action<DownloadItemViewModel>? RequestSelectItem;
    public event Action? RequestSelectAll;

    // ─── Silme Onay Diyaloğu (Diskten de silinsin mi? / Listeden Kaldır) ────
    [ObservableProperty] private bool _isDeleteConfirmDialogOpen = false;
    [ObservableProperty] private string _deleteConfirmTitle = "Dosyayı Sil / Kaldır";
    [ObservableProperty] private string _deleteConfirmMessage = string.Empty;
    [ObservableProperty] private DeleteConfirmMode _deleteMode = DeleteConfirmMode.GeneralChoice;
    [ObservableProperty] private bool _isListOnlyMode = false;
    [ObservableProperty] private bool _isDiskAndListMode = false;
    [ObservableProperty] private bool _isGeneralChoiceMode = true;

    partial void OnDeleteModeChanged(DeleteConfirmMode value)
    {
        IsListOnlyMode = value == DeleteConfirmMode.ListOnlyPrompt;
        IsDiskAndListMode = value == DeleteConfirmMode.DiskAndListPrompt;
        IsGeneralChoiceMode = value == DeleteConfirmMode.GeneralChoice;
    }

    private List<DownloadItemViewModel> _pendingDeleteItems = new();

    // ─── Ayarlar Modalı (Çark Butonu) ─────────────────────────────────────
    [ObservableProperty] private bool _isSettingsDialogOpen;
    [ObservableProperty] private int _settingsTabIndex = 0; // 0: Hız Sınırları, 1: Site Grabber, 2: Tarayıcı
    [ObservableProperty] private string _settingsDefaultSaveDirectory = string.Empty;
    [ObservableProperty] private int _settingsMaxConcurrentDownloads = 4;
    [ObservableProperty] private int _settingsDefaultSegments = 8;

    // ─── İstatistikler, Canlı Hız Grafiği ve Durum Çubuğu ─────────────────
    [ObservableProperty] private string _totalDownloadSpeedText = "0 B/s";
    [ObservableProperty] private string _totalUploadSpeedText = "0 B/s";
    [ObservableProperty] private string _downloadLimitDisplayText = "[∞]";
    [ObservableProperty] private string _uploadLimitDisplayText = "[∞]";
    [ObservableProperty] private string _activeCountText = "0 İndiriliyor";
    [ObservableProperty] private ObservableCollection<double> _speedHistory = new();

    // ─── Tam Boy Canlı Hız Grafiği (Üst Menü Altında) ──────────────────────
    [ObservableProperty] private bool _isSpeedGraphVisible = true;
    [ObservableProperty] private string _graphTitle = "🌐 Toplam İndirme Hızı (Tüm Aktif İndirmeler)";
    [ObservableProperty] private string _graphCurrentSpeedText = "0 B/s";
    [ObservableProperty] private string _graphMinSpeedText = "0 B/s";
    [ObservableProperty] private string _graphMaxSpeedText = "0 B/s";
    [ObservableProperty] private string _graphDetailsText = "Aktif İndirme: 0";
    [ObservableProperty] private ObservableCollection<double> _displaySpeedHistory = new();
    private ObservableCollection<double> _globalSpeedHistory = new();
    private double _globalMinSpeedRecorded = 0;
    private double _globalMaxSpeedRecorded = 0;

    // Alt durum çubuğu sayaçları (hDM stili)
    [ObservableProperty] private int _totalCount = 0;
    [ObservableProperty] private int _completedCount = 0;
    [ObservableProperty] private int _activeCount = 0;
    [ObservableProperty] private int _pausedCount = 0;
    [ObservableProperty] private int _failedCount = 0;
    [ObservableProperty] private string _diskFreeSpaceText = "Disk: C:\\ -> Hesaplanıyor...";

    public MainViewModel(
        DownloadManager downloadManager,
        DownloadEventBus eventBus,
        ClipboardWatcher clipboardWatcher,
        YtDlpMediaService mediaService,
        SiteGrabberService siteGrabberService,
        UpdateService updateService)
    {
        _downloadManager = downloadManager;
        _eventBus = eventBus;
        _clipboardWatcher = clipboardWatcher;
        _mediaService = mediaService;
        _siteGrabberService = siteGrabberService;
        _updateService = updateService;

        _clipboardWatcher.IsEnabled = true;

        var defaultDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        AddDialogSavePath = defaultDownloads;
        SettingsDefaultSaveDirectory = defaultDownloads;

        var savedStyleStr = _downloadManager.GetSetting(SettingKeyProgressBarStyle);
        if (int.TryParse(savedStyleStr, out var savedStyleInt) && Enum.IsDefined(typeof(ProgressBarStyleKey), savedStyleInt))
        {
            _progressBarStyleKey = (ProgressBarStyleKey)savedStyleInt;
        }

        var savedThemeStr = _downloadManager.GetSetting(SettingKeyThemeMode);
        if (int.TryParse(savedThemeStr, out var savedThemeInt) && Enum.IsDefined(typeof(AppThemeMode), savedThemeInt))
        {
            _currentThemeMode = (AppThemeMode)savedThemeInt;
        }
        _savedThemeMode = _currentThemeMode;
        ApplyThemeMode(_currentThemeMode);

        var savedSoundStr = _downloadManager.GetSetting(SettingKeyCompletionSound);
        if (int.TryParse(savedSoundStr, out var savedSoundInt) && Enum.IsDefined(typeof(CompletionSound), savedSoundInt))
        {
            _currentCompletionSound = (CompletionSound)savedSoundInt;
        }

        // Başlangıç için hız grafiğine 28 boş çubuk koy
        for (int i = 0; i < 28; i++) SpeedHistory.Add(2.0);

        // Canlı hız grafiğinde sol tarafın boş kalmaması için taban çizgisi çubukları
        for (int i = 0; i < DownloadItemViewModel.MaxHistoryBars; i++)
        {
            _globalSpeedHistory.Add(2.0);
        }
        DisplaySpeedHistory = _globalSpeedHistory;

        UpdateHybridState();
        UpdateLimitDisplays();
        UpdateDiskSpace();
        SubscribeToEvents();
        LoadExistingTasks();
        _ = CheckForUpdatesSilentlyAsync();
    }

    private void SubscribeToEvents()
    {
        // 1. Sunucu Probe tamamlandı (Dosya adı ve boyut güncellenir)
        _eventBus.Subscribe<ProbeCompletedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);
                if (item != null)
                {
                    item.FileName = e.FileName;
                    if (item.Category == DownloadCategory.Other)
                    {
                        item.Category = DownloadCategoryExtensions.FromFileName(e.FileName);
                    }
                    if (e.TotalBytes > 0) item.TotalBytes = e.TotalBytes;
                    ApplyFilter();
                }
            });
        });

        // 2. Görev Başlatıldı (Durum 'İndiriliyor'a geçer)
        _eventBus.Subscribe<TaskStartedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);
                if (item != null)
                {
                    item.Status = DownloadStatus.Downloading;
                    ApplyFilter();
                    UpdateGlobalStats();
                    UpdateHybridState();
                }
            });
        });

        // 3. Canlı İlerleme Güncellemesi
        _eventBus.Subscribe<ProgressUpdatedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);
                if (item != null)
                {
                    item.Status = DownloadStatus.Downloading;
                    item.DownloadedBytes = e.DownloadedBytes;
                    if (e.TotalBytes > 0) item.TotalBytes = e.TotalBytes;
                    item.ProgressPercent = e.ProgressPercent;
                    
                    if (item.SpeedBytesPerSecond == e.SpeedBytesPerSecond)
                    {
                        item.PushSpeedBar(e.SpeedBytesPerSecond);
                    }
                    else
                    {
                        item.SpeedBytesPerSecond = e.SpeedBytesPerSecond;
                    }

                    item.EtaDisplay = e.Eta.HasValue ? FormatEta(e.Eta.Value) : "-";

                    // Hız sıfıra düştü ve bir süre düzelmedi kontrolü
                    if (item.Status == DownloadStatus.Downloading)
                    {
                        if (e.SpeedBytesPerSecond <= 0)
                        {
                            item.ConsecutiveZeroSpeedCount++;
                            // Yaklaşık 5-6 saniye (14 x 400ms) boyunca veri akışı durduysa
                            if (item.ConsecutiveZeroSpeedCount >= 14 && !item.IsStalled)
                            {
                                item.IsStalled = true;
                                SoundPlayerService.PlayWarning();
                            }
                        }
                        else
                        {
                            item.ConsecutiveZeroSpeedCount = 0;
                            if (item.IsStalled)
                            {
                                item.IsStalled = false;
                            }
                        }
                    }

                    if (e.Segments.Count > 0)
                    {
                        item.SegmentProgresses = e.Segments.Select(s =>
                        {
                            var total = s.EndByte - s.StartByte + 1;
                            return total > 0 ? (double)s.DownloadedBytes / total : 0.0;
                        }).ToList();
                    }

                    if (item.IsTorrent)
                    {
                        var details = _downloadManager.GetTorrentDetails(e.TaskId);
                        if (details != null)
                        {
                            item.SeedsCount = details.Seeds;
                            item.LeechesCount = details.Leeches;
                            item.UploadSpeedBytesPerSecond = details.UploadSpeed;

                            if (SelectedDownload?.Id == e.TaskId)
                            {
                                CurrentTorrentDetails = details;
                            }
                        }
                    }

                    UpdateGlobalStats();
                }
            });
        });

        // 4. Görev Tamamlandı
        _eventBus.Subscribe<TaskCompletedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);
                if (item != null)
                {
                    item.Status = DownloadStatus.Completed;
                    item.ProgressPercent = 100.0;
                    item.SpeedBytesPerSecond = 0;
                    item.EtaDisplay = "Tamamlandı";
                    item.SavePath = e.FilePath;
                    // Streaming indirmelerde TotalBytes -1 olabilir; gerçek boyutu yaz
                    if (e.TotalBytes > 0) item.TotalBytes = e.TotalBytes;
                    if (!string.IsNullOrEmpty(e.FilePath) && (File.Exists(e.FilePath) || Directory.Exists(e.FilePath)))
                    {
                        var name = Path.GetFileName(e.FilePath);
                        if (!string.IsNullOrWhiteSpace(name)) item.FileName = name;
                    }
                    ApplyFilter();
                }
                UpdateGlobalStats();
                UpdateHybridState();
                UpdateDiskSpace();

                // 🔔 İndirme Tamamlandı Bildirimi ve Ses (Sağ Alt Köşe)
                ShowCompletionNotification(item?.FileName ?? "Dosya", e.FilePath);
            });
        });

        // 5. Görev Duraklatıldı
        _eventBus.Subscribe<TaskPausedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);
                if (item != null)
                {
                    item.Status = DownloadStatus.Paused;
                    item.SpeedBytesPerSecond = 0;
                    item.EtaDisplay = "Duraklatıldı";
                    ApplyFilter();
                }
                UpdateGlobalStats();
                UpdateHybridState();
            });
        });

        // 6. Görev Başarısız (Bağlantı koptu / Hata)
        _eventBus.Subscribe<TaskFailedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);
                if (item != null)
                {
                    item.Status = DownloadStatus.Failed;
                    item.IsStalled = true;
                    item.LastErrorMessage = e.Reason;
                    item.SpeedBytesPerSecond = 0;
                    item.EtaDisplay = "Bağlantı Kesildi";
                    ApplyFilter();
                    SoundPlayerService.PlayWarning();
                }
                UpdateGlobalStats();
                UpdateHybridState();
            });
        });

        // 7. Pano algılama olayı (ClipGrab / IDM tarzı)
        _eventBus.Subscribe<ClipboardUrlDetectedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsAddDialogOpen && !IsSiteGrabberDialogOpen && !IsSettingsDialogOpen)
                {
                    RequestBringToFront?.Invoke();
                    OpenAddDialogWithUrl(e.Url, e.UrlType);
                }
            });
        });

        // 8. Tarayıcı eklentisi algılama olayı (Port 9666 Köprüsü)
        _eventBus.Subscribe<BrowserDownloadCapturedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                RequestBringToFront?.Invoke();
                OpenAddDialogForBrowserCapture(e.Url, e.FileName, e.Referrer, e.Cookies, e.UserAgent);
            });
        });

        // 9. BitTorrent Meta Verisi İndi (Dosya listesi ve boyutu hazır)
        _eventBus.Subscribe<TorrentMetadataLoadedEvent>(e =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);
                if (item != null)
                {
                    item.TotalBytes = e.TotalBytes;
                    if (!string.IsNullOrWhiteSpace(e.Name))
                    {
                        item.FileName = e.Name;
                    }
                }
                if (SelectedDownload?.Id == e.TaskId)
                {
                    CurrentTorrentDetails = _downloadManager.GetTorrentDetails(e.TaskId);
                }
                UpdateGlobalStats();
            });
        });
    }

    private void ShowCompletionNotification(string fileName, string? filePath)
    {
        try
        {
            var toast = new ToastNotificationWindow(fileName, filePath, _currentCompletionSound);
            toast.Show();
        }
        catch { }
    }

    private void LoadExistingTasks()
    {
        Downloads.Clear();
        foreach (var task in _downloadManager.Tasks)
        {
            var fileName = string.IsNullOrWhiteSpace(task.FileName) ? task.Name : task.FileName;
            var cat = task.Category;
            if (cat == DownloadCategory.Other)
            {
                cat = DownloadCategoryExtensions.FromFileName(fileName);
            }
            var savePath = task.FinalFilePath;
            var totalBytes = task.TotalBytes;
            var downloadedBytes = task.DownloadedBytes;
            if (task.Status == DownloadStatus.Completed)
            {
                if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath))
                {
                    var len = new FileInfo(savePath).Length;
                    if (len > 0)
                    {
                        totalBytes = len;
                        downloadedBytes = len;
                    }
                }
                else if (totalBytes > 0 && downloadedBytes <= 0)
                {
                    downloadedBytes = totalBytes;
                }
            }

            Downloads.Add(new DownloadItemViewModel
            {
                Id = task.Id,
                FileName = fileName,
                OriginalUrl = task.OriginalUrl,
                TotalBytes = totalBytes,
                DownloadedBytes = downloadedBytes,
                ProgressPercent = task.Status == DownloadStatus.Completed ? 100.0 : task.ProgressPercent,
                Status = task.Status,
                Category = cat,
                CreatedAt = task.CreatedAt,
                SavePath = savePath
            });
        }
        ApplyFilter();
        UpdateGlobalStats();
    }

    partial void OnSelectedCategoryIndexChanged(int value) => ApplyFilter();
    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchQuery?.Trim().ToLowerInvariant();
        var items = Downloads.AsEnumerable();

        if (!string.IsNullOrEmpty(query))
        {
            items = items.Where(d => d.FileName.ToLowerInvariant().Contains(query) ||
                                     d.OriginalUrl.ToLowerInvariant().Contains(query));
        }

        items = SelectedCategoryIndex switch
        {
            1 => items.Where(d => d.IsActive),
            2 => items.Where(d => d.IsCompleted),
            3 => items.Where(d => d.Status == DownloadStatus.Paused),
            4 => items.Where(d => d.IsTorrent),
            5 => items.Where(d => d.Category == DownloadCategory.Video),
            6 => items.Where(d => d.Category == DownloadCategory.Music),
            7 => items.Where(d => d.Category == DownloadCategory.Archive),
            8 => items.Where(d => d.Category == DownloadCategory.Document),
            _ => items // 0: Tümü
        };

        FilteredDownloads = new ObservableCollection<DownloadItemViewModel>(items);

        // Sayaçları güncelle
        TotalCount = Downloads.Count;
        CompletedCount = Downloads.Count(d => d.IsCompleted);
        ActiveCount = Downloads.Count(d => d.IsActive);
        PausedCount = Downloads.Count(d => d.Status == DownloadStatus.Paused);
        FailedCount = Downloads.Count(d => d.IsFailed);
    }

    partial void OnSelectedDownloadChanged(DownloadItemViewModel? value)
    {
        if (value != null)
        {
            if (value.IsTorrent)
            {
                CurrentTorrentDetails = _downloadManager.GetTorrentDetails(value.Id);
            }
            else
            {
                CurrentTorrentDetails = null;
            }
        }
        else
        {
            CurrentTorrentDetails = null;
        }

        RefreshSpeedGraph();
        UpdateHybridState();
    }

    /// <summary>
    /// UI ListBox seçimini senkronize eder. Tek tıkla sadece o indirmeyi, çoklu seçimde seçilenleri hatasız bağlar.
    /// </summary>
    public void SyncSelectedItems(System.Collections.IList? selectedItems)
    {
        var currentList = selectedItems?.OfType<DownloadItemViewModel>().ToList() ?? new List<DownloadItemViewModel>();

        // 1. SelectedDownloads koleksiyonunu güncelle
        SelectedDownloads.Clear();
        foreach (var item in currentList)
        {
            SelectedDownloads.Add(item);
        }

        // 2. Her bir ViewModel üzerindeki IsSelected bayrağını güncelle
        var selectedSet = new HashSet<long>(currentList.Select(x => x.Id));
        foreach (var item in Downloads)
        {
            item.IsSelected = selectedSet.Contains(item.Id);
        }

        // 3. Tekil seçim mi yoksa çoklu seçim mi?
        if (currentList.Count == 1)
        {
            SelectedDownload = currentList[0];
            CurrentTorrentDetails = SelectedDownload.IsTorrent ? _downloadManager.GetTorrentDetails(SelectedDownload.Id) : null;
        }
        else if (currentList.Count > 1)
        {
            SelectedDownload = currentList.Last();
            CurrentTorrentDetails = null;
        }
        else
        {
            SelectedDownload = null;
            CurrentTorrentDetails = null;
        }

        // 4. Canlı hız grafiği başlık ve verilerini yenile
        RefreshSpeedGraph();
        UpdateHybridState();
    }

    [RelayCommand]
    public async Task ToggleTorrentFilePriority(TorrentFileInfo? file)
    {
        if (file == null || SelectedDownload == null || !SelectedDownload.IsTorrent) return;
        bool newDownloadState = !file.IsSelected;
        file.IsSelected = newDownloadState;
        file.Priority = newDownloadState ? "Normal" : "DoNotDownload";
        await _downloadManager.SetTorrentFilePriorityAsync(SelectedDownload.Id, file.Index, newDownloadState);
        CurrentTorrentDetails = _downloadManager.GetTorrentDetails(SelectedDownload.Id);
    }

    [RelayCommand]
    public async Task SelectAllTorrentFiles(bool selectAll)
    {
        if (SelectedDownload == null || !SelectedDownload.IsTorrent) return;
        await _downloadManager.SetAllTorrentFilesPriorityAsync(SelectedDownload.Id, selectAll);
        CurrentTorrentDetails = _downloadManager.GetTorrentDetails(SelectedDownload.Id);
    }

    /// <summary>
    /// Yeni eklenen indirmeyi seçili hale getirir ve üstteki hız grafiği bölümünde anında gösterir.
    /// </summary>
    public void FocusNewDownload(DownloadItemViewModel newItem)
    {
        SelectedDownloads.Clear();
        SelectedDownloads.Add(newItem);
        foreach (var d in Downloads)
        {
            d.IsSelected = (d == newItem);
        }
        SelectedDownload = newItem;
        CurrentTorrentDetails = newItem.IsTorrent ? _downloadManager.GetTorrentDetails(newItem.Id) : null;

        RefreshSpeedGraph();
        UpdateHybridState();
        RequestSelectItem?.Invoke(newItem);
    }

    partial void OnIsClipboardMonitoringEnabledChanged(bool value)
    {
        _clipboardWatcher.IsEnabled = value;
    }

    partial void OnAddDialogIsMp3Changed(bool value)
    {
        if (value && AddDialogIsMp4) AddDialogIsMp4 = false;
        else if (!value && !AddDialogIsMp4) AddDialogIsMp4 = true;
    }

    partial void OnAddDialogIsMp4Changed(bool value)
    {
        if (value && AddDialogIsMp3) AddDialogIsMp3 = false;
        else if (!value && !AddDialogIsMp3) AddDialogIsMp3 = true;
    }

    partial void OnAddDialogDownloadSingleOnlyChanged(bool value)
    {
        if (value && AddDialogDownloadWholePlaylist) AddDialogDownloadWholePlaylist = false;
        else if (!value && !AddDialogDownloadWholePlaylist) AddDialogDownloadWholePlaylist = true;
    }

    partial void OnAddDialogDownloadWholePlaylistChanged(bool value)
    {
        if (value && AddDialogDownloadSingleOnly) AddDialogDownloadSingleOnly = false;
        else if (!value && !AddDialogDownloadSingleOnly) AddDialogDownloadSingleOnly = true;
    }

    partial void OnAddDialogUrlChanged(string value)
    {
        AnalyzeAddUrl(value);
    }

    // ─── Komutlar: Tekil ve Toplu İndirme İşlemleri (Sağ Tık & Çoklu Seçim) ───────────

    [RelayCommand]
    public async Task StartAllAsync()
    {
        var waiting = Downloads.Where(d => !d.IsCompleted && d.Status != DownloadStatus.Downloading).ToList();
        foreach (var item in waiting)
        {
            item.Status = DownloadStatus.Connecting;
            await _downloadManager.StartOrResumeTaskAsync(item.Id);
        }
    }

    [RelayCommand]
    public async Task PauseAllAsync()
    {
        var running = Downloads.Where(d => d.IsActive).ToList();
        foreach (var item in running)
        {
            item.Status = DownloadStatus.Paused;
            await _downloadManager.PauseTaskAsync(item.Id);
        }
    }

    [RelayCommand]
    public async Task StartItemAsync(DownloadItemViewModel? item)
    {
        var target = item ?? SelectedDownload;
        if (target == null) return;
        target.IsStalled = false;
        target.ConsecutiveZeroSpeedCount = 0;
        target.Status = DownloadStatus.Connecting;
        await _downloadManager.StartOrResumeTaskAsync(target.Id);
    }

    [RelayCommand]
    public async Task PauseItemAsync(DownloadItemViewModel? item)
    {
        var target = item ?? SelectedDownload;
        if (target == null) return;
        target.Status = DownloadStatus.Paused;
        await _downloadManager.PauseTaskAsync(target.Id);
    }

    [RelayCommand]
    public async Task StartSelectedAsync()
    {
        var selectedList = FilteredDownloads.Where(d => d.IsSelected).ToList();
        if (selectedList.Count > 0)
        {
            foreach (var item in selectedList)
            {
                item.Status = DownloadStatus.Connecting;
                await _downloadManager.StartOrResumeTaskAsync(item.Id);
            }
        }
        else if (SelectedDownload != null)
        {
            await StartItemAsync(SelectedDownload);
        }
    }

    [RelayCommand]
    public async Task PauseSelectedAsync()
    {
        var selectedList = FilteredDownloads.Where(d => d.IsSelected).ToList();
        if (selectedList.Count > 0)
        {
            foreach (var item in selectedList)
            {
                item.Status = DownloadStatus.Paused;
                await _downloadManager.PauseTaskAsync(item.Id);
            }
        }
        else if (SelectedDownload != null)
        {
            await PauseItemAsync(SelectedDownload);
        }
    }

    // ─── Hibrit Başlat / Duraklat Komutları ─────────────────────────────────

    [RelayCommand]
    public async Task ToggleItemStartPauseAsync(DownloadItemViewModel? item)
    {
        var target = item ?? SelectedDownload;
        if (target == null) return;

        if (target.IsActive)
        {
            await PauseItemAsync(target);
        }
        else
        {
            await StartItemAsync(target);
        }
        UpdateHybridState();
    }

    [RelayCommand]
    public async Task ToggleStartPauseSelectedAsync()
    {
        var selectedList = GetEffectiveSelectedList();
        if (selectedList.Count > 0)
        {
            bool hasActive = selectedList.Any(d => d.IsActive && !d.IsStalled);
            if (hasActive)
            {
                foreach (var item in selectedList.Where(d => d.IsActive && !d.IsStalled))
                {
                    item.Status = DownloadStatus.Paused;
                    await _downloadManager.PauseTaskAsync(item.Id);
                }
            }
            else
            {
                foreach (var item in selectedList.Where(d => !d.IsActive || d.IsStalled))
                {
                    item.IsStalled = false;
                    item.ConsecutiveZeroSpeedCount = 0;
                    item.Status = DownloadStatus.Connecting;
                    await _downloadManager.StartOrResumeTaskAsync(item.Id);
                }
            }
        }
        else if (SelectedDownload != null)
        {
            await ToggleItemStartPauseAsync(SelectedDownload);
        }
        else
        {
            if (Downloads.Any(d => d.IsActive))
            {
                await PauseAllAsync();
            }
            else
            {
                await StartAllAsync();
            }
        }
        UpdateHybridState();
    }

    public void UpdateHybridState()
    {
        var selectedList = GetEffectiveSelectedList();
        if (selectedList.Count == 1)
        {
            var item = selectedList[0];
            SelectedHybridIcon = item.HybridActionIcon;
            SelectedHybridText = item.HybridActionText;
            SelectedHybridTooltip = item.HybridActionTooltip;
            SelectedHybridColor = item.HybridActionColor;
        }
        else if (selectedList.Count > 1)
        {
            bool hasActive = selectedList.Any(d => d.IsActive);
            if (hasActive)
            {
                SelectedHybridIcon = "⏸";
                SelectedHybridText = "Seçilenleri Duraklat";
                SelectedHybridTooltip = $"Seçilen {selectedList.Count} İndirmeyi Duraklat";
                SelectedHybridColor = "#F59E0B";
            }
            else
            {
                SelectedHybridIcon = "▶";
                SelectedHybridText = "Seçilenleri Başlat";
                SelectedHybridTooltip = $"Seçilen {selectedList.Count} İndirmeyi Başlat";
                SelectedHybridColor = "#10B981";
            }
        }
        else
        {
            bool anyActive = Downloads.Any(d => d.IsActive);
            if (anyActive)
            {
                SelectedHybridIcon = "⏸";
                SelectedHybridText = "Tümünü Duraklat";
                SelectedHybridTooltip = "Tüm Aktif İndirmeleri Duraklat";
                SelectedHybridColor = "#F59E0B";
            }
            else
            {
                SelectedHybridIcon = "▶";
                SelectedHybridText = "Başlat";
                SelectedHybridTooltip = "Seçili veya Bekleyen İndirmeleri Başlat";
                SelectedHybridColor = "#10B981";
            }
        }
    }

    // ─── Silme Komutları (Delete tuşu / Shift+Delete / Buton) ───────────

    private List<DownloadItemViewModel> GetEffectiveSelectedList()
    {
        var selectedList = FilteredDownloads.Where(d => d.IsSelected).ToList();
        if (selectedList.Count == 0 && SelectedDownloads.Count > 0)
        {
            selectedList = SelectedDownloads.ToList();
        }
        if (selectedList.Count == 0 && SelectedDownload != null)
        {
            selectedList = new List<DownloadItemViewModel> { SelectedDownload };
        }
        return selectedList;
    }

    /// <summary>
    /// Delete Tuşu: Listeden kaldırmayı önerir (Dosya diskten silinmez, onay sorar).
    /// </summary>
    [RelayCommand]
    public void RequestListOnlyDelete()
    {
        var selectedList = GetEffectiveSelectedList();
        if (selectedList.Count == 0) return;

        _pendingDeleteItems = selectedList;
        DeleteMode = DeleteConfirmMode.ListOnlyPrompt;
        DeleteConfirmTitle = selectedList.Count == 1 ? "Listeden Kaldır" : $"{selectedList.Count} İndirmeyi Listeden Kaldır";
        DeleteConfirmMessage = selectedList.Count == 1
            ? $"'{selectedList[0].FileName}' indirme listesinden kaldırılsın mı?\n\n(Dosya bilgisayarınızdan silinmez)"
            : $"Seçilen {selectedList.Count} adet indirme listeden kaldırılsın mı?\n\n(Dosyalar bilgisayarınızdan silinmez)";
        IsDeleteConfirmDialogOpen = true;
    }

    /// <summary>
    /// Shift + Delete Tuşu: Hem diskten hem listeden kalıcı olarak silmeyi önerir (Onay sorar).
    /// </summary>
    [RelayCommand]
    public void RequestPermanentDelete()
    {
        var selectedList = GetEffectiveSelectedList();
        if (selectedList.Count == 0) return;

        _pendingDeleteItems = selectedList;
        DeleteMode = DeleteConfirmMode.DiskAndListPrompt;
        DeleteConfirmTitle = selectedList.Count == 1 ? "Kalıcı Olarak Sil (Diskten ve Listeden)" : $"{selectedList.Count} Dosyayı Kalıcı Olarak Sil";
        DeleteConfirmMessage = selectedList.Count == 1
            ? $"DİKKAT: '{selectedList[0].FileName}' dosyası hem indirme listesinden hem de bilgisayarınızdan (diskten) kalıcı olarak silinsin mi?\n\nBu işlem geri alınamaz!"
            : $"DİKKAT: Seçilen {selectedList.Count} adet dosya hem indirme listesinden hem de bilgisayarınızdan (diskten) kalıcı olarak silinsin mi?\n\nBu işlem geri alınamaz!";
        IsDeleteConfirmDialogOpen = true;
    }

    [RelayCommand]
    public void RequestDeleteSelected()
    {
        var selectedList = GetEffectiveSelectedList();
        if (selectedList.Count == 0) return;

        _pendingDeleteItems = selectedList;
        DeleteMode = DeleteConfirmMode.GeneralChoice;
        DeleteConfirmTitle = selectedList.Count == 1 ? "Dosyayı Sil / Kaldır" : $"{selectedList.Count} Dosyayı Sil / Kaldır";
        DeleteConfirmMessage = selectedList.Count == 1
            ? $"'{selectedList[0].FileName}' indirme listesinden kaldırılacak.\n\nBu dosya diskten (bilgisayarınızdan) da kalıcı olarak silinsin mi?"
            : $"Seçilen {selectedList.Count} adet indirme listeden kaldırılacak.\n\nBu dosyalar diskten (bilgisayarınızdan) da kalıcı olarak silinsin mi?";
        IsDeleteConfirmDialogOpen = true;
    }

    [RelayCommand]
    public void RequestDeleteItem(DownloadItemViewModel? item)
    {
        var selectedList = FilteredDownloads.Where(d => d.IsSelected).ToList();
        if (selectedList.Count == 0 && SelectedDownloads.Count > 0)
        {
            selectedList = SelectedDownloads.ToList();
        }

        if (selectedList.Count > 1 && (item == null || selectedList.Contains(item)))
        {
            RequestDeleteSelected();
            return;
        }

        var target = item ?? SelectedDownload;
        if (target == null) return;

        _pendingDeleteItems = new List<DownloadItemViewModel> { target };
        DeleteMode = DeleteConfirmMode.GeneralChoice;
        DeleteConfirmTitle = "Dosyayı Sil / Kaldır";
        DeleteConfirmMessage = $"'{target.FileName}' indirme listesinden kaldırılacak.\n\nBu dosya diskten (bilgisayarınızdan) da kalıcı olarak silinsin mi?";
        IsDeleteConfirmDialogOpen = true;
    }

    [RelayCommand]
    public void SelectAll()
    {
        SelectedDownloads.Clear();
        foreach (var item in FilteredDownloads)
        {
            item.IsSelected = true;
            SelectedDownloads.Add(item);
        }
        if (FilteredDownloads.Count > 0)
        {
            SelectedDownload = FilteredDownloads.Last();
        }
        RequestSelectAll?.Invoke();
        RefreshSpeedGraph();
        UpdateHybridState();
    }

    [RelayCommand]
    public void RequestDeleteAll()
    {
        var allList = FilteredDownloads.ToList();
        if (allList.Count == 0) return;

        _pendingDeleteItems = allList;
        DeleteMode = DeleteConfirmMode.GeneralChoice;
        DeleteConfirmTitle = $"{allList.Count} Dosyayı Sil / Kaldır";
        DeleteConfirmMessage = $"Listedeki TÜM {allList.Count} adet indirme kaldırılacak.\n\nBu dosyalar diskten (bilgisayarınızdan) da kalıcı olarak silinsin mi?";
        IsDeleteConfirmDialogOpen = true;
    }

    [RelayCommand]
    public async Task ClearCompletedAsync()
    {
        var completedList = Downloads.Where(d => d.IsCompleted).ToList();
        if (completedList.Count == 0) return;

        foreach (var item in completedList)
        {
            await _downloadManager.RemoveTaskAsync(item.Id, deleteFile: false);
            Downloads.Remove(item);
        }

        SelectedDownload = null;
        ApplyFilter();
        UpdateGlobalStats();
    }

    public void AdjustColumnWidth(string columnName, double deltaX)
    {
        switch (columnName)
        {
            case "Name":
                ColNameWidth = Math.Max(120, ColNameWidth + deltaX);
                break;
            case "Size":
                ColSizeWidth = Math.Max(50, ColSizeWidth + deltaX);
                break;
            case "Progress":
                ColProgressWidth = Math.Max(70, ColProgressWidth + deltaX);
                break;
            case "Speed":
                ColSpeedWidth = Math.Max(50, ColSpeedWidth + deltaX);
                break;
            case "MaxSpeed":
                ColMaxSpeedWidth = Math.Max(50, ColMaxSpeedWidth + deltaX);
                break;
            case "Eta":
                ColEtaWidth = Math.Max(50, ColEtaWidth + deltaX);
                break;
            case "Peers":
                ColPeersWidth = Math.Max(50, ColPeersWidth + deltaX);
                break;
            case "Status":
                ColStatusWidth = Math.Max(50, ColStatusWidth + deltaX);
                break;
        }
    }

    [RelayCommand]
    public void ResetColumnWidths()
    {
        ColNameWidth = 260;
        ColSizeWidth = 95;
        ColProgressWidth = 130;
        ColSpeedWidth = 90;
        ColMaxSpeedWidth = 90;
        ColEtaWidth = 80;
        ColPeersWidth = 85;
        ColStatusWidth = 95;

        ShowColCategory = true;
        ShowColSize = true;
        ShowColProgress = true;
        ShowColSpeed = true;
        ShowColMaxSpeed = true;
        ShowColEta = true;
        ShowColPeers = true;
        ShowColStatus = true;
    }

    // Geriye dönük uyumluluk için eski komutları da diyaloğa yönlendiriyoruz
    [RelayCommand]
    public void RemoveSelected() => RequestDeleteSelected();

    [RelayCommand]
    public void RemoveItem(DownloadItemViewModel? item) => RequestDeleteItem(item);

    [RelayCommand]
    public void DeleteWithFile(DownloadItemViewModel? item) => RequestDeleteItem(item);

    [RelayCommand]
    public async Task ConfirmDeleteWithFileAsync()
    {
        await ExecuteDeleteAsync(deleteFromDisk: true);
    }

    [RelayCommand]
    public async Task ConfirmDeleteListOnlyAsync()
    {
        await ExecuteDeleteAsync(deleteFromDisk: false);
    }

    [RelayCommand]
    public void CancelDeleteDialog()
    {
        IsDeleteConfirmDialogOpen = false;
        _pendingDeleteItems.Clear();
    }

    private async Task ExecuteDeleteAsync(bool deleteFromDisk)
    {
        IsDeleteConfirmDialogOpen = false;
        var items = _pendingDeleteItems.ToList();
        _pendingDeleteItems.Clear();

        foreach (var target in items)
        {
            await _downloadManager.RemoveTaskAsync(target.Id, deleteFile: deleteFromDisk);

            if (deleteFromDisk)
            {
                DeleteLocalFilesForDownload(target);
            }

            Downloads.Remove(target);
        }

        SelectedDownload = null;
        ApplyFilter();
        UpdateGlobalStats();
    }

    private static void DeleteLocalFilesForDownload(DownloadItemViewModel target)
    {
        try
        {
            var path = target.SavePath;
            if (string.IsNullOrWhiteSpace(path)) return;

            // KORUMA: SavePath asla bir klasör olarak recursive silinmemelidir!
            // Eğer SavePath bir dizini gösteriyorsa veya sistem/kullanıcı klasörüyse kesinlikle dokunulmaz.
            if (Directory.Exists(path))
            {
                return;
            }

            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }

            // Geçici indirme uzantıları (.fdown, .fdtmp, .part, .ytdl, .tmp)
            string[] tempExtensions = [".fdown", ".fdtmp", ".part", ".ytdl", ".tmp"];
            foreach (var ext in tempExtensions)
            {
                var tempPath = path + ext;
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }

            // Klasör içi isim eşleşmeli geçici artıklar (Sadece fileName.* olanlar: ornek.mp4.part vb.)
            var dir = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !string.IsNullOrEmpty(fileName))
            {
                try
                {
                    var matching = Directory.GetFiles(dir, $"{fileName}.*");
                    foreach (var f in matching)
                    {
                        var fExt = Path.GetExtension(f).ToLowerInvariant();
                        if (fExt is ".fdown" or ".fdtmp" or ".part" or ".ytdl" or ".tmp")
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    [RelayCommand]
    public void OpenFile(DownloadItemViewModel? item)
    {
        var target = item ?? SelectedDownload;
        if (target == null) return;
        var path = target.SavePath;
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else
        {
            OpenFolder(target);
        }
    }

    [RelayCommand]
    public void OpenFolder(DownloadItemViewModel? item)
    {
        var target = item ?? SelectedDownload;
        if (target == null) return;
        var path = target.SavePath;
        if (string.IsNullOrEmpty(path))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = false
                });
            }
            else
            {
                var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                if (Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{dir}\"",
                        UseShellExecute = false
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenFolder error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void CopyUrl(DownloadItemViewModel? item)
    {
        var target = item ?? SelectedDownload;
        if (target == null || string.IsNullOrWhiteSpace(target.OriginalUrl)) return;
        try
        {
            _clipboardWatcher.ProcessText(target.OriginalUrl);
        }
        catch { }
    }

    // ─── qBittorrent İbre (Speedometer) ve Hız Limiti Kontrolü ─────────────

    [RelayCommand]
    public void ToggleAltRateLimit()
    {
        IsAltRateLimitActive = !IsAltRateLimitActive;
        UpdateLimitDisplays();
    }

    private void UpdateLimitDisplays()
    {
        long effectiveDownloadLimit = 0;
        long effectiveUploadLimit = 0;

        if (IsAltRateLimitActive)
        {
            effectiveDownloadLimit = AltDownloadLimitKiB * 1024;
            effectiveUploadLimit = AltUploadLimitKiB * 1024;
            DownloadLimitDisplayText = $"[{SpeedCalculator.FormatSpeed(effectiveDownloadLimit)}]";
            UploadLimitDisplayText = $"[{SpeedCalculator.FormatSpeed(effectiveUploadLimit)}]";
            AltRateLimitTooltip = $"Yavaş Hız (Limitli): İndirme {AltDownloadLimitKiB} KiB/s, Gönderme {AltUploadLimitKiB} KiB/s • Sol Tık: Sınırsıza Geç • Sağ Tık: Hız Ayarları";
        }
        else
        {
            effectiveDownloadLimit = GlobalDownloadLimitKiB > 0 ? GlobalDownloadLimitKiB * 1024 : 0;
            effectiveUploadLimit = GlobalUploadLimitKiB > 0 ? GlobalUploadLimitKiB * 1024 : 0;

            DownloadLimitDisplayText = GlobalDownloadLimitKiB > 0
                ? $"[{SpeedCalculator.FormatSpeed(effectiveDownloadLimit)}]"
                : "[Sınırsız]";

            UploadLimitDisplayText = GlobalUploadLimitKiB > 0
                ? $"[{SpeedCalculator.FormatSpeed(effectiveUploadLimit)}]"
                : "[Sınırsız]";
            AltRateLimitTooltip = "Yüksek Hız (Sınırsız) • Sol Tık: Limitli Hıza Geç • Sağ Tık: Hız Ayarları";
        }

        // Motor hız limitlerini anında güncelle
        _downloadManager.SetSpeedLimits(effectiveDownloadLimit, effectiveUploadLimit);

        OnPropertyChanged(nameof(IsAltRateLimitActive));
        OnPropertyChanged(nameof(RateLimitStatusIcon));
        OnPropertyChanged(nameof(RateLimitStatusText));
        OnPropertyChanged(nameof(RateLimitStatusSubtext));
        OnPropertyChanged(nameof(RateLimitStatusColor));
        OnPropertyChanged(nameof(RateLimitBadgeBackground));
        OnPropertyChanged(nameof(RateLimitBadgeBorder));
    }

    partial void OnIsAltRateLimitActiveChanged(bool value) => UpdateLimitDisplays();
    partial void OnGlobalDownloadLimitKiBChanged(long value) => UpdateLimitDisplays();
    partial void OnGlobalUploadLimitKiBChanged(long value) => UpdateLimitDisplays();
    partial void OnAltDownloadLimitKiBChanged(long value) => UpdateLimitDisplays();
    partial void OnAltUploadLimitKiBChanged(long value) => UpdateLimitDisplays();

    // ─── Komutlar: ⚙️ Ayarlar Modalı ──────────────────────────────────────

    [RelayCommand]
    public void OpenSettingsDialog()
    {
        _savedThemeMode = _currentThemeMode;
        SettingsTabIndex = 0;
        IsSettingsDialogOpen = true;
    }

    [RelayCommand]
    public void OpenSpeedLimitSettings()
    {
        _savedThemeMode = _currentThemeMode;
        SettingsTabIndex = 2; // SEKME 3 (Index 2): ⚡ Hız & Limitler
        IsSettingsDialogOpen = true;
    }

    [RelayCommand]
    public void SaveSettings()
    {
        _downloadManager.MaxConcurrentDownloads = SettingsMaxConcurrentDownloads;
        _downloadManager.SetSetting(SettingKeyProgressBarStyle, ((int)_progressBarStyleKey).ToString());
        _downloadManager.SetSetting(SettingKeyThemeMode, ((int)_currentThemeMode).ToString());
        _downloadManager.SetSetting(SettingKeyCompletionSound, ((int)_currentCompletionSound).ToString());
        _savedThemeMode = _currentThemeMode;
        ApplyThemeMode(_currentThemeMode);
        UpdateLimitDisplays();
        UpdateDiskSpace();
        IsSettingsDialogOpen = false;
    }

    [RelayCommand]
    public void CancelSettings()
    {
        if (_currentThemeMode != _savedThemeMode)
        {
            SetThemeMode(_savedThemeMode);
        }
        IsSettingsDialogOpen = false;
    }

    // ─── Komutlar: 🌐 Site Yakalayıcı (Site Grabber) ──────────────────────

    [RelayCommand]
    public void OpenSiteGrabberDialog()
    {
        SiteGrabberUrl = string.Empty;
        SiteGrabberStatusText = "Taranacak web sitesi adresini girin.";
        GrabbedFiles.Clear();
        IsSiteGrabberDialogOpen = true;
    }

    [RelayCommand]
    public async Task AnalyzeSiteAsync()
    {
        if (string.IsNullOrWhiteSpace(SiteGrabberUrl)) return;

        SiteGrabberIsLoading = true;
        SiteGrabberStatusText = "Sayfa taranıyor ve dosyalar ayıklanıyor...";

        try
        {
            var files = await _siteGrabberService.AnalyzePageAsync(SiteGrabberUrl.Trim());
            GrabbedFiles = new ObservableCollection<GrabbedFileItem>(files);
            SiteGrabberStatusText = $"{files.Count} indirilebilir dosya ve medya bulundu.";
        }
        catch (Exception ex)
        {
            SiteGrabberStatusText = $"Hata: {ex.Message}";
        }
        finally
        {
            SiteGrabberIsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmGrabbedDownloadsAsync()
    {
        var selected = GrabbedFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        var saveDir = SettingsDefaultSaveDirectory;
        IsSiteGrabberDialogOpen = false;

        DownloadItemViewModel? firstItem = null;
        foreach (var file in selected)
        {
            var task = await _downloadManager.AddHttpDownloadAsync(file.Url, saveDir, segmentCount: 8, startImmediately: true);
            var newItem = new DownloadItemViewModel
            {
                Id = task.Id,
                FileName = file.FileName,
                OriginalUrl = file.Url,
                Category = file.Category,
                Status = DownloadStatus.Connecting,
                CreatedAt = task.CreatedAt,
                SavePath = Path.Combine(saveDir, file.FileName)
            };
            Downloads.Insert(0, newItem);
            firstItem ??= newItem;
        }

        ApplyFilter();
        UpdateGlobalStats();

        if (firstItem != null)
        {
            FocusNewDownload(firstItem);
        }
    }

    [RelayCommand]
    public void CancelSiteGrabber()
    {
        IsSiteGrabberDialogOpen = false;
    }

    // ─── Komutlar: Yeni İndirme Ekleme ────────────────────────────────────

    public Func<Task<string?>>? RequestBrowseTorrentFile { get; set; }

    [RelayCommand]
    public async Task BrowseTorrentFile()
    {
        if (RequestBrowseTorrentFile != null)
        {
            var path = await RequestBrowseTorrentFile();
            if (!string.IsNullOrWhiteSpace(path))
            {
                AddDialogUrl = path;
                IsAddDialogOpen = true;
                AnalyzeAddUrl(path);
            }
        }
    }

    public async Task HandleDroppedFileOrUrlAsync(string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return;

        var clean = pathOrUrl.Trim().Trim('\"', '\'');
        var saveDir = !string.IsNullOrWhiteSpace(AddDialogSavePath)
            ? AddDialogSavePath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        // .torrent dosyası veya magnet linki ise indirmeyi doğrudan başlat
        if (clean.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var task = await _downloadManager.AddTorrentDownloadAsync(clean, saveDir, startImmediately: true);
                var newItem = new DownloadItemViewModel
                {
                    Id = task.Id,
                    FileName = task.FileName,
                    OriginalUrl = task.OriginalUrl,
                    Category = DownloadCategory.Torrent,
                    Status = DownloadStatus.Downloading,
                    CreatedAt = task.CreatedAt,
                    SavePath = saveDir
                };
                Downloads.Insert(0, newItem);
                ApplyFilter();
                UpdateGlobalStats();
                FocusNewDownload(newItem);
            }
            catch (Exception ex)
            {
                AddDialogUrl = clean;
                AddDialogStatusText = $"Hata: {ex.Message}";
                IsAddDialogOpen = true;
                AnalyzeAddUrl(clean);
            }
        }
        else
        {
            // Standart web veya medya linki ise diyaloğu doldur
            AddDialogUrl = clean;
            IsAddDialogOpen = true;
            AnalyzeAddUrl(clean);
        }
    }

    [RelayCommand]
    public void OpenAddDownloadDialog()
    {
        AddDialogUrl = string.Empty;
        AddDialogTitle = string.Empty;
        AddDialogThumbnailUrl = null;
        AddDialogIsMediaDetected = false;
        AddDialogIsTorrentDetected = false;
        AddDialogIsPlaylistDetected = false;
        AddDialogIsMp3 = true;
        AddDialogIsMp4 = false;
        AddDialogDownloadSingleOnly = true;
        AddDialogDownloadWholePlaylist = false;
        AddDialogStatusText = "URL veya Magnet linki girin";
        IsAddDialogOpen = true;
    }

    public void OpenAddDialogWithUrl(string url, ClipboardUrlType urlType)
    {
        AddDialogUrl = url;
        AddDialogReferrer = null;
        AddDialogCookies = null;
        AddDialogUserAgent = null;
        IsAddDialogOpen = true;
        AnalyzeAddUrl(url);
    }

    public void OpenAddDialogForBrowserCapture(
        string url, string? fileName, string? referrer, string? cookies, string? userAgent)
    {
        AddDialogUrl = url;
        AddDialogReferrer = referrer;
        AddDialogCookies = cookies;
        AddDialogUserAgent = userAgent;
        IsAddDialogOpen = true;
        AnalyzeAddUrl(url);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            AddDialogTitle = fileName;
            AddDialogStatusText = $"🌐 Tarayıcıdan yakalandı: {fileName}";
        }
    }

    [RelayCommand]
    public void OpenBrowserExtensionFolder()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "browser-extension"),
            Path.Combine(Directory.GetCurrentDirectory(), "tools", "browser-extension"),
            @"C:\FDownloader\tools\browser-extension"
        };

        var folder = candidates.FirstOrDefault(Directory.Exists);
        if (folder != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = false
                });
            }
            catch { }
        }
    }

    private void AnalyzeAddUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            AddDialogIsMediaDetected = false;
            AddDialogIsTorrentDetected = false;
            AddDialogIsPlaylistDetected = false;
            AddDialogStatusText = "URL veya Magnet linki girin";
            return;
        }

        var url = rawUrl.Trim();
        var cleanUrl = url.Split('?')[0].Trim('\"', '\'');

        // 1. BitTorrent Kontrolü
        if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ||
            cleanUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ||
            url.Contains(".torrent?", StringComparison.OrdinalIgnoreCase))
        {
            AddDialogIsTorrentDetected = true;
            AddDialogIsMediaDetected = false;
            AddDialogIsPlaylistDetected = false;
            AddDialogTitle = url.StartsWith("magnet:") ? "Magnet Torrent" : Path.GetFileName(cleanUrl);
            AddDialogStatusText = "BitTorrent indirmesi algılandı.";
            return;
        }

        // 2. YouTube & Medya Kontrolü
        bool isMedia = url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                       url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
                       url.Contains("tiktok.com", StringComparison.OrdinalIgnoreCase) ||
                       url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase) ||
                       url.Contains("twitter.com", StringComparison.OrdinalIgnoreCase) ||
                       url.Contains("x.com", StringComparison.OrdinalIgnoreCase) ||
                       url.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase);

        if (isMedia)
        {
            AddDialogIsMediaDetected = true;
            AddDialogIsTorrentDetected = false;

            // Playlist algılama
            bool hasPlaylist = url.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
                               url.Contains("/playlist", StringComparison.OrdinalIgnoreCase);
            bool hasVideoId = url.Contains("v=", StringComparison.OrdinalIgnoreCase) ||
                              url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

            if (hasPlaylist)
            {
                AddDialogIsPlaylistDetected = true;
                if (hasVideoId)
                {
                    AddDialogPlaylistNoticeText = "🎵 Çalma Listesi (Playlist) Algılandı: Hem bu parça hem de liste bulundu.";
                    AddDialogDownloadSingleOnly = true;
                    AddDialogDownloadWholePlaylist = false;
                }
                else
                {
                    AddDialogPlaylistNoticeText = "🎵 Çalma Listesi (Playlist) Algılandı: Tüm liste indirilecek.";
                    AddDialogDownloadSingleOnly = false;
                    AddDialogDownloadWholePlaylist = true;
                }
            }
            else
            {
                AddDialogIsPlaylistDetected = false;
                AddDialogDownloadSingleOnly = true;
                AddDialogDownloadWholePlaylist = false;
            }

            FetchMediaInfoInBackground(url);
        }
        else
        {
            AddDialogIsMediaDetected = false;
            AddDialogIsTorrentDetected = false;
            AddDialogIsPlaylistDetected = false;
            AddDialogTitle = Path.GetFileName(cleanUrl);
            AddDialogStatusText = "Doğrudan indirme bağlantısı.";
        }
    }

    private async void FetchMediaInfoInBackground(string url)
    {
        AddDialogIsLoading = true;
        bool isYouTube = url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
        AddDialogStatusText = isYouTube ? "YouTube bilgisi alınıyor..." : "Medya bilgisi alınıyor...";

        try
        {
            var info = await _mediaService.GetMediaInfoAsync(url);
            AddDialogIsLoading = false;

            if (info != null)
            {
                AddDialogTitle = info.Title;
                AddDialogThumbnailUrl = info.ThumbnailUrl;
                AddDialogStatusText = $"{info.Title} ({info.Duration:mm\\:ss})";
            }
            else
            {
                AddDialogTitle = "Medya İndirme";
                AddDialogStatusText = "Medya algılandı. MP4 veya MP3 formatı seçebilirsiniz.";
            }
        }
        catch
        {
            AddDialogIsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmAddDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(AddDialogUrl)) return;

        var url = AddDialogUrl.Trim();
        var saveDir = AddDialogSavePath;

        try
        {
            IsAddDialogOpen = false;

            if (AddDialogIsMediaDetected)
            {
                bool isWholePlaylist = AddDialogIsPlaylistDetected && AddDialogDownloadWholePlaylist;

                // YouTube / Video / MP3 İndirmesi
                var task = await _downloadManager.AddMediaDownloadAsync(
                    url,
                    saveDir,
                    isAudioOnlyMp3: AddDialogIsMp3,
                    isPlaylist: isWholePlaylist,
                    title: string.IsNullOrWhiteSpace(AddDialogTitle) ? null : AddDialogTitle,
                    startImmediately: true);

                var newItem = new DownloadItemViewModel
                {
                    Id = task.Id,
                    FileName = task.FileName,
                    OriginalUrl = task.OriginalUrl,
                    Category = task.Category,
                    Status = DownloadStatus.Downloading,
                    CreatedAt = task.CreatedAt,
                    SavePath = Path.Combine(saveDir, task.FileName)
                };
                Downloads.Insert(0, newItem);
                ApplyFilter();
                UpdateGlobalStats();
                FocusNewDownload(newItem);
            }
            else if (url.StartsWith("magnet:") || url.Split('?')[0].Trim('\"', '\'').EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) || AddDialogIsTorrentDetected)
            {
                // BitTorrent İndirmesi
                var task = await _downloadManager.AddTorrentDownloadAsync(url, saveDir, startImmediately: true);
                var newItem = new DownloadItemViewModel
                {
                    Id = task.Id,
                    FileName = task.FileName,
                    OriginalUrl = task.OriginalUrl,
                    Category = DownloadCategory.Torrent,
                    Status = DownloadStatus.Downloading,
                    CreatedAt = task.CreatedAt,
                    SavePath = saveDir
                };
                Downloads.Insert(0, newItem);
                ApplyFilter();
                UpdateGlobalStats();
                FocusNewDownload(newItem);
            }
            else
            {
                // Standart Çok Parçalı HTTP İndirmesi
                var targetFileName = !string.IsNullOrWhiteSpace(AddDialogTitle) ? AddDialogTitle : null;
                var detectedCategory = DownloadCategoryExtensions.FromFileName(targetFileName ?? url);

                var task = await _downloadManager.AddHttpDownloadAsync(
                    url,
                    saveDir,
                    segmentCount: SettingsDefaultSegments,
                    category: detectedCategory,
                    fileName: targetFileName,
                    referer: AddDialogReferrer,
                    cookies: AddDialogCookies,
                    userAgent: AddDialogUserAgent,
                    startImmediately: true);

                var finalCategory = task.Category != DownloadCategory.Other ? task.Category : detectedCategory;
                var newItem = new DownloadItemViewModel
                {
                    Id = task.Id,
                    FileName = !string.IsNullOrWhiteSpace(task.FileName) ? task.FileName : (targetFileName ?? task.Name),
                    OriginalUrl = task.OriginalUrl,
                    Category = finalCategory,
                    Status = DownloadStatus.Connecting,
                    CreatedAt = task.CreatedAt,
                    SavePath = Path.Combine(saveDir, task.FileName)
                };
                Downloads.Insert(0, newItem);
                ApplyFilter();
                UpdateGlobalStats();
                FocusNewDownload(newItem);
            }
        }
        catch (Exception ex)
        {
            AddDialogStatusText = $"Hata: {ex.Message}";
            IsAddDialogOpen = true;
        }
    }

    [RelayCommand]
    public void CancelAddDownload()
    {
        IsAddDialogOpen = false;
    }

    [RelayCommand]
    public void ToggleSpeedGraph()
    {
        IsSpeedGraphVisible = !IsSpeedGraphVisible;
    }

    private void UpdateGlobalStats()
    {
        var active = Downloads.Where(d => d.IsActive).ToList();
        ActiveCountText = $"{active.Count} İndiriliyor";

        var totalDownSpeed = active.Sum(d => d.SpeedBytesPerSecond);
        TotalDownloadSpeedText = totalDownSpeed > 0 ? SpeedCalculator.FormatSpeed(totalDownSpeed) : "0 B/s";

        var totalUpSpeed = active.Sum(d => d.UploadSpeedBytesPerSecond);
        TotalUploadSpeedText = totalUpSpeed > 0 ? SpeedCalculator.FormatSpeed(totalUpSpeed) : "0 B/s";

        // Global Max / Min Hız Takibi
        if (totalDownSpeed > _globalMaxSpeedRecorded)
            _globalMaxSpeedRecorded = totalDownSpeed;

        if (totalDownSpeed > 0)
        {
            if (_globalMinSpeedRecorded == 0 || totalDownSpeed < _globalMinSpeedRecorded)
                _globalMinSpeedRecorded = totalDownSpeed;
        }

        // Canlı Hız Grafiği (Sparkline Çubukları)
        double barHeight = 2.0;
        if (totalDownSpeed > 0)
        {
            double scale = Math.Max(64 * 1024.0, _globalMaxSpeedRecorded);
            double ratio = Math.Clamp(totalDownSpeed / scale, 0.08, 1.0);
            barHeight = Math.Max(4.0, ratio * 38.0);
        }

        SpeedHistory.Add(barHeight);
        if (SpeedHistory.Count > 28)
        {
            SpeedHistory.RemoveAt(0);
        }

        _globalSpeedHistory.Add(barHeight);
        if (_globalSpeedHistory.Count > DownloadItemViewModel.MaxHistoryBars)
        {
            _globalSpeedHistory.RemoveAt(0);
        }

        RefreshSpeedGraph();
        UpdateHybridState();
    }

    public void RefreshSpeedGraph()
    {
        DownloadItemViewModel? singleTarget = null;

        var selectedList = SelectedDownloads != null && SelectedDownloads.Count > 0
            ? SelectedDownloads.ToList()
            : Downloads.Where(d => d.IsSelected).ToList();

        if (selectedList.Count == 1)
        {
            // Tek bir kayıt seçili
            singleTarget = selectedList[0];
        }
        else if (selectedList.Count > 1)
        {
            // Birden fazla kayıt seçili
            singleTarget = null;
        }
        else if (SelectedDownload != null)
        {
            // Doğrudan seçili olan tekil kayıt
            singleTarget = SelectedDownload;
        }

        if (singleTarget != null)
        {
            // TEK SEÇİLİ ÖĞE: Bu indirmeye ait verileri göster
            GraphTitle = $"🎯 {singleTarget.FileName}";
            GraphCurrentSpeedText = singleTarget.SpeedDisplay;
            GraphMinSpeedText = singleTarget.MinSpeedDisplay;
            GraphMaxSpeedText = singleTarget.MaxSpeedDisplay;
            GraphDetailsText = $"%{singleTarget.ProgressPercent:F1} • {singleTarget.DownloadedBytesDisplay} / {singleTarget.TotalBytesDisplay} • {singleTarget.StatusDisplay}";

            if (DisplaySpeedHistory != singleTarget.SpeedHistory)
            {
                DisplaySpeedHistory = singleTarget.SpeedHistory;
            }
        }
        else
        {
            // HİÇ SEÇİLİ YOK VEYA BİRDEN FAZLA SEÇİLİ: Toplam indirme durumunu göster
            int count = selectedList.Count;
            var activeList = count > 1 
                ? selectedList.Where(d => d.IsActive).ToList() 
                : Downloads.Where(d => d.IsActive).ToList();

            var totalDownSpeed = activeList.Sum(d => d.SpeedBytesPerSecond);
            GraphTitle = count > 1 
                ? $"🌐 Seçilen {count} Dosyanın Toplam Hızı" 
                : "🌐 Toplam İndirme Hızı (Tüm Aktif İndirmeler)";

            GraphCurrentSpeedText = totalDownSpeed > 0 ? SpeedCalculator.FormatSpeed(totalDownSpeed) : "0 B/s";
            GraphMinSpeedText = _globalMinSpeedRecorded > 0 ? SpeedCalculator.FormatSpeed(_globalMinSpeedRecorded) : "0 B/s";
            GraphMaxSpeedText = _globalMaxSpeedRecorded > 0 ? SpeedCalculator.FormatSpeed(_globalMaxSpeedRecorded) : "0 B/s";
            GraphDetailsText = $"{activeList.Count} İndiriliyor • {Downloads.Count(d => d.IsCompleted)} Tamamlandı • Toplam {Downloads.Count}";

            if (DisplaySpeedHistory != _globalSpeedHistory)
            {
                DisplaySpeedHistory = _globalSpeedHistory;
            }
        }
    }

    private void UpdateDiskSpace()
    {
        DiskFreeSpaceText = DiskSpaceChecker.GetDriveInfoSummary(SettingsDefaultSaveDirectory);
    }

    private static string FormatEta(TimeSpan eta)
    {
        if (eta.TotalHours >= 1)
            return $"{eta.Hours}s {eta.Minutes}d";
        if (eta.TotalMinutes >= 1)
            return $"{eta.Minutes}d {eta.Seconds}sn";
        return $"{eta.Seconds}sn";
    }

    [RelayCommand]
    public async Task CheckForUpdatesManualAsync()
    {
        UpdateStatusText = "Güncellemeler denetleniyor...";
        var info = await _updateService.CheckForUpdatesAsync();
        if (info != null)
        {
            IsUpdateAvailable = true;
            UpdateVersionText = $"v{info.TargetFullRelease.Version}";
            UpdateStatusText = $"Yeni sürüm mevcut ({UpdateVersionText}).";
        }
        else
        {
            IsUpdateAvailable = false;
            UpdateStatusText = "Uygulamanız güncel.";
        }
    }

    [RelayCommand]
    public async Task ApplyUpdateAsync()
    {
        if (IsUpdating) return;
        try
        {
            IsUpdating = true;
            UpdateStatusText = "Güncelleme indiriliyor...";
            var info = await _updateService.CheckForUpdatesAsync();
            if (info == null)
            {
                IsUpdating = false;
                return;
            }

            await _updateService.DownloadUpdatesAsync(info, p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateDownloadProgress = p;
                    UpdateStatusText = $"Güncelleme indiriliyor: %{p}";
                });
            });

            UpdateStatusText = "Güncelleme uygulanıyor ve yeniden başlatılıyor...";
            _updateService.ApplyUpdatesAndRestart(info);
        }
        catch (Exception ex)
        {
            IsUpdating = false;
            UpdateStatusText = $"Güncelleme başarısız: {ex.Message}";
        }
    }

    private async Task CheckForUpdatesSilentlyAsync()
    {
        try
        {
            var info = await _updateService.CheckForUpdatesAsync();
            if (info != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsUpdateAvailable = true;
                    UpdateVersionText = $"v{info.TargetFullRelease.Version}";
                    UpdateStatusText = $"Yeni sürüm mevcut ({UpdateVersionText}).";
                });
            }
        }
        catch { }
    }
}

public enum DeleteConfirmMode
{
    GeneralChoice,
    ListOnlyPrompt,
    DiskAndListPrompt
}

