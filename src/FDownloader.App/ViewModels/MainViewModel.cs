using System.Collections.ObjectModel;



using System.Diagnostics;



using Avalonia;



using Avalonia.Controls.ApplicationLifetimes;



using Avalonia.Threading;



using CommunityToolkit.Mvvm.ComponentModel;



using CommunityToolkit.Mvvm.Input;



using FDownloader.Core.Config;



using FDownloader.Core.Domain;



using FDownloader.Core.Events;



using FDownloader.Core.Manager;



using FDownloader.Core.Network;



using FDownloader.Core.Torrent;



using FDownloader.Core.Utilities;



using FDownloader.Core.Video;



using FDownloader.App.Views;



using FDownloader.App.Services;



namespace FDownloader.App.ViewModels;



public partial class MainViewModel : ViewModelBase, IDisposable



{



    private readonly DownloadManager _downloadManager;



    private readonly DownloadEventBus _eventBus;



    private readonly ClipboardWatcher _clipboardWatcher;



    private readonly YtDlpMediaService _mediaService;



    private readonly SiteGrabberService _siteGrabberService;



    private readonly UpdateService _updateService;



    // ─── Otomatik Güncelleme (Velopack & GitHub Releases) ───────────────────

    [ObservableProperty] private bool _isUpdateAvailable = false;

    [ObservableProperty] private string _updateVersionText = string.Empty;

    [ObservableProperty] private bool _isUpdating = false;

    [ObservableProperty] private int _updateDownloadProgress = 0;

    [ObservableProperty] private string _updateStatusText = string.Empty;

    [ObservableProperty] private bool _isUpdateDialogOpen = false;

    [ObservableProperty] private string _updateChangelog = string.Empty;

    [ObservableProperty] private bool _isLoadingChangelog = false;

    [ObservableProperty] private string _updateTargetVersion = string.Empty;

    [ObservableProperty] private string _updateCurrentVersion = string.Empty;

    [ObservableProperty] private string _updateReleaseDate = string.Empty;

    [ObservableProperty] private string _updateReleaseTitle = string.Empty;

    private Velopack.UpdateInfo? _availableUpdateInfo;

    public bool IsUpdateAvailableAndNotUpdating => IsUpdateAvailable && !IsUpdating;

    public bool HasPendingUpdate => IsUpdateAvailable && !IsCurrentVersionUpToDate;

    public bool HasExtensionUpdate => HasPendingUpdate && 
        (!string.IsNullOrWhiteSpace(UpdateTargetVersion) && 
         (UpdateTargetVersion.Contains("2.0.1") || UpdateTargetVersion.Contains("2.0.2") || UpdateTargetVersion.Contains("2.1")));

    public string DisplayConnectedExtensionVersion =>
        string.IsNullOrWhiteSpace(ConnectedExtensionVersion) ? "v1.0.0" : $"v{ConnectedExtensionVersion.TrimStart('v')}";

    public string DisplayExpectedExtensionVersion =>
        string.IsNullOrWhiteSpace(ExpectedExtensionVersion) ? "v2.0.2" : $"v{ExpectedExtensionVersion.TrimStart('v')}";

    [ObservableProperty] private bool _isExtensionUpdateRequiredModalOpen = false;

    public bool IsCurrentVersionUpToDate =>
        !string.IsNullOrWhiteSpace(UpdateTargetVersion) &&
        !string.IsNullOrWhiteSpace(UpdateCurrentVersion) &&
        UpdateTargetVersion.TrimStart('v').Equals(UpdateCurrentVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase);

    public string UpdateDialogTitle => HasPendingUpdate 
        ? Loc["Update.DialogTitle"] 
        : Loc["Update.ChangelogTitle"];

    public string UpdateDialogCloseButtonText => HasPendingUpdate
        ? Loc["Update.Later"]
        : Loc["Common.Close"];

    public string UpdateHeaderIcon => HasPendingUpdate ? "🚀" : "📋";

    public string UpToDateBadgeText => string.Format(Loc["Update.LatestVersionInstalled"], UpdateCurrentVersion);

    public string AppVersionText
    {
        get
        {
            var v = typeof(MainViewModel).Assembly.GetName().Version;
            return v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v2.0.2";
        }
    }

    public string WindowTitle => "FDownloader - İndirme Yöneticisi";

    partial void OnIsUpdateAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUpdateAvailableAndNotUpdating));
        NotifyUpdateStateProperties();
    }

    partial void OnUpdateTargetVersionChanged(string value)
    {
        NotifyUpdateStateProperties();
    }

    partial void OnUpdateCurrentVersionChanged(string value)
    {
        NotifyUpdateStateProperties();
    }

    private void NotifyUpdateStateProperties()
    {
        OnPropertyChanged(nameof(HasPendingUpdate));
        OnPropertyChanged(nameof(HasExtensionUpdate));
        OnPropertyChanged(nameof(IsCurrentVersionUpToDate));
        OnPropertyChanged(nameof(UpdateDialogTitle));
        OnPropertyChanged(nameof(UpdateDialogCloseButtonText));
        OnPropertyChanged(nameof(UpdateHeaderIcon));
        OnPropertyChanged(nameof(UpToDateBadgeText));
    }



    partial void OnIsUpdatingChanged(bool value)



    {



        OnPropertyChanged(nameof(IsUpdateAvailableAndNotUpdating));



    }



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



    [ObservableProperty] private double _colNameWidth = 220;



    [ObservableProperty] private double _colSizeWidth = 85;



    [ObservableProperty] private double _colProgressWidth = 110;



    [ObservableProperty] private double _colSpeedWidth = 95;



    [ObservableProperty] private double _colMaxSpeedWidth = 80;



    [ObservableProperty] private double _colEtaWidth = 100;



    [ObservableProperty] private double _colPeersWidth = 150;



    [ObservableProperty] private double _colStatusWidth = 120;



    // ─── Alt Panel (BitTorrent & IDM detayları) ───────────────────────────



    [ObservableProperty] private int _selectedDetailTabIndex = 0; // 0: Genel, 1: Parçalar, 2: Torrent Eşleri



    [ObservableProperty] private TorrentDetails? _currentTorrentDetails;



    [ObservableProperty] private string _torrentDetailsSelectAllText = "✅ Tümünü İndir";



    [ObservableProperty] private string _torrentDetailsDeselectAllText = "⬜ Tümünü Bırak";



    [ObservableProperty] private string _torrentDetailsSelectAllBackground = "Transparent";



    [ObservableProperty] private string _torrentDetailsSelectAllBorderBrush = "#33FFFFFF";



    [ObservableProperty] private string _torrentDetailsDeselectAllBackground = "Transparent";



    [ObservableProperty] private string _torrentDetailsDeselectAllBorderBrush = "#33FFFFFF";



    [ObservableProperty] private string _addDialogSelectAllText = "✅ Tümünü Seç";



    [ObservableProperty] private string _addDialogDeselectAllText = "⬜ Tümünü Bırak";



    [ObservableProperty] private string _addDialogSelectAllBackground = "Transparent";



    [ObservableProperty] private string _addDialogSelectAllBorderBrush = "#33FFFFFF";



    [ObservableProperty] private string _addDialogDeselectAllBackground = "Transparent";



    [ObservableProperty] private string _addDialogDeselectAllBorderBrush = "#33FFFFFF";



    // ─── Ayarlar İlerleme Çubuğu Hover Önizleme ────────────────────────────



    [ObservableProperty] private double _previewProgressKlasik = 65;



    [ObservableProperty] private double _previewProgressGradyan = 65;



    [ObservableProperty] private double _previewProgressNeon = 65;



    [ObservableProperty] private double _previewProgressInce = 65;



    // ─── Pano İzleyici ────────────────────────────────────────────────────



    [ObservableProperty] private bool _isClipboardMonitoringEnabled = true;



    // ─── Yeni İndirme Ekleme Diyaloğu ─────────────────────────────────────



    [ObservableProperty] private bool _isAddDialogOpen;



    [ObservableProperty] private string _addDialogUrl = string.Empty;



    [ObservableProperty] private string _addDialogTitle = string.Empty;



    [ObservableProperty] private string _addDialogSavePath = string.Empty;



    [ObservableProperty] private bool _addDialogIsMp3 = true; // Varsayılan MP3
    [ObservableProperty] private bool _addDialogIsMp4 = false;

    // Video ve Ses Kalite/Çözünürlük Seçenekleri (4K, 1440p, 1080p, 720p, 480p, 360p, 320k)
    [ObservableProperty] private ObservableCollection<MediaQualityOption> _addDialogQualityOptions = new();
    [ObservableProperty] private MediaQualityOption? _addDialogSelectedQuality;

    // Torrent Seeding Seçenekleri (Ekle Diyaloğu)
    [ObservableProperty] private bool _addDialogStopSeedingImmediately = false;
    [ObservableProperty] private double _addDialogSeedRatioLimit = 0.50;



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



    // Torrent İndirme Öncesi Dosya Seçim Listesi (Torrent Stili)



        private int _torrentInspectCounter = 0;
    [ObservableProperty] private ObservableCollection<TorrentFileInfo> _addDialogTorrentFiles = new();



    [ObservableProperty] private bool _addDialogHasTorrentFiles = false;
    [ObservableProperty] private bool _addDialogIsTorrentResolving = false;



    [ObservableProperty] private string _addDialogDiskSpaceText = string.Empty;



    [ObservableProperty] private bool _addDialogHasDiskWarning = false;



    [ObservableProperty] private string _addDialogDiskWarningText = string.Empty;



    [ObservableProperty] private string _addDialogTorrentSelectedSizeText = string.Empty;



    partial void OnAddDialogSavePathChanged(string value)



    {



        UpdateAddDialogDiskSpace();



    }



    // ─── Site Yakalayıcı (Site Grabber - hDM Özelliği) ────────────────────



    [ObservableProperty] private bool _isSiteGrabberDialogOpen;



    [ObservableProperty] private string _siteGrabberUrl = string.Empty;



    [ObservableProperty] private bool _siteGrabberIsLoading;



    [ObservableProperty] private string _siteGrabberStatusText = string.Empty;



    [ObservableProperty] private ObservableCollection<GrabbedFileItem> _grabbedFiles = new();



    // ─── Hız İbresi & Hız Sınırları ─────────────────────────────────



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



    public string RateLimitStatusText => IsAltRateLimitActive ? Loc["Status.LowSpeed"] : Loc["Status.HighSpeed"];



    public string RateLimitStatusSubtext => IsAltRateLimitActive ? Loc["Status.Limited"] : Loc["Status.Unlimited"];



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



    // 1. Okyanus Mavisi (Varsayılan)



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



    // 2. Siber Mor



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



    // 3. Platin Gri (Aydınlık gibi ama daha mat/gri tonlar)



    private const string PlatinumGrayWindowBg = "#D8DCE3";



    private const string PlatinumGrayToolbarBg = "#E5E9F0";



    private const string PlatinumGraySidebarBg = "#CED4DE";



    private const string PlatinumGrayCardBg = "#EAEFF5";



    private const string PlatinumGrayTableHeaderBg = "#C5CBD6";



    private const string PlatinumGrayDetailBg = "#D8DCE3";



    private const string PlatinumGrayStatusBarBg = "#E5E9F0";



    private const string PlatinumGrayBorderColor = "#9CA3AF";



    private const string PlatinumGrayForeground = "#111827";



    private const string PlatinumGrayTextSecondary = "#4B5563";



    private const string PlatinumGrayAccent = "#0284C7";



    private const string PlatinumGrayAccentFg = "#FFFFFF";



    private const string PlatinumGraySpeedGraphPanelBg = "#C5CBD6";



    private const string PlatinumGraySpeedGraphInnerBg = "#EAEFF5";



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



    // Canlı Tema Özellikleri (UI Bindingleri - Varsayılan: Okyanus)



    [ObservableProperty] private string _themeWindowBg = OceanBlueWindowBg;



    [ObservableProperty] private string _themeToolbarBg = OceanBlueToolbarBg;



    [ObservableProperty] private string _themeSidebarBg = OceanBlueSidebarBg;



    [ObservableProperty] private string _themeCardBg = OceanBlueCardBg;



    [ObservableProperty] private string _themeTableHeaderBg = OceanBlueTableHeaderBg;



    [ObservableProperty] private string _themeDetailBg = OceanBlueDetailBg;



    [ObservableProperty] private string _themeStatusBarBg = OceanBlueStatusBarBg;



    [ObservableProperty] private string _themeBorderColor = OceanBlueBorderColor;



    [ObservableProperty] private string _themeForeground = OceanBlueForeground;



    [ObservableProperty] private string _themeTextSecondary = OceanBlueTextSecondary;



    [ObservableProperty] private string _themeAccentBg = OceanBlueAccent;



    [ObservableProperty] private string _themeAccentFg = OceanBlueAccentFg;



    [ObservableProperty] private string _themeSpeedGraphPanelBg = OceanBlueSpeedGraphPanelBg;



    [ObservableProperty] private string _themeSpeedGraphInnerBg = OceanBlueSpeedGraphInnerBg;



    [ObservableProperty] private string _themeSpeedCurrentBadgeBg = "#1A10B981";



    [ObservableProperty] private string _themeSpeedCurrentBadgeBorder = "#10B981";



    [ObservableProperty] private string _themeSpeedCurrentLabelFg = "#6EE7B7";



    [ObservableProperty] private string _themeSpeedCurrentValueFg = "#10B981";



    [ObservableProperty] private string _themeSpeedMaxBadgeBg = "#1A8B5CF6";



    [ObservableProperty] private string _themeSpeedMaxBadgeBorder = "#8B5CF6";



    [ObservableProperty] private string _themeSpeedMaxLabelFg = "#C4B5FD";



    [ObservableProperty] private string _themeSpeedMaxValueFg = "#A78BFA";



    [ObservableProperty] private string _themeSpeedMaxColFg = "#6EE7B7";



    // ─── Tema Modu (4 Farklı Renk Paleti) ──────────────────────────────────



    private AppThemeMode _currentThemeMode = AppThemeMode.OceanBlue;



    private AppThemeMode _savedThemeMode = AppThemeMode.OceanBlue;



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



    public bool IsThemePlatinumGray



    {



        get => _currentThemeMode == AppThemeMode.PlatinumGray;



        set { if (value) SetThemeMode(AppThemeMode.PlatinumGray); }



    }



    public bool IsThemeLightClean



    {



        get => _currentThemeMode == AppThemeMode.LightClean;



        set { if (value) SetThemeMode(AppThemeMode.LightClean); }



    }



    public bool IsThemeHighContrast



    {



        get => _currentThemeMode == AppThemeMode.HighContrast;



        set { if (value) SetThemeMode(AppThemeMode.HighContrast); }



    }



    // ─── Çoklu Dil Desteği (P6-02: Localization) ──────────────────────────

    public FDownloader.Core.Localization.LocalizationService Loc => FDownloader.Core.Localization.LocalizationService.Instance;



    public bool IsLanguageTurkish

    {

        get => Loc.IsTurkish;

        set

        {

            if (value)

            {

                SetAppLanguage(FDownloader.Core.Localization.LocalizationService.DefaultLanguage);

            }

        }

    }



    public bool IsLanguageEnglish

    {

        get => Loc.IsEnglish;

        set

        {

            if (value)

            {

                SetAppLanguage(FDownloader.Core.Localization.LocalizationService.EnglishLanguage);

            }

        }

    }



    public void SetAppLanguage(string langCode)

    {

        Loc.CurrentLanguage = langCode;

        _downloadManager.SetSetting(SettingKeyLanguage, langCode);

        foreach (var item in Downloads)

        {

            item.RefreshTranslations();

        }

        UpdateHybridState();

        UpdateGlobalStats();

        UpdateStatusCountTexts();
        UpdateExtensionStatus();
        OnPropertyChanged(nameof(DetailSelectedSourceUrl));
        OnPropertyChanged(nameof(DetailSelectedDownloadedSize));
        OnPropertyChanged(nameof(DetailLastErrorForeground));

        UpdateLimitDisplays();

        OnPropertyChanged(nameof(IsLanguageTurkish));

        OnPropertyChanged(nameof(IsLanguageEnglish));

        OnPropertyChanged(nameof(Loc));

        OnPropertyChanged(nameof(FirstRunWizardNextButtonText));

        OnPropertyChanged(nameof(FirstRunWizardStepIndicatorText));

        OnPropertyChanged(nameof(RateLimitStatusText));

        OnPropertyChanged(nameof(RateLimitStatusSubtext));

        OnPropertyChanged(nameof(SelectedHybridText));

        OnPropertyChanged(nameof(SelectedHybridTooltip));

        OnPropertyChanged(nameof(DetailSelectedFileName));

        OnPropertyChanged(nameof(DetailSelectedSavePath));

        OnPropertyChanged(nameof(DetailSelectedLastError));

        OnPropertyChanged(nameof(DownloadLimitDisplayText));

        OnPropertyChanged(nameof(UploadLimitDisplayText));

        OnPropertyChanged(nameof(RecentLogsText));

        OnPropertyChanged(string.Empty);

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



    private const string SettingKeyAutoExtractArchives = "Archive.AutoExtract";



    private const string SettingKeyExtractArchivesToSubfolder = "Archive.ExtractToSubfolder";



    private const string SettingKeyDeleteArchiveAfterExtract = "Archive.DeleteAfterExtract";



    private const string SettingKeyShowSuggestions = "Ui.ShowSuggestions";



    private const string SettingKeyStartWithWindows = "System.StartWithWindows";



    private const string SettingKeyStartMinimizedToTray = "System.StartMinimizedToTray";



    private const string SettingKeyMinimizeToTrayOnClose = "System.MinimizeToTrayOnClose";



    private const string SettingKeyShowTrayIcon = "System.ShowTrayIcon";

    private const string SettingKeyPreventSleepWhileDownloading = "System.PreventSleepWhileDownloading";

    private const string SettingKeyWarnOnExitWithActiveDownloads = "System.WarnOnExitWithActiveDownloads";

    private const string SettingKeyDefaultSaveDirectory = "Download.DefaultSaveDirectory";

    private const string SettingKeyFirstRunCompleted = "App.FirstRunCompleted";

    private const string SettingKeyLanguage = "Ui.Language";

    private const string SettingKeyGlobalDownloadLimitKiB = "Network.GlobalDownloadLimitKiB";

    private const string SettingKeyGlobalUploadLimitKiB = "Network.GlobalUploadLimitKiB";

    private const string SettingKeyAltDownloadLimitKiB = "Network.AltDownloadLimitKiB";

    private const string SettingKeyAltUploadLimitKiB = "Network.AltUploadLimitKiB";

    private const string SettingKeyIsAltRateLimitActive = "Network.IsAltRateLimitActive";



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



        OnPropertyChanged(nameof(IsThemeOceanBlue));



        OnPropertyChanged(nameof(IsThemeCyberPurple));



        OnPropertyChanged(nameof(IsThemePlatinumGray));



        OnPropertyChanged(nameof(IsThemeLightClean));



        OnPropertyChanged(nameof(IsThemeHighContrast));



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



                ThemeSpeedCurrentBadgeBg = "#1A10B981";



                ThemeSpeedCurrentBadgeBorder = "#10B981";



                ThemeSpeedCurrentLabelFg = "#6EE7B7";



                ThemeSpeedCurrentValueFg = "#10B981";



                ThemeSpeedMaxBadgeBg = "#1A8B5CF6";



                ThemeSpeedMaxBadgeBorder = "#8B5CF6";



                ThemeSpeedMaxLabelFg = "#C4B5FD";



                ThemeSpeedMaxValueFg = "#A78BFA";



                ThemeSpeedMaxColFg = "#6EE7B7";



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



                ThemeSpeedCurrentBadgeBg = "#1A10B981";



                ThemeSpeedCurrentBadgeBorder = "#10B981";



                ThemeSpeedCurrentLabelFg = "#6EE7B7";



                ThemeSpeedCurrentValueFg = "#10B981";



                ThemeSpeedMaxBadgeBg = "#1A8B5CF6";



                ThemeSpeedMaxBadgeBorder = "#8B5CF6";



                ThemeSpeedMaxLabelFg = "#C4B5FD";



                ThemeSpeedMaxValueFg = "#A78BFA";



                ThemeSpeedMaxColFg = "#6EE7B7";



                break;



            case AppThemeMode.PlatinumGray:



                ThemeWindowBg = PlatinumGrayWindowBg;



                ThemeToolbarBg = PlatinumGrayToolbarBg;



                ThemeSidebarBg = PlatinumGraySidebarBg;



                ThemeCardBg = PlatinumGrayCardBg;



                ThemeTableHeaderBg = PlatinumGrayTableHeaderBg;



                ThemeDetailBg = PlatinumGrayDetailBg;



                ThemeStatusBarBg = PlatinumGrayStatusBarBg;



                ThemeBorderColor = PlatinumGrayBorderColor;



                ThemeForeground = PlatinumGrayForeground;



                ThemeTextSecondary = PlatinumGrayTextSecondary;



                ThemeAccentBg = PlatinumGrayAccent;



                ThemeAccentFg = PlatinumGrayAccentFg;



                ThemeSpeedGraphPanelBg = PlatinumGraySpeedGraphPanelBg;



                ThemeSpeedGraphInnerBg = PlatinumGraySpeedGraphInnerBg;



                ThemeSpeedCurrentBadgeBg = "#D1FAE5";



                ThemeSpeedCurrentBadgeBorder = "#059669";



                ThemeSpeedCurrentLabelFg = "#065F46";



                ThemeSpeedCurrentValueFg = "#047857";



                ThemeSpeedMaxBadgeBg = "#EDE9FE";



                ThemeSpeedMaxBadgeBorder = "#7C3AED";



                ThemeSpeedMaxLabelFg = "#5B21B6";



                ThemeSpeedMaxValueFg = "#6D28D9";



                ThemeSpeedMaxColFg = "#047857";



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



                ThemeSpeedCurrentBadgeBg = "#ECFDF5";



                ThemeSpeedCurrentBadgeBorder = "#10B981";



                ThemeSpeedCurrentLabelFg = "#065F46";



                ThemeSpeedCurrentValueFg = "#047857";



                ThemeSpeedMaxBadgeBg = "#F5F3FF";



                ThemeSpeedMaxBadgeBorder = "#8B5CF6";



                ThemeSpeedMaxLabelFg = "#5B21B6";



                ThemeSpeedMaxValueFg = "#6D28D9";



                ThemeSpeedMaxColFg = "#059669";



                break;



            default:



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



                ThemeSpeedCurrentBadgeBg = "#1A10B981";



                ThemeSpeedCurrentBadgeBorder = "#10B981";



                ThemeSpeedCurrentLabelFg = "#6EE7B7";



                ThemeSpeedCurrentValueFg = "#10B981";



                ThemeSpeedMaxBadgeBg = "#1A8B5CF6";



                ThemeSpeedMaxBadgeBorder = "#8B5CF6";



                ThemeSpeedMaxLabelFg = "#C4B5FD";



                ThemeSpeedMaxValueFg = "#A78BFA";



                ThemeSpeedMaxColFg = "#6EE7B7";



                break;



            case AppThemeMode.HighContrast:



                ThemeWindowBg = "#000000";



                ThemeToolbarBg = "#000000";



                ThemeSidebarBg = "#000000";



                ThemeCardBg = "#000000";



                ThemeTableHeaderBg = "#000000";



                ThemeDetailBg = "#000000";



                ThemeStatusBarBg = "#000000";



                ThemeBorderColor = "#FFFFFF";



                ThemeForeground = "#FFFFFF";



                ThemeTextSecondary = "#FFFF00";



                ThemeAccentBg = "#FFFF00";



                ThemeAccentFg = "#000000";



                ThemeSpeedGraphPanelBg = "#000000";



                ThemeSpeedGraphInnerBg = "#000000";



                ThemeSpeedCurrentBadgeBg = "#000000";



                ThemeSpeedCurrentBadgeBorder = "#00FF00";



                ThemeSpeedCurrentLabelFg = "#00FF00";



                ThemeSpeedCurrentValueFg = "#00FF00";



                ThemeSpeedMaxBadgeBg = "#000000";



                ThemeSpeedMaxBadgeBorder = "#00FFFF";



                ThemeSpeedMaxLabelFg = "#00FFFF";



                ThemeSpeedMaxValueFg = "#00FFFF";



                ThemeSpeedMaxColFg = "#00FF00";



                break;



        }



        try



        {



            if (Avalonia.Application.Current is Avalonia.Application app)



            {



                app.RequestedThemeVariant = (mode is AppThemeMode.LightClean or AppThemeMode.PlatinumGray) 



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



    // ─── Dosya Çakışma Diyaloğu (Dosya Zaten Mevcut) ────────────────────────



    [ObservableProperty] private bool _isFileConflictDialogOpen = false;



    [ObservableProperty] private string _conflictExistingFileName = string.Empty;



    [ObservableProperty] private string _conflictExistingFilePath = string.Empty;



    [ObservableProperty] private string _conflictExistingFileSizeDisplay = string.Empty;



    [ObservableProperty] private string _conflictExistingFileDateDisplay = string.Empty;



    [ObservableProperty] private string _conflictExistingFileTypeDisplay = string.Empty;



    [ObservableProperty] private string _conflictExistingFileCategoryIcon = "📄";



    [ObservableProperty] private string _conflictNewFileName = string.Empty;



    [ObservableProperty] private string _conflictNewUrl = string.Empty;



    [ObservableProperty] private string _conflictNewFileSizeDisplay = "Bilinmiyor";



    [ObservableProperty] private string _conflictNewFileTypeDisplay = string.Empty;



    [ObservableProperty] private string _conflictNewFileCategoryIcon = "📥";



    [ObservableProperty] private string _conflictSaveDirectory = string.Empty;



    private PendingDownloadRequest? _pendingDownloadRequest;



    // ─── 8. Link Refresh (İndirme Adresini Yenile - IDM Tarzı) ──────────────



    [ObservableProperty] private bool _isRefreshLinkDialogOpen = false;



    [ObservableProperty] private string _refreshLinkOldUrl = string.Empty;



    [ObservableProperty] private string _refreshLinkNewUrl = string.Empty;



    [ObservableProperty] private string _refreshLinkStatusText = string.Empty;



    [ObservableProperty] private bool _refreshLinkIsLoading = false;



    private DownloadItemViewModel? _targetRefreshItem;



    // ─── 9. Artık / Geçici Dosya Temizleyici ────────────────────────────────



    [ObservableProperty] private bool _isCleanTempFilesDialogOpen = false;



    [ObservableProperty] private ObservableCollection<TempFileInfoItem> _orphanTempFiles = new();



    [ObservableProperty] private string _totalOrphanSizeText = "0 B";



    [ObservableProperty] private string _cleanTempFilesStatusText = string.Empty;



    // ─── 10. Dosya Bütünlüğü (Checksum / Hash Doğrulama) ─────────────────────



    [ObservableProperty] private bool _isChecksumDialogOpen = false;



    [ObservableProperty] private string _checksumFileName = string.Empty;



    [ObservableProperty] private string _checksumFilePath = string.Empty;



    [ObservableProperty] private string _checksumMd5 = string.Empty;



    [ObservableProperty] private string _checksumSha1 = string.Empty;



    [ObservableProperty] private string _checksumSha256 = string.Empty;



    [ObservableProperty] private string _checksumExpectedHash = string.Empty;



    [ObservableProperty] private string _checksumMatchStatusText = string.Empty;



    [ObservableProperty] private string _checksumMatchStatusColor = "#10B981";



    [ObservableProperty] private double _checksumProgressPercent = 0;



    [ObservableProperty] private bool _isCalculatingChecksum = false;



    // ─── 11. Gelişmiş İndirme Zamanlayıcısı (Scheduler) ─────────────────────



    [ObservableProperty] private bool _isSchedulerDialogOpen = false;



    [ObservableProperty] private bool _isSchedulerEnabled = false;



    [ObservableProperty] private string _schedulerStartTime = "02:00";



    [ObservableProperty] private string _schedulerEndTime = "07:00";



    [ObservableProperty] private int _schedulerStartActionIndex = 0; // 0: Tümünü Başlat, 1: Hız Limitini Kaldır



    [ObservableProperty] private int _schedulerEndActionIndex = 0;   // 0: Tümünü Duraklat, 1: Hız Limiti Uygula, 2: Bilgisayarı Kapat



    [ObservableProperty] private bool _schedulerMonday = true;



    [ObservableProperty] private bool _schedulerTuesday = true;



    [ObservableProperty] private bool _schedulerWednesday = true;



    [ObservableProperty] private bool _schedulerThursday = true;



    [ObservableProperty] private bool _schedulerFriday = true;



    [ObservableProperty] private bool _schedulerSaturday = true;



    [ObservableProperty] private bool _schedulerSunday = true;



    [ObservableProperty] private string _schedulerStatusSummary = "Zamanlayıcı Kapalı";



    private System.Timers.Timer? _schedulerTimer;



    // ─── Ayarlar Modalı (Çark Butonu) ─────────────────────────────────────



    public const int SettingsTabAppearance = 4;



    public const int SettingsTabNotification = 2;



    public const int SettingsTabSpeedAndNetwork = 0;



    public const int SettingsTabStorage = 1;



    public const int SettingsTabTorrent = 3;

    public const int SettingsTabBitTorrent = 3;



    public const int SettingsTabSiteGrabber = 5;



    public const int SettingsTabBrowser = 6;



    public const int SettingsTabBehavior = 7;



    public const int SettingsTabAbout = 8;



    // ─── Tarayıcı Eklentisi Durumu ve Sürüm Yönetimi ──────────────────────



    public const string TargetExtensionVersion = "2.0.2";



    public const double ExtensionHeartbeatTimeoutSeconds = 65.0;



    public const string ExtensionStatusColorWaiting = "#334155";      // Slate koyu mavi/gri (Tarayıcı bekleniyor)



    public const string ExtensionStatusColorOutdated = "#F59E0B";     // Turuncu (Güncelleme gerekli)



    public const string ExtensionStatusColorActive = "#10B981";       // Yeşil (Aktif & Güncel)



    [ObservableProperty] private string _extensionStatusBadgeText = "";



    [ObservableProperty] private string _extensionStatusBadgeBackground = ExtensionStatusColorWaiting;



    [ObservableProperty] private string _extensionStatusBadgeForeground = "#CBD5E1";



    [ObservableProperty] private string _extensionStatusBadgeToolTip = "FDownloader yerel köprüsü (127.0.0.1:9666) hazır ve dinliyor. Tarayıcınızı açtığınızda eklenti otomatik bağlanacaktır.";



    [ObservableProperty] private bool _isExtensionConnected = false;



    [ObservableProperty] private bool _isExtensionUpdateAvailable = false;



    [ObservableProperty] private string _connectedExtensionVersion = string.Empty;



    [ObservableProperty] private string _expectedExtensionVersion = TargetExtensionVersion;



    private DateTime? _lastExtensionHeartbeat;
    private bool _hasAutoPromptedExtensionUpdate = false;



    [ObservableProperty] private bool _isSettingsDialogOpen;



    // ─── İlk Açılış Sihirbazı (P6-01: First Run Wizard) ─────────────────

    [ObservableProperty] private bool _isFirstRunWizardOpen;

    [ObservableProperty] private int _firstRunWizardStep = 1; // 1: Karşılama, 2: Klasör & Başlangıç, 3: Tarayıcı Eklentisi

    [ObservableProperty] private bool _isFirstRunWizardCompleted;



    public bool IsFirstRunWizardStep1 => FirstRunWizardStep == 1;

    public bool IsFirstRunWizardStep2 => FirstRunWizardStep == 2;

    public bool IsFirstRunWizardStep3 => FirstRunWizardStep == 3;

    public bool CanGoBackFirstRunWizard => FirstRunWizardStep > 1;

    public string FirstRunWizardStepIndicatorText => Loc.IsEnglish ? $"Step {FirstRunWizardStep} / 3" : $"Adım {FirstRunWizardStep} / 3";

    public string FirstRunWizardNextButtonText => FirstRunWizardStep >= 3 

        ? Loc["Wizard.Complete"] 

        : Loc["Wizard.Next"];



    partial void OnFirstRunWizardStepChanged(int value)

    {

        OnPropertyChanged(nameof(IsFirstRunWizardStep1));

        OnPropertyChanged(nameof(IsFirstRunWizardStep2));

        OnPropertyChanged(nameof(IsFirstRunWizardStep3));

        OnPropertyChanged(nameof(CanGoBackFirstRunWizard));

        OnPropertyChanged(nameof(FirstRunWizardStepIndicatorText));

        OnPropertyChanged(nameof(FirstRunWizardNextButtonText));

    }



    [ObservableProperty] private int _settingsTabIndex = SettingsTabSpeedAndNetwork;



    [ObservableProperty] private string _settingsDefaultSaveDirectory = string.Empty;



    [ObservableProperty] private int _settingsMaxConcurrentDownloads = 4;



    [ObservableProperty] private int _settingsDefaultSegments = 8;



    [ObservableProperty] private bool _enableSuggestions = true;



    [ObservableProperty] private bool _isSuggestionBannerVisible = false;



    // ─── BitTorrent Ayarları (Torrent Modülü) ───────────────────────────



    [ObservableProperty] private int _torrentListeningPort = 55123;



    [ObservableProperty] private bool _torrentEnableUpnp = true;



    [ObservableProperty] private bool _torrentEnableDht = true;



    [ObservableProperty] private bool _torrentEnablePex = true;



    [ObservableProperty] private bool _torrentEnableLsd = true;



    [ObservableProperty] private int _torrentEncryptionMode = 0; // 0: Tercih Et, 1: Zorunlu Kıl, 2: Devre Dışı



    [ObservableProperty] private int _torrentMaxConnectionsTotal = 200;



    [ObservableProperty] private int _torrentMaxConnectionsPerTorrent = 60;



    [ObservableProperty] private bool _torrentEnableAutoSequential = false;



    [ObservableProperty] private double _torrentSeedRatioLimit = 1.0;



    [ObservableProperty] private bool _torrentStopSeedingWhenRatioReached = false;



    [ObservableProperty] private string? _torrentNetworkInterfaceId;



    // ─── Otomatik Arşiv Çıkarma (Auto-Extract) ─────────────────────────────



    [ObservableProperty] private bool _autoExtractArchives = false;



    [ObservableProperty] private bool _extractArchivesToSubfolder = true;



    [ObservableProperty] private bool _deleteArchiveAfterExtract = false;



    // ─── Sistem Başlangıcı ve Tepsi (System Tray) Davranışı ────────────────



    [ObservableProperty] private bool _startWithWindows = false;



    [ObservableProperty] private bool _startMinimizedToTray = false;



    [ObservableProperty] private bool _minimizeToTrayOnClose = true;



    [ObservableProperty] private bool _showTrayIcon = true;

    [ObservableProperty] private bool _preventSleepWhileDownloading = true;

    [ObservableProperty] private bool _warnOnExitWithActiveDownloads = true;

    [ObservableProperty] private bool _isExitConfirmDialogOpen = false;

    [ObservableProperty] private string _exitConfirmMessage = string.Empty;

    public event Action? RequestCloseWindow;

    public event Action? RequestHideToTray;



    // ─── Ağ Adaptörü & VPN Kill-Switch ─────────────────────────────────────



    [ObservableProperty] private ObservableCollection<NetworkInterfaceInfo> _networkInterfaces = new();



    [ObservableProperty] private NetworkInterfaceInfo? _selectedNetworkInterface;



    // İzleyici Ekleme Modalı



    [ObservableProperty] private bool _isAddTrackerDialogOpen = false;



    [ObservableProperty] private string _newTrackerUrlsText = string.Empty;



    // ─── Arşiv Parolası Giriş Modalı ─────────────────────────────────────────



    [ObservableProperty] private bool _isArchivePasswordDialogOpen = false;



    [ObservableProperty] private string _archivePasswordFilePath = string.Empty;



    [ObservableProperty] private string _archivePasswordFileName = string.Empty;



    [ObservableProperty] private string _archivePasswordInput = string.Empty;



    [ObservableProperty] private string? _archivePasswordErrorMessage;



    [ObservableProperty] private bool _isArchiveExtracting = false;



    [ObservableProperty] private bool _isArchivePasswordVisible = false;



    public string ArchivePasswordChar => IsArchivePasswordVisible ? "" : "●";



    public string ArchivePasswordVisibilityIcon => IsArchivePasswordVisible ? "🙈" : "👁️";



    [RelayCommand]



    public void ToggleArchivePasswordVisibility()



    {



        IsArchivePasswordVisible = !IsArchivePasswordVisible;

        OnPropertyChanged(nameof(ArchivePasswordChar));

        OnPropertyChanged(nameof(ArchivePasswordVisibilityIcon));

    }

    private const string SettingKeyArchiveSavedPasswords = "Archive.SavedPasswords";
    public ObservableCollection<string> SavedArchivePasswords { get; } = new();
    [ObservableProperty] private bool _hasSavedArchivePasswords = false;
    [ObservableProperty] private bool _rememberArchivePassword = true;

    public void LoadSavedArchivePasswords()
    {
        try
        {
            SavedArchivePasswords.Clear();
            var json = _downloadManager.GetSetting(SettingKeyArchiveSavedPasswords);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    foreach (var item in list.Where(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        SavedArchivePasswords.Add(item);
                    }
                }
            }
        }
        catch { }
        HasSavedArchivePasswords = SavedArchivePasswords.Count > 0;
    }

    public void PersistArchivePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return;
        try
        {
            var trimmed = password.Trim();
            var existing = SavedArchivePasswords.ToList();
            existing.RemoveAll(p => string.Equals(p, trimmed, StringComparison.Ordinal));
            existing.Insert(0, trimmed);
            if (existing.Count > 30) existing = existing.Take(30).ToList();

            SavedArchivePasswords.Clear();
            foreach (var p in existing) SavedArchivePasswords.Add(p);
            HasSavedArchivePasswords = SavedArchivePasswords.Count > 0;

            var json = System.Text.Json.JsonSerializer.Serialize(existing);
            _downloadManager.SetSetting(SettingKeyArchiveSavedPasswords, json);
        }
        catch { }
    }

    [RelayCommand]
    public void SelectSavedArchivePassword(string? pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return;
        ArchivePasswordInput = pwd;
        ArchivePasswordErrorMessage = null;
    }

    [RelayCommand]
    public void DeleteSavedArchivePassword(string? pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return;
        try
        {
            SavedArchivePasswords.Remove(pwd);
            HasSavedArchivePasswords = SavedArchivePasswords.Count > 0;
            var json = System.Text.Json.JsonSerializer.Serialize(SavedArchivePasswords.ToList());
            _downloadManager.SetSetting(SettingKeyArchiveSavedPasswords, json);
        }
        catch { }
    }



    // ─── İstatistikler, Canlı Hız Grafiği ve Durum Çubuğu ─────────────────



    [ObservableProperty] private string _totalDownloadSpeedText = "0 B/s";



    [ObservableProperty] private string _totalUploadSpeedText = "0 B/s";



    [ObservableProperty] private string _downloadLimitDisplayText = "[∞]";



    [ObservableProperty] private string _uploadLimitDisplayText = "[∞]";



    [ObservableProperty] private string _activeCountText = "0 İndiriliyor";



    [ObservableProperty] private ObservableCollection<double> _speedHistory = new();



    // ─── Tam Boy Canlı Hız Grafiği (Üst Menü Altında) ──────────────────────



    [ObservableProperty] private bool _isSpeedGraphVisible = true;



    [ObservableProperty] private string _speedGraphToggleTooltip = "Hız Grafiğini Gizle";



    partial void OnIsSpeedGraphVisibleChanged(bool value)



    {



        SpeedGraphToggleTooltip = value ? "Hız Grafiğini Gizle" : "Hız Grafiğini Göster";



    }



    [ObservableProperty] private string _graphTitle = string.Empty;



    [ObservableProperty] private string _graphCurrentSpeedText = "0 B/s";



    [ObservableProperty] private string _graphMinSpeedText = "0 B/s";



    [ObservableProperty] private string _graphMaxSpeedText = "0 B/s";



    [ObservableProperty] private string _graphDetailsText = "Aktif İndirme: 0";



    [ObservableProperty] private ObservableCollection<double> _displaySpeedHistory = new();



    private ObservableCollection<double> _globalSpeedHistory = new();



    private double _globalMinSpeedRecorded;



    private double _globalMaxSpeedRecorded;



    private readonly DispatcherTimer _uiUpdateTimer;



    // Alt durum çubuğu sayaçları (hDM stili)



    [ObservableProperty] private int _totalCount = 0;



    [ObservableProperty] private int _completedCount = 0;



    [ObservableProperty] private int _activeCount = 0;



    [ObservableProperty] private int _pausedCount = 0;



    [ObservableProperty] private int _failedCount = 0;



    [ObservableProperty] private string _totalCountText = "";



    [ObservableProperty] private string _completedCountText = "";



    [ObservableProperty] private string _activeStatusCountText = "";



    [ObservableProperty] private string _pausedCountText = "";



    [ObservableProperty] private string _failedCountText = "";



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



        // UI Performans Zamanlayıcısı: Global istatistikleri ve sparkline grafiğini saniyede 2 kez (500ms) düzenli günceller



        // Canlı Çoklu Dil (i18n) Olay Dinleyicisi: Dil değiştiğinde tüm UI binding'lerini anında yeniler

        FDownloader.Core.Localization.LocalizationService.Instance.PropertyChanged += (_, _) =>

        {

            foreach (var item in Downloads)

            {

                item.RefreshTranslations();

            }

            UpdateHybridState();

            UpdateGlobalStats();

            OnPropertyChanged(nameof(Loc));

            OnPropertyChanged(nameof(IsLanguageTurkish));

            OnPropertyChanged(nameof(IsLanguageEnglish));

            OnPropertyChanged(nameof(FirstRunWizardNextButtonText));

            OnPropertyChanged(nameof(FirstRunWizardStepIndicatorText));

            OnPropertyChanged(nameof(RateLimitStatusText));

            OnPropertyChanged(nameof(RateLimitStatusSubtext));

            OnPropertyChanged(nameof(SelectedHybridText));

            OnPropertyChanged(nameof(SelectedHybridTooltip));

        OnPropertyChanged(nameof(DetailSelectedFileName));

        OnPropertyChanged(nameof(DetailSelectedSavePath));

        OnPropertyChanged(nameof(DetailSelectedLastError));

        OnPropertyChanged(nameof(DownloadLimitDisplayText));

        OnPropertyChanged(nameof(UploadLimitDisplayText));

        OnPropertyChanged(nameof(RecentLogsText));

            OnPropertyChanged(string.Empty);

        };



        _uiUpdateTimer = new DispatcherTimer(DispatcherPriority.Background)



        {



            Interval = TimeSpan.FromMilliseconds(500)



        };



        _uiUpdateTimer.Tick += (_, _) => UpdateGlobalStats();



        _uiUpdateTimer.Start();



        _clipboardWatcher.IsEnabled = true;



        InitializeScheduler();



        InitializeScheduler();



        var defaultDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");



        var savedLang = _downloadManager.GetSetting(SettingKeyLanguage);

        if (!string.IsNullOrWhiteSpace(savedLang))

        {

            Loc.CurrentLanguage = savedLang;

        }

        else

        {

            // İlk açılışta sistem dilini otomatik algıla

            Loc.CurrentLanguage = FDownloader.Core.Localization.LocalizationService.DetectSystemLanguage();

        }



        var savedSaveDir = _downloadManager.GetSetting(SettingKeyDefaultSaveDirectory);

        if (!string.IsNullOrWhiteSpace(savedSaveDir) && Directory.Exists(savedSaveDir))

        {

            defaultDownloads = savedSaveDir;

        }



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



        // BitTorrent ayarlarını yükle



        var tSettings = _downloadManager.GetTorrentSettings();



        TorrentListeningPort = tSettings.ListeningPort;



        TorrentEnableUpnp = tSettings.EnableUpnp;



        TorrentEnableDht = tSettings.EnableDht;



        TorrentEnablePex = tSettings.EnablePeX;



        TorrentEnableLsd = tSettings.EnableLsd;



        TorrentEncryptionMode = tSettings.EncryptionMode;



        TorrentMaxConnectionsTotal = tSettings.MaxConnectionsTotal;



        TorrentMaxConnectionsPerTorrent = tSettings.MaxConnectionsPerTorrent;



        TorrentEnableAutoSequential = tSettings.EnableAutoSequentialForVideo;



        TorrentSeedRatioLimit = tSettings.SeedRatioLimit;



        TorrentStopSeedingWhenRatioReached = tSettings.StopSeedingWhenRatioReached;



        TorrentNetworkInterfaceId = tSettings.NetworkInterfaceId;



        LoadNetworkInterfaces(tSettings.NetworkInterfaceId);



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyAutoExtractArchives), out var autoExt))



            AutoExtractArchives = autoExt;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyExtractArchivesToSubfolder), out var subf))



            ExtractArchivesToSubfolder = subf;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyDeleteArchiveAfterExtract), out var delArch))
            DeleteArchiveAfterExtract = delArch;

        LoadSavedArchivePasswords();

        var showSugStr = _downloadManager.GetSetting(SettingKeyShowSuggestions);



        if (bool.TryParse(showSugStr, out var showSug))



            EnableSuggestions = showSug;



        else



            EnableSuggestions = true;



        IsSuggestionBannerVisible = EnableSuggestions;



        // Sistem Başlangıcı ve Tepsi Ayarları



        if (OperatingSystem.IsWindows())



        {



            StartWithWindows = Services.WindowsStartupService.IsStartupEnabled();



        }



        else if (bool.TryParse(_downloadManager.GetSetting(SettingKeyStartWithWindows), out var sww))



        {



            StartWithWindows = sww;



        }



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyStartMinimizedToTray), out var smt))



            StartMinimizedToTray = smt;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyMinimizeToTrayOnClose), out var mtc))



            MinimizeToTrayOnClose = mtc;



        else



            MinimizeToTrayOnClose = true;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyShowTrayIcon), out var sti))



            ShowTrayIcon = sti;



        else



            ShowTrayIcon = true;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyPreventSleepWhileDownloading), out var pswd))

            PreventSleepWhileDownloading = pswd;

        else

            PreventSleepWhileDownloading = true;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyWarnOnExitWithActiveDownloads), out var woe))

            WarnOnExitWithActiveDownloads = woe;

        else

            WarnOnExitWithActiveDownloads = true;



                // Hız Sınırları ve Yavaş/Yüksek Hız Modu Yükleme
        if (long.TryParse(_downloadManager.GetSetting(SettingKeyGlobalDownloadLimitKiB), out var gdl))
            GlobalDownloadLimitKiB = gdl;

        if (long.TryParse(_downloadManager.GetSetting(SettingKeyGlobalUploadLimitKiB), out var gul))
            GlobalUploadLimitKiB = gul;

        if (long.TryParse(_downloadManager.GetSetting(SettingKeyAltDownloadLimitKiB), out var adl))
            AltDownloadLimitKiB = adl;

        if (long.TryParse(_downloadManager.GetSetting(SettingKeyAltUploadLimitKiB), out var aul))
            AltUploadLimitKiB = aul;

        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyIsAltRateLimitActive), out var bArl))
            IsAltRateLimitActive = bArl;

        UpdateLimitDisplays();

        // P6-01: İlk Açılış Sihirbazı kontrolü (UI thread üzerinde pencere ve bağlam yüklendikten hemen sonra tetikle)
        Dispatcher.UIThread.Post(() =>
        {
            var firstRunCompletedStr = _downloadManager.GetSetting(SettingKeyFirstRunCompleted);
            if (bool.TryParse(firstRunCompletedStr, out var frc) && frc)
            {
                IsFirstRunWizardCompleted = true;
                IsFirstRunWizardOpen = false;
            }
            else
            {
                IsFirstRunWizardCompleted = false;
                IsFirstRunWizardOpen = true;
                FirstRunWizardStep = 1;
            }
        });

    }



    private void LoadNetworkInterfaces(string? savedId)



    {



        NetworkInterfaces.Clear();



        var defaultItem = new NetworkInterfaceInfo(



            "",



            "Varsayılan",



            "Tüm Ağ Adaptörleri",



            "🌐 Varsayılan (Tüm Ağ Adaptörleri - Otomatik)",



            null,



            true,



            false);



        NetworkInterfaces.Add(defaultItem);



        var list = NetworkInterfaceHelper.GetAvailableInterfaces();



        foreach (var item in list)



        {



            NetworkInterfaces.Add(item);



        }



        SelectedNetworkInterface = NetworkInterfaces.FirstOrDefault(n => !string.IsNullOrEmpty(savedId) && (n.Id == savedId || n.Name == savedId))



            ?? defaultItem;



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



                    if (item.Status is DownloadStatus.Paused or DownloadStatus.Completed or DownloadStatus.Cancelled or DownloadStatus.Failed)



                    {



                        return;



                    }



                    item.Status = DownloadStatus.Downloading;



                    item.DownloadedBytes = e.DownloadedBytes;



                    if (e.TotalBytes > 0) item.TotalBytes = e.TotalBytes;



                    item.ProgressPercent = e.ProgressPercent;



                                        item.SpeedBytesPerSecond = e.SpeedBytesPerSecond;



                    item.EtaDisplay = e.Eta.HasValue ? FormatEta(e.Eta.Value) : "-";



                    // Hız sıfıra düştü ve bir süre düzelmedi kontrolü



                    if (item.Status == DownloadStatus.Downloading)



                    {



                        // Torrent henüz meta verisi arama aşamasındaysa hız 0 olması doğaldır, bağlantı koptu sayılmaz



                        bool isTorrentSearchingMetadata = item.IsTorrent && (item.TotalBytes == 0 || item.IsTorrentMetaSearching);



                        if (e.SpeedBytesPerSecond <= 0)



                        {



                            if (!isTorrentSearchingMetadata)



                            {



                                item.ConsecutiveZeroSpeedCount++;



                                // Yaklaşık 6-7 saniye boyunca veri akışı durduysa



                                if (item.ConsecutiveZeroSpeedCount >= 16 && !item.IsStalled)



                                {



                                    item.IsStalled = true;



                                    SoundPlayerService.PlayWarning();



                                }



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
                            item.IsSeeding = details.State.Contains("Seed", StringComparison.OrdinalIgnoreCase);



                            if (SelectedDownload?.Id == e.TaskId)



                            {



                                CurrentTorrentDetails = details;



                            }



                        }



                    }



                }



            });



        });



        // 3.5. Görev Adı / Dosya Adı Güncellendi

        _eventBus.Subscribe<TaskRenamedEvent>(e =>

        {

            Dispatcher.UIThread.Post(() =>

            {

                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);

                if (item != null && !string.IsNullOrWhiteSpace(e.NewName))

                {

                    item.FileName = e.NewName;

                    ApplyFilter();

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



                // 📦 Otomatik Arşiv Çıkarma (Auto-Extract)



                // 🌊 Torrent Dosyası İndirildiğinde Kullanıcıya Dosya Listesini Aç

                if (!string.IsNullOrEmpty(e.FilePath) && e.FilePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(e.FilePath))

                {

                    Dispatcher.UIThread.Post(async () =>

                    {

                        try

                        {

                            RequestBringToFront?.Invoke();

                            await HandleDroppedFileOrUrlAsync(e.FilePath);

                        }

                        catch { }

                    });

                }



                if (AutoExtractArchives && !string.IsNullOrEmpty(e.FilePath) && ArchiveExtractorService.IsFirstVolumeOrStandalone(e.FilePath))



                {



                    _ = Task.Run(async () =>



                    {



                        var result = await _downloadManager.ArchiveExtractor.ExtractAsync(
                            e.FilePath,
                            createSubfolder: ExtractArchivesToSubfolder);

                        string? autoMatchedPassword = null;
                        if (!result.Success && result.IsPasswordProtected)
                        {
                            var savedPwds = SavedArchivePasswords.ToList();
                            foreach (var pwd in savedPwds)
                            {
                                var tryResult = await _downloadManager.ArchiveExtractor.ExtractAsync(
                                    e.FilePath,
                                    createSubfolder: ExtractArchivesToSubfolder,
                                    password: pwd);

                                if (tryResult.Success)
                                {
                                    result = tryResult;
                                    autoMatchedPassword = pwd;
                                    break;
                                }
                            }
                        }

                        Dispatcher.UIThread.Post(() =>
                        {



                            if (result.Success)



                            {



                                var notifMsg = autoMatchedPassword != null
                                    ? $"{result.ExtractedFilesCount} dosya kayıtlı parola ('{autoMatchedPassword}') ile otomatik açıldı."
                                    : $"{result.ExtractedFilesCount} dosya klasöre başarıyla açıldı.";

                                ShowNotification("📚 Arşiv Çıkarıldı", notifMsg, result.ExtractedDirectory, "📚", _currentCompletionSound, true);

                                if (autoMatchedPassword != null)
                                {
                                    FDownloader.Core.Logging.AppFileLoggerProvider.AppendLog(
                                        Microsoft.Extensions.Logging.LogLevel.Information,
                                        "Archive",
                                        $"Arşiv kayıtlı parola ('{autoMatchedPassword}') ile otomatik olarak başarıyla çıkarıldı: {Path.GetFileName(e.FilePath)}");
                                }

                                if (DeleteArchiveAfterExtract && File.Exists(e.FilePath))
                                {
                                    try
                                    {
                                        File.Delete(e.FilePath);
                                        FDownloader.Core.Logging.AppFileLoggerProvider.AppendLog(
                                            Microsoft.Extensions.Logging.LogLevel.Information,
                                            "Archive",
                                            $"Arşiv başarıyla çıkarıldıktan sonra orijinal dosya silindi: {Path.GetFileName(e.FilePath)}");
                                    }
                                    catch (Exception delEx)
                                    {
                                        FDownloader.Core.Logging.AppFileLoggerProvider.AppendLog(
                                            Microsoft.Extensions.Logging.LogLevel.Warning,
                                            "Archive",
                                            $"Arşiv çıkarıldı fakat orijinal dosya silinemedi: {Path.GetFileName(e.FilePath)}",
                                            delEx);
                                    }
                                }



                            }



                            else if (result.IsPasswordProtected)
                            {
                                ShowNotification("🔒 Şifreli Arşiv", "Arşivi klasöre çıkartabilmek için parola gerekiyor.", e.FilePath, "🔒", CompletionSound.Sessiz, true);
                                PromptArchivePassword(e.FilePath);
                            }
                            else
                            {
                                ShowNotification("⚠️ Arşiv Hatası", result.ErrorMessage ?? "Arşiv açılırken bir hata oluştu.", e.FilePath, "⚠️", CompletionSound.Sessiz, true);
                            }
                        });



                    });



                }



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



                    item.PushSpeedBar(0);



                    item.EtaDisplay = "Duraklatıldı";



                    ApplyFilter();



                }



                UpdateGlobalStats();



                UpdateHybridState();



            });



        });

        // Görev Devam Ettirildi (Torrent veya HTTP duraklatıldıktan sonra devam edince)
        _eventBus.Subscribe<TaskResumedEvent>(e =>
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



        // 6.5. Görev Yeniden Bağlanma / Deneme (Auto-retry)



        _eventBus.Subscribe<TaskRetryingEvent>(e =>



        {



            Dispatcher.UIThread.Post(() =>



            {



                var item = Downloads.FirstOrDefault(d => d.Id == e.TaskId);



                if (item != null)



                {



                    item.Status = DownloadStatus.Retrying;



                    item.IsStalled = false;



                    item.EtaDisplay = $"Tekrar deneniyor ({e.AttemptNumber}. deneme, {(int)e.Delay.TotalSeconds}sn)";



                    ApplyFilter();



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



                if (!IsSiteGrabberDialogOpen && !IsSettingsDialogOpen)



                {



                    RequestBringToFront?.Invoke();



                    if (!IsAddDialogOpen || !string.Equals(AddDialogUrl, e.Url, StringComparison.OrdinalIgnoreCase))



                    {



                        OpenAddDialogWithUrl(e.Url, e.UrlType);



                    }



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



        // 8.1. Tarayıcı eklentisi canlılık ve sürüm bildirimi (Heartbeat)



        _eventBus.Subscribe<BrowserExtensionHeartbeatEvent>(e =>



        {



            Dispatcher.UIThread.Post(() =>



            {



                OnExtensionHeartbeat(e);



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



    private void ShowNotification(string title, string message, string? filePath, string icon = "✓", CompletionSound sound = CompletionSound.Notify, bool isArchive = false)



    {



        try



        {



            var toast = new ToastNotificationWindow(title, message, filePath, icon, sound, isArchive);



            toast.Show();



        }



        catch { }



    }



    private void ShowCompletionNotification(string fileName, string? filePath)



    {



        ShowNotification("İndirme Tamamlandı!", fileName, filePath, "✓", _currentCompletionSound, false);



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



    private static bool IsItemInCategory(DownloadItemViewModel d, int categoryIndex) => categoryIndex switch

    {

        1 => d.IsActive,

        2 => d.IsCompleted,

        3 => d.Status == DownloadStatus.Paused,

        4 => d.IsTorrent,

        5 => d.Category == DownloadCategory.Video,

        6 => d.Category == DownloadCategory.Music,

        7 => d.Category == DownloadCategory.Archive,

        8 => d.Category == DownloadCategory.Document,

        _ => true

    };



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



        // Eğer seçili kategoride (örn. Torrent veya İndiriliyor) hiç öğe kalmadıysa ama listede başka dosyalar varsa 'Tümü'ne dön

        if (SelectedCategoryIndex > 0 && Downloads.Count > 0 && !Downloads.Any(d => IsItemInCategory(d, SelectedCategoryIndex)))

        {

            _selectedCategoryIndex = 0;

            OnPropertyChanged(nameof(SelectedCategoryIndex));

            items = Downloads.AsEnumerable();

            if (!string.IsNullOrEmpty(query))

            {

                items = items.Where(d => d.FileName.ToLowerInvariant().Contains(query) ||

                                         d.OriginalUrl.ToLowerInvariant().Contains(query));

            }

        }



        var targetList = items.ToList();

        FilteredDownloads.Clear();

        foreach (var itm in targetList)

        {

            FilteredDownloads.Add(itm);

        }



        // Sayaçları güncelle



        TotalCount = Downloads.Count;



        CompletedCount = Downloads.Count(d => d.IsCompleted);



        ActiveCount = Downloads.Count(d => d.IsActive);



        PausedCount = Downloads.Count(d => d.Status == DownloadStatus.Paused);



        FailedCount = Downloads.Count(d => d.IsFailed);



        UpdateStatusCountTexts();



    }



    private void UpdateStatusCountTexts()

    {

        TotalCountText = string.Format(Loc["Status.Total"], TotalCount);

        CompletedCountText = string.Format(Loc["Status.Completed"], CompletedCount);

        ActiveStatusCountText = string.Format(Loc["Status.Active"], ActiveCount);

        PausedCountText = string.Format(Loc["Status.Paused"], PausedCount);

        FailedCountText = string.Format(Loc["Status.Failed"], FailedCount);

    }



    public string DetailSelectedFileName => SelectedDownload != null ? SelectedDownload.FileName : Loc["Detail.NoItemSelected"];

    public string DetailSelectedSavePath => SelectedDownload != null ? SelectedDownload.SavePath : "-";

    public string DetailSelectedSourceUrl => SelectedDownload != null ? SelectedDownload.OriginalUrl : "-";

    public string DetailSelectedDownloadedSize => SelectedDownload != null ? SelectedDownload.DownloadedBytesDisplay : "0 B";

    public string DetailSelectedLastError => SelectedDownload != null ? (!string.IsNullOrWhiteSpace(SelectedDownload.LastErrorMessage) ? SelectedDownload.LastErrorMessage : Loc["Detail.NoError"]) : Loc["Detail.NoError"];

    public string DetailLastErrorForeground => (SelectedDownload != null && !string.IsNullOrWhiteSpace(SelectedDownload.LastErrorMessage)) ? "#EF4444" : "#10B981";



    [RelayCommand]

    public void OpenLogFile() => FDownloader.Core.Logging.AppFileLoggerProvider.OpenLogFile();



    [RelayCommand]

    public void OpenLogFolder() => FDownloader.Core.Logging.AppFileLoggerProvider.OpenLogFolder();



    [RelayCommand]

    public void CopyRecentLogs()

    {

        var logs = string.Join(Environment.NewLine, FDownloader.Core.Logging.AppFileLoggerProvider.RecentLogs);

        if (string.IsNullOrWhiteSpace(logs)) logs = "Kayıtlı log bulunamadı / No logs found.";

        ClipboardWatcher.SetWindowsClipboardText(logs);

    }



    public string RecentLogsText

    {

        get

        {

            var logs = FDownloader.Core.Logging.AppFileLoggerProvider.RecentLogs;

            if (logs.Count == 0) return "Henüz sistem hatası veya log kaydı yok / No log records yet.";

            return string.Join(Environment.NewLine, logs.TakeLast(25));

        }

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



            OnPropertyChanged(nameof(DetailSelectedFileName));

            OnPropertyChanged(nameof(DetailSelectedSavePath));

            OnPropertyChanged(nameof(DetailSelectedLastError));

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



    public async Task SelectAllTorrentFiles()



    {



        if (SelectedDownload == null || !SelectedDownload.IsTorrent) return;



        await _downloadManager.SetAllTorrentFilesPriorityAsync(SelectedDownload.Id, true);



        CurrentTorrentDetails = _downloadManager.GetTorrentDetails(SelectedDownload.Id);



    }



    [RelayCommand]



    public async Task DeselectAllTorrentFiles()



    {



        if (SelectedDownload == null || !SelectedDownload.IsTorrent) return;



        await _downloadManager.SetAllTorrentFilesPriorityAsync(SelectedDownload.Id, false);



        CurrentTorrentDetails = _downloadManager.GetTorrentDetails(SelectedDownload.Id);



    }



    partial void OnCurrentTorrentDetailsChanged(TorrentDetails? value)



    {



        UpdateTorrentDetailsSelectionState();



    }



    public void UpdateTorrentDetailsSelectionState()



    {



        if (CurrentTorrentDetails?.Files == null || CurrentTorrentDetails.Files.Count == 0)



        {



            TorrentDetailsSelectAllText = "✅ Tümünü İndir";



            TorrentDetailsDeselectAllText = "⬜ Tümünü Bırak";



            TorrentDetailsSelectAllBackground = "Transparent";



            TorrentDetailsSelectAllBorderBrush = "#33FFFFFF";



            TorrentDetailsDeselectAllBackground = "Transparent";



            TorrentDetailsDeselectAllBorderBrush = "#33FFFFFF";



            return;



        }



        bool allSelected = CurrentTorrentDetails.Files.All(f => f.IsSelected);



        bool noneSelected = CurrentTorrentDetails.Files.All(f => !f.IsSelected);



        if (allSelected)



        {



            TorrentDetailsSelectAllText = "✅ Tümünü İndir";



            TorrentDetailsDeselectAllText = "⬜ Tümünü Bırak";



            TorrentDetailsSelectAllBackground = "#2510B981";



            TorrentDetailsSelectAllBorderBrush = "#10B981";



            TorrentDetailsDeselectAllBackground = "Transparent";



            TorrentDetailsDeselectAllBorderBrush = "#33FFFFFF";



        }



        else if (noneSelected)



        {



            TorrentDetailsSelectAllText = "⬜ Tümünü İndir";



            TorrentDetailsDeselectAllText = "✅ Tümünü Bırak";



            TorrentDetailsSelectAllBackground = "Transparent";



            TorrentDetailsSelectAllBorderBrush = "#33FFFFFF";



            TorrentDetailsDeselectAllBackground = "#25EF4444";



            TorrentDetailsDeselectAllBorderBrush = "#EF4444";



        }



        else



        {



            TorrentDetailsSelectAllText = "⬜ Tümünü İndir";



            TorrentDetailsDeselectAllText = "⬜ Tümünü Bırak";



            TorrentDetailsSelectAllBackground = "Transparent";



            TorrentDetailsSelectAllBorderBrush = "#33FFFFFF";



            TorrentDetailsDeselectAllBackground = "Transparent";



            TorrentDetailsDeselectAllBorderBrush = "#33FFFFFF";



        }



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
        RefreshAddDialogQualityOptions(_lastDetectedMediaInfo);
    }

    partial void OnAddDialogIsMp4Changed(bool value)
    {
        if (value && AddDialogIsMp3) AddDialogIsMp3 = false;
        else if (!value && !AddDialogIsMp3) AddDialogIsMp3 = true;
        RefreshAddDialogQualityOptions(_lastDetectedMediaInfo);
    }

    private FDownloader.Core.Video.MediaMetadata? _lastDetectedMediaInfo;

    public void RefreshAddDialogQualityOptions(FDownloader.Core.Video.MediaMetadata? info = null)
    {
        _lastDetectedMediaInfo = info ?? _lastDetectedMediaInfo;
        AddDialogQualityOptions.Clear();

        if (AddDialogIsMp4)
        {
            var availableHeights = _lastDetectedMediaInfo?.Formats?
                .Where(f => f.Height.HasValue && f.Height.Value > 0)
                .Select(f => f.Height!.Value)
                .Distinct()
                .OrderByDescending(h => h)
                .ToList() ?? new List<int>();

            var standardResolutions = new (int Height, string Label)[]
            {
                (2160, "🎬 4K Ultra HD (2160p)"),
                (1440, "🎬 2K Quad HD (1440p)"),
                (1080, "🎬 Full HD (1080p)"),
                (720, "🎬 HD (720p)"),
                (480, "🎬 SD (480p)"),
                (360, "🎬 Düşük (360p)")
            };

            AddDialogQualityOptions.Add(new FDownloader.Core.Video.MediaQualityOption
            {
                DisplayText = "🌟 En Yüksek Kalite (Otomatik)",
                FormatSpecifier = "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best[ext=mp4]/best",
                Height = availableHeights.Count > 0 ? availableHeights[0] : 1080,
                IsAudioOnly = false
            });

            foreach (var (h, label) in standardResolutions)
            {
                if (availableHeights.Count > 0 && h > availableHeights.Max())
                {
                    continue;
                }

                var fmt = _lastDetectedMediaInfo?.Formats?.FirstOrDefault(f => f.Height == h);
                var sizeText = (fmt?.FileSize.HasValue == true && fmt.FileSize.Value > 0)
                    ? $" (~{SpeedCalculator.FormatBytes(fmt.FileSize.Value)})"
                    : "";

                AddDialogQualityOptions.Add(new FDownloader.Core.Video.MediaQualityOption
                {
                    DisplayText = $"{label}{sizeText}",
                    FormatSpecifier = $"bestvideo[height<={h}][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<={h}]+bestaudio/best[height<={h}]/best",
                    Height = h,
                    FileSizeBytes = fmt?.FileSize,
                    IsAudioOnly = false
                });
            }
        }
        else
        {
            AddDialogQualityOptions.Add(new FDownloader.Core.Video.MediaQualityOption
            {
                DisplayText = "🎵 MP3 En Yüksek Kalite (320 kbps)",
                FormatSpecifier = "-x --audio-format mp3 --audio-quality 0 --embed-metadata",
                IsAudioOnly = true
            });
            AddDialogQualityOptions.Add(new FDownloader.Core.Video.MediaQualityOption
            {
                DisplayText = "🎵 MP3 Yüksek Kalite (256 kbps)",
                FormatSpecifier = "-x --audio-format mp3 --audio-quality 2 --embed-metadata",
                IsAudioOnly = true
            });
            AddDialogQualityOptions.Add(new FDownloader.Core.Video.MediaQualityOption
            {
                DisplayText = "🎵 MP3 Standart (192 kbps)",
                FormatSpecifier = "-x --audio-format mp3 --audio-quality 4 --embed-metadata",
                IsAudioOnly = true
            });
            AddDialogQualityOptions.Add(new FDownloader.Core.Video.MediaQualityOption
            {
                DisplayText = "🎵 MP3 Hızlı / Düşük Boyut (128 kbps)",
                FormatSpecifier = "-x --audio-format mp3 --audio-quality 5 --embed-metadata",
                IsAudioOnly = true
            });
            AddDialogQualityOptions.Add(new FDownloader.Core.Video.MediaQualityOption
            {
                DisplayText = "🎵 M4A / AAC (Orijinal Kayıpsız Akış)",
                FormatSpecifier = "-x --audio-format m4a --embed-metadata",
                IsAudioOnly = true
            });
        }

        AddDialogSelectedQuality = AddDialogQualityOptions.FirstOrDefault();
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



    [RelayCommand]



    public async Task RestartItemAsync(DownloadItemViewModel? item)



    {



        var target = item ?? SelectedDownload;



        if (target == null) return;



        target.DownloadedBytes = 0;



        target.ProgressPercent = 0;



        target.IsStalled = false;



        target.ConsecutiveZeroSpeedCount = 0;



        target.Status = DownloadStatus.Connecting;



        await _downloadManager.RestartTaskAsync(target.Id);



    }



    [RelayCommand]



    public async Task RestartSelectedAsync()



    {



        var selectedList = FilteredDownloads.Where(d => d.IsSelected).ToList();



        if (selectedList.Count > 0)



        {



            foreach (var item in selectedList)



            {



                await RestartItemAsync(item);



            }



        }



        else if (SelectedDownload != null)



        {



            await RestartItemAsync(SelectedDownload);



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

        else if (Downloads.Count > 0)

        {

            var candidate = Downloads.Any(d => d.IsActive && !d.IsStalled) ? (Downloads.FirstOrDefault(d => d.IsActive && !d.IsStalled) ?? Downloads[0]) : (Downloads.FirstOrDefault(d => d.Status is DownloadStatus.Queued or DownloadStatus.Paused or DownloadStatus.Failed) ?? Downloads[0]);

            SelectedDownload = candidate;

            await ToggleItemStartPauseAsync(candidate);

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



                SelectedHybridText = Loc["Toolbar.PauseSelected"];



                SelectedHybridTooltip = $"⏸ {Loc["Toolbar.PauseSelected"]} ({selectedList.Count})";



                SelectedHybridColor = "#F59E0B";



            }



            else



            {



                SelectedHybridIcon = "▶";



                SelectedHybridText = Loc["Toolbar.StartSelected"];



                SelectedHybridTooltip = $"▶ {Loc["Toolbar.StartSelected"]} ({selectedList.Count})";



                SelectedHybridColor = "#10B981";



            }



        }



        else



        {



            bool anyActive = Downloads.Any(d => d.IsActive);



            if (anyActive)



            {



                SelectedHybridIcon = "⏸";



                SelectedHybridText = Loc["Common.Pause"];



                SelectedHybridTooltip = $"⏸ {Loc["Common.Pause"]}";



                SelectedHybridColor = "#F59E0B";



            }



            else



            {



                SelectedHybridIcon = "▶";



                SelectedHybridText = Loc["Toolbar.StartShort"];



                SelectedHybridTooltip = $"▶ {Loc["Toolbar.StartShort"]}";



                SelectedHybridColor = "#10B981";



            }



        }



    }



    // ─── Silme Komutları (Delete tuşu / Shift+Delete / Buton) ───────────



    private List<DownloadItemViewModel> GetEffectiveSelectedList()



    {



        // 1. Öncelik: ListBox'ın o anki gerçek seçili öğeleri



        var selectedList = SelectedDownloads != null && SelectedDownloads.Count > 0



            ? SelectedDownloads.Where(d => Downloads.Contains(d)).DistinctBy(d => d.Id).ToList()



            : new List<DownloadItemViewModel>();



        // 2. Çoklu seçim listesi boşsa ama IsSelected bayrağı açık olanlar varsa



        if (selectedList.Count == 0)



        {



            selectedList = FilteredDownloads.Where(d => d.IsSelected && Downloads.Contains(d)).DistinctBy(d => d.Id).ToList();



        }



        // 3. Hala boşsa ve SelectedDownload tekili seçiliyse



        if (selectedList.Count == 0 && SelectedDownload != null && Downloads.Contains(SelectedDownload))



        {



            selectedList.Add(SelectedDownload);



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



        DeleteConfirmTitle = selectedList.Count == 1 ? "Listeden Kaldır" : $"⚠️ {selectedList.Count} İndirmeyi Listeden Kaldır";



        DeleteConfirmMessage = selectedList.Count == 1



            ? $"'{selectedList[0].FileName}' indirme listesinden kaldırılsın mı?\n\n(Dosya bilgisayarınızdan silinmez)"



            : $"DİKKAT: Seçilen {selectedList.Count} adet indirme listeden kaldırılacak:\n\n• {string.Join("\n• ", selectedList.Take(4).Select(x => x.FileName))}{(selectedList.Count > 4 ? $"\n... ve {selectedList.Count - 4} dosya daha" : "")}\n\nBu dosyalar listeden kaldırılsın mı? (Bilgisayarınızdan silinmez)";



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



        DeleteConfirmTitle = selectedList.Count == 1 ? "Kalıcı Olarak Sil (Diskten ve Listeden)" : $"⚠️ {selectedList.Count} Dosyayı Kalıcı Olarak Sil";



        DeleteConfirmMessage = selectedList.Count == 1



            ? $"DİKKAT: '{selectedList[0].FileName}' dosyası hem indirme listesinden hem de bilgisayarınızdan (diskten) kalıcı olarak silinsin mi?\n\nBu işlem geri alınamaz!"



            : $"DİKKAT: Seçilen {selectedList.Count} adet dosya hem indirme listesinden hem de bilgisayarınızdan (diskten) kalıcı olarak silinecek:\n\n• {string.Join("\n• ", selectedList.Take(4).Select(x => x.FileName))}{(selectedList.Count > 4 ? $"\n... ve {selectedList.Count - 4} dosya daha" : "")}\n\nBu işlem geri alınamaz!";



        IsDeleteConfirmDialogOpen = true;



    }



    [RelayCommand]



    public void RequestDeleteSelected()



    {



        var selectedList = GetEffectiveSelectedList();



        if (selectedList.Count == 0) return;



        _pendingDeleteItems = selectedList;



        DeleteMode = DeleteConfirmMode.GeneralChoice;



        DeleteConfirmTitle = selectedList.Count == 1 ? "Dosyayı Sil / Kaldır" : $"⚠️ {selectedList.Count} Dosyayı Sil / Kaldır";



        DeleteConfirmMessage = selectedList.Count == 1



            ? $"'{selectedList[0].FileName}' indirme listesinden kaldırılacak.\n\nBu dosya diskten (bilgisayarınızdan) da kalıcı olarak silinsin mi?"



            : $"DİKKAT: Seçilen {selectedList.Count} adet indirme listeden kaldırılacak:\n\n• {string.Join("\n• ", selectedList.Take(4).Select(x => x.FileName))}{(selectedList.Count > 4 ? $"\n... ve {selectedList.Count - 4} dosya daha" : "")}\n\nBu dosyalar diskten (bilgisayarınızdan) da kalıcı olarak silinsin mi?";



        IsDeleteConfirmDialogOpen = true;



    }



    [RelayCommand]



    public void RequestDeleteItem(DownloadItemViewModel? item)

    {

        var effectiveList = GetEffectiveSelectedList();



        // 1. Çoklu seçim durumu: Eğer sağ tıklanan öğe çoklu seçimin içindeyse VEYA item parametresi yoksa

        if ((item != null && effectiveList.Contains(item) && effectiveList.Count > 1) ||

            (item == null && effectiveList.Count > 1))

        {

            RequestDeleteSelected();

            return;

        }



        // 2. Tekil seçim durumu: Yalnızca tek bir öğe silinecek

        var target = item ?? SelectedDownload ?? effectiveList.FirstOrDefault();

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



            SelectedDownloads.Remove(item);



        }



        if (SelectedDownload != null && (completedList.Contains(SelectedDownload) || !Downloads.Contains(SelectedDownload)))



        {



            SelectedDownload = null;



        }



        if (SelectedDownloads.Count == 0)



        {



            SelectedDownload = null;



        }



        else if (SelectedDownloads.Count == 1)



        {



            SelectedDownload = SelectedDownloads[0];



        }



        CurrentTorrentDetails = SelectedDownload != null && SelectedDownload.IsTorrent



            ? _downloadManager.GetTorrentDetails(SelectedDownload.Id)



            : null;



        ApplyFilter();



        UpdateGlobalStats();



        RefreshSpeedGraph();



        UpdateHybridState();



    }



    public void AdjustColumnWidth(string columnName, double deltaX)



    {



        switch (columnName)



        {



            case "Name":



                ColNameWidth = Math.Max(100, ColNameWidth + deltaX);



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



                ColEtaWidth = Math.Max(60, ColEtaWidth + deltaX);



                break;



            case "Peers":



                ColPeersWidth = Math.Max(50, ColPeersWidth + deltaX);



                break;



            case "Status":



                ColStatusWidth = Math.Max(50, ColStatusWidth + deltaX);



                break;



        }



    }



    public void ResetSingleColumnWidth(string columnName)



    {



        switch (columnName)



        {



            case "Name": ColNameWidth = 260; break;



            case "Size": ColSizeWidth = 95; break;



            case "Progress": ColProgressWidth = 130; break;



            case "Speed": ColSpeedWidth = 95; break;



            case "MaxSpeed": ColMaxSpeedWidth = 90; break;



            case "Eta": ColEtaWidth = 100; break;



            case "Peers": ColPeersWidth = 150; break;



            case "Status": ColStatusWidth = 120; break;



        }



    }



    [RelayCommand]



    public void ResetColumnWidths()



    {



        ColNameWidth = 260;



        ColSizeWidth = 95;



        ColProgressWidth = 130;



        ColSpeedWidth = 95;



        ColMaxSpeedWidth = 90;



        ColEtaWidth = 100;



        ColPeersWidth = 150;



        ColStatusWidth = 120;



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

        // 1. Arayüzden anında kaldır (Kullanıcı hiçbir bekleme veya donma hissetmesin)
        foreach (var target in items)
        {
            Downloads.Remove(target);
            SelectedDownloads.Remove(target);
        }

        if (SelectedDownload != null && (items.Contains(SelectedDownload) || !Downloads.Contains(SelectedDownload)))
        {
            SelectedDownload = null;
        }

        // 2. Arka plan motor, veritabanı ve dosya temizliğini async yürüt
        _ = Task.Run(async () =>
        {
            foreach (var target in items)
            {
                try
                {
                    await _downloadManager.RemoveTaskAsync(target.Id, deleteFile: deleteFromDisk);
                }
                catch { }

                if (deleteFromDisk)
                {
                    try
                    {
                        DeleteLocalFilesForDownload(target);
                    }
                    catch { }
                }
            }
        });



        if (SelectedDownloads.Count == 0)



        {



            SelectedDownload = null;



        }



        else if (SelectedDownloads.Count == 1)



        {



            SelectedDownload = SelectedDownloads[0];



        }



        CurrentTorrentDetails = SelectedDownload != null && SelectedDownload.IsTorrent



            ? _downloadManager.GetTorrentDetails(SelectedDownload.Id)



            : null;



        ApplyFilter();



        UpdateGlobalStats();



        RefreshSpeedGraph();



        UpdateHybridState();



    }



    private static void DeleteLocalFilesForDownload(DownloadItemViewModel target)



    {



        try



        {



            var path = target.SavePath;



            // 1. Eğer SavePath doğrudan geçerli bir dosyayı gösteriyorsa güvenli sil



            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))



            {



                PathSafetyHelper.SafeDeleteFile(path);



            }



            // 2. Geçici indirme uzantıları (.fdown, .fdtmp, .part, .ytdl, .tmp)



            if (!string.IsNullOrWhiteSpace(path))



            {



                string[] tempExtensions = [".fdown", ".fdtmp", ".part", ".ytdl", ".tmp"];



                foreach (var ext in tempExtensions)



                {



                    var tempPath = path + ext;



                    PathSafetyHelper.SafeDeleteFile(tempPath);



                }



                // Klasör içi isim ve akış eşleşmeli geçici artıklar (.part, .ytdl, yt-dlp .f*.mp4 vb.)



                var dir = Path.GetDirectoryName(path);



                var fileName = Path.GetFileName(path);



                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !string.IsNullOrEmpty(fileName))



                {



                    try



                    {



                        var baseName = Path.GetFileNameWithoutExtension(fileName);



                        if (!string.IsNullOrWhiteSpace(baseName) && baseName.Length >= 3)



                        {



                            var candidateFiles = Directory.GetFiles(dir, $"{baseName}*");



                            foreach (var f in candidateFiles)



                            {



                                var fName = Path.GetFileName(f);



                                if (fName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase) ||



                                    fName.StartsWith(baseName + "-", StringComparison.OrdinalIgnoreCase) ||



                                    fName.Equals(fileName, StringComparison.OrdinalIgnoreCase))



                                {



                                    PathSafetyHelper.SafeDeleteFile(f);



                                }



                            }



                        }



                    }



                    catch { }



                }



            }



            // 3. Dosya henüz tamamlanmamışsa standart indirme klasörü altındaki akışları ve dosyaları temizle



            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);



            if (!string.IsNullOrWhiteSpace(userProfile) && !string.IsNullOrWhiteSpace(target.FileName))



            {



                var standardDirs = new[]



                {



                    Path.Combine(userProfile, "Downloads"),



                    Path.Combine(userProfile, "İndirilenler")



                };



                var baseName = Path.GetFileNameWithoutExtension(target.FileName);



                foreach (var sDir in standardDirs)



                {



                    if (!Directory.Exists(sDir)) continue;



                    var singleFile = Path.Combine(sDir, target.FileName);



                    PathSafetyHelper.SafeDeleteFile(singleFile);



                    if (!string.IsNullOrWhiteSpace(baseName) && baseName.Length >= 3)



                    {



                        try



                        {



                            var matches = Directory.GetFiles(sDir, $"{baseName}*");



                            foreach (var f in matches)



                            {



                                var fName = Path.GetFileName(f);



                                if (fName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase) ||



                                    fName.StartsWith(baseName + "-", StringComparison.OrdinalIgnoreCase) ||



                                    fName.Equals(target.FileName, StringComparison.OrdinalIgnoreCase))



                                {



                                    PathSafetyHelper.SafeDeleteFile(f);



                                }



                            }



                        }



                        catch { }



                    }



                }



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



    public async Task ExtractArchive(DownloadItemViewModel? item)



    {



        var target = item ?? SelectedDownload;



        if (target == null) return;



        var path = target.SavePath;



        if (string.IsNullOrEmpty(path) || !File.Exists(path) || !ArchiveExtractorService.IsArchive(path))



        {



            ShowNotification("📚 Arşiv Bulunamadı", "Belirtilen dosya mevcut değil veya desteklenen bir arşiv formatı değil.", path, "⚠️", CompletionSound.Sessiz, true);



            return;



        }



        try



        {



            var name = Path.GetFileName(path);



            ShowNotification("📚 Arşiv Çıkarılıyor...", $"{name} hedef klasöre açılıyor...", path, "⏳", CompletionSound.Sessiz, true);



            var result = await _downloadManager.ArchiveExtractor.ExtractAsync(
                path,
                createSubfolder: ExtractArchivesToSubfolder);

            string? autoMatchedPassword = null;
            if (!result.Success && result.IsPasswordProtected)
            {
                var savedPwds = SavedArchivePasswords.ToList();
                foreach (var pwd in savedPwds)
                {
                    var tryResult = await _downloadManager.ArchiveExtractor.ExtractAsync(
                        path,
                        createSubfolder: ExtractArchivesToSubfolder,
                        password: pwd);

                    if (tryResult.Success)
                    {
                        result = tryResult;
                        autoMatchedPassword = pwd;
                        break;
                    }
                }
            }

            if (result.Success)
            {
                var notifMsg = autoMatchedPassword != null
                    ? $"{result.ExtractedFilesCount} dosya kayıtlı parola ('{autoMatchedPassword}') ile açıldı."
                    : $"{result.ExtractedFilesCount} dosya klasöre çıkarıldı.";
                ShowNotification("📚 Arşiv Başarıyla Çıkarıldı", notifMsg, result.ExtractedDirectory, "📚", _currentCompletionSound, true);
            }



            else if (result.IsPasswordProtected)



            {



                PromptArchivePassword(path);



            }



            else



            {



                ShowNotification("⚠️ Arşiv Hatası", result.ErrorMessage ?? "Arşiv açılırken bir hata oluştu.", path, "⚠️", CompletionSound.Sessiz, true);



            }



        }



        catch (Exception ex)



        {



            ShowNotification("⚠️ Arşiv Hatası", ex.Message, path, "⚠️", CompletionSound.Sessiz, true);



        }



    }



    public void PromptArchivePassword(string filePath)



    {



        ArchivePasswordFilePath = filePath;



        ArchivePasswordFileName = Path.GetFileName(filePath);



        ArchivePasswordInput = string.Empty;



        ArchivePasswordErrorMessage = null;



        IsArchiveExtracting = false;



        IsArchivePasswordVisible = false;



        OnPropertyChanged(nameof(ArchivePasswordChar));



        OnPropertyChanged(nameof(ArchivePasswordVisibilityIcon));



        IsArchivePasswordDialogOpen = true;
        RequestBringToFront?.Invoke();



    }



    [RelayCommand]



    public void CancelArchivePasswordDialog()



    {



        IsArchivePasswordDialogOpen = false;



        ArchivePasswordFilePath = string.Empty;



        ArchivePasswordInput = string.Empty;



        ArchivePasswordErrorMessage = null;



        IsArchivePasswordVisible = false;



    }



    [RelayCommand]



    public async Task ConfirmArchivePassword()



    {



        if (string.IsNullOrEmpty(ArchivePasswordFilePath) || !File.Exists(ArchivePasswordFilePath))



        {



            IsArchivePasswordDialogOpen = false;



            return;



        }



        if (string.IsNullOrEmpty(ArchivePasswordInput))



        {



            ArchivePasswordErrorMessage = "Lütfen arşiv parolasını girin.";



            return;



        }



        IsArchiveExtracting = true;



        ArchivePasswordErrorMessage = null;



        var path = ArchivePasswordFilePath;



        var pwd = ArchivePasswordInput;



        var result = await _downloadManager.ArchiveExtractor.ExtractAsync(



            path,



            createSubfolder: ExtractArchivesToSubfolder,



            password: pwd);



        IsArchiveExtracting = false;



        if (result.Success)
        {
            if (RememberArchivePassword && !string.IsNullOrWhiteSpace(pwd))
            {
                PersistArchivePassword(pwd);
            }

            IsArchivePasswordDialogOpen = false;



            ShowNotification("📚 Arşiv Başarıyla Çıkarıldı", $"{result.ExtractedFilesCount} dosya klasöre çıkarıldı.", result.ExtractedDirectory, "📚", _currentCompletionSound, true);

            if (DeleteArchiveAfterExtract && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    FDownloader.Core.Logging.AppFileLoggerProvider.AppendLog(
                        Microsoft.Extensions.Logging.LogLevel.Information,
                        "Archive",
                        $"Arşiv başarıyla çıkarıldıktan sonra orijinal dosya silindi: {Path.GetFileName(path)}");
                }
                catch (Exception delEx)
                {
                    FDownloader.Core.Logging.AppFileLoggerProvider.AppendLog(
                        Microsoft.Extensions.Logging.LogLevel.Warning,
                        "Archive",
                        $"Arşiv çıkarıldı fakat orijinal dosya silinemedi: {Path.GetFileName(path)}",
                        delEx);
                }
            }



        }



        else if (result.IsPasswordProtected)



        {



            ArchivePasswordErrorMessage = result.ErrorMessage ?? "Girilen parola hatalı veya geçersiz. Lütfen tekrar deneyin.";



        }



        else



        {



            ArchivePasswordErrorMessage = result.ErrorMessage ?? "Arşiv açılırken bir hata oluştu.";



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



    // ─── Torrent İbre (Speedometer) ve Hız Limiti Kontrolü ─────────────



    [RelayCommand]



    public void ToggleAltRateLimit()
    {
        IsAltRateLimitActive = !IsAltRateLimitActive;
        _downloadManager.SetSetting(SettingKeyIsAltRateLimitActive, IsAltRateLimitActive.ToString());
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



                : Loc["Status.Unlimited"];



            UploadLimitDisplayText = GlobalUploadLimitKiB > 0



                ? $"[{SpeedCalculator.FormatSpeed(effectiveUploadLimit)}]"



                : Loc["Status.Unlimited"];



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



        SettingsTabIndex = SettingsTabSpeedAndNetwork;



        IsSettingsDialogOpen = true;



    }



    [RelayCommand]



    public void OpenSpeedLimitSettings()



    {



        _savedThemeMode = _currentThemeMode;



        SettingsTabIndex = SettingsTabSpeedAndNetwork;



        IsSettingsDialogOpen = true;



    }



    [RelayCommand]



    public void SaveSettings()



    {



        _downloadManager.MaxConcurrentDownloads = SettingsMaxConcurrentDownloads;

        _downloadManager.SetSetting(SettingKeyLanguage, Loc.CurrentLanguage);



        _downloadManager.SetSetting(SettingKeyProgressBarStyle, ((int)_progressBarStyleKey).ToString());



        _downloadManager.SetSetting(SettingKeyThemeMode, ((int)_currentThemeMode).ToString());



        _downloadManager.SetSetting(SettingKeyCompletionSound, ((int)_currentCompletionSound).ToString());



        _downloadManager.SetSetting(SettingKeyAutoExtractArchives, AutoExtractArchives.ToString());



        _downloadManager.SetSetting(SettingKeyExtractArchivesToSubfolder, ExtractArchivesToSubfolder.ToString());



        _downloadManager.SetSetting(SettingKeyDeleteArchiveAfterExtract, DeleteArchiveAfterExtract.ToString());



        _downloadManager.SetSetting(SettingKeyShowSuggestions, EnableSuggestions.ToString());



        _downloadManager.SetSetting(SettingKeyStartWithWindows, StartWithWindows.ToString());



        _downloadManager.SetSetting(SettingKeyStartMinimizedToTray, StartMinimizedToTray.ToString());



        _downloadManager.SetSetting(SettingKeyMinimizeToTrayOnClose, MinimizeToTrayOnClose.ToString());



        _downloadManager.SetSetting(SettingKeyShowTrayIcon, ShowTrayIcon.ToString());

        _downloadManager.SetSetting(SettingKeyPreventSleepWhileDownloading, PreventSleepWhileDownloading.ToString());

        _downloadManager.SetSetting(SettingKeyWarnOnExitWithActiveDownloads, WarnOnExitWithActiveDownloads.ToString());



        if (!string.IsNullOrWhiteSpace(SettingsDefaultSaveDirectory))

        {

            _downloadManager.SetSetting(SettingKeyDefaultSaveDirectory, SettingsDefaultSaveDirectory);

            AddDialogSavePath = SettingsDefaultSaveDirectory;

        }



        _downloadManager.SetSetting(SettingKeyGlobalDownloadLimitKiB, GlobalDownloadLimitKiB.ToString());

        _downloadManager.SetSetting(SettingKeyGlobalUploadLimitKiB, GlobalUploadLimitKiB.ToString());

        _downloadManager.SetSetting(SettingKeyAltDownloadLimitKiB, AltDownloadLimitKiB.ToString());

        _downloadManager.SetSetting(SettingKeyAltUploadLimitKiB, AltUploadLimitKiB.ToString());

        _downloadManager.SetSetting(SettingKeyIsAltRateLimitActive, IsAltRateLimitActive.ToString());



        if (OperatingSystem.IsWindows())



        {



            Services.WindowsStartupService.ConfigureStartup(StartWithWindows, StartMinimizedToTray);



        }



        IsSuggestionBannerVisible = EnableSuggestions;



        _savedThemeMode = _currentThemeMode;



        ApplyThemeMode(_currentThemeMode);



        UpdateLimitDisplays();



        UpdateDiskSpace();



        // BitTorrent ayarlarını kaydet ve motora uygula



        var tSettings = new TorrentSettings



        {



            ListeningPort = TorrentListeningPort,



            EnableUpnp = TorrentEnableUpnp,



            EnableDht = TorrentEnableDht,



            EnablePeX = TorrentEnablePex,



            EnableLsd = TorrentEnableLsd,



            EncryptionMode = TorrentEncryptionMode,



            MaxConnectionsTotal = TorrentMaxConnectionsTotal,



            MaxConnectionsPerTorrent = TorrentMaxConnectionsPerTorrent,



            EnableAutoSequentialForVideo = TorrentEnableAutoSequential,



            SeedRatioLimit = TorrentSeedRatioLimit,



            StopSeedingWhenRatioReached = TorrentStopSeedingWhenRatioReached,



            NetworkInterfaceId = SelectedNetworkInterface?.Id ?? ""



        };



        _ = _downloadManager.SaveTorrentSettingsAsync(tSettings);

        SaveSchedulerSettings();



        IsSettingsDialogOpen = false;



    }



    /// <summary>

    /// Tüm ayarları fabrika varsayılanlarına sıfırlar.

    /// </summary>

    [RelayCommand]

    public void ResetSettingsToDefaults()

    {

        _downloadManager.ClearAllSettings();



        // Varsayılan değerleri yeniden yükle

        var defaultDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        SettingsDefaultSaveDirectory = defaultDownloads;

        AddDialogSavePath = defaultDownloads;

        SettingsMaxConcurrentDownloads = 4;

        SettingsDefaultSegments = 8;

        _downloadManager.MaxConcurrentDownloads = 4;



        GlobalDownloadLimitKiB = 0;

        GlobalUploadLimitKiB = 0;

        AltDownloadLimitKiB = 0;

        AltUploadLimitKiB = 0;

        IsAltRateLimitActive = false;



        AutoExtractArchives = false;

        ExtractArchivesToSubfolder = true;

        DeleteArchiveAfterExtract = false;

        EnableSuggestions = true;

        IsSuggestionBannerVisible = true;



        StartWithWindows = false;

        StartMinimizedToTray = false;

        MinimizeToTrayOnClose = true;

        ShowTrayIcon = true;

        PreventSleepWhileDownloading = true;

        WarnOnExitWithActiveDownloads = true;



        if (OperatingSystem.IsWindows())

        {

            try { Services.WindowsStartupService.ConfigureStartup(false, false); } catch { }

        }



        SetThemeMode(AppThemeMode.OceanBlue);

        _savedThemeMode = AppThemeMode.OceanBlue;

        ApplyThemeMode(AppThemeMode.OceanBlue);



        SetCompletionSound(CompletionSound.Notify);

        SetProgressBarStyle(ProgressBarStyleKey.Klasik);



        // Dil varsayılanı: Sistem dili

        var sysLang = FDownloader.Core.Localization.LocalizationService.DetectSystemLanguage();

        SetAppLanguage(sysLang);



        // BitTorrent varsayılanları

        TorrentListeningPort = 55123;

        TorrentEnableUpnp = true;

        TorrentEnableDht = true;

        TorrentEnablePex = true;

        TorrentEnableLsd = true;

        TorrentEncryptionMode = 0;

        TorrentMaxConnectionsTotal = 200;

        TorrentMaxConnectionsPerTorrent = 50;

        TorrentEnableAutoSequential = true;

        TorrentSeedRatioLimit = 1.0;

        TorrentStopSeedingWhenRatioReached = false;



        // Proxy varsayılanı: Sistem proxy'si

        _downloadManager.SaveProxyConfig(new FDownloader.Core.Network.ProxyConfig { Mode = FDownloader.Core.Network.ProxyMode.System });



        UpdateLimitDisplays();

        UpdateDiskSpace();



        ShowNotification(

            Loc["Settings.ResetConfirmTitle"],

            "Tüm ayarlar fabrika varsayılanlarına başarıyla sıfırlandı.",

            null,

            "🔄",

            CompletionSound.Notify);

    }



    /// <summary>

    /// Kayıtlı ayarları bir JSON dosyasına dışa aktarır.

    /// </summary>

    [RelayCommand]

    public async Task ExportSettings()

    {

        if (RequestSaveSettingsFile == null) return;



        try

        {

            var filePath = await RequestSaveSettingsFile();

            if (string.IsNullOrWhiteSpace(filePath)) return;



            var settingsDict = _downloadManager.GetAllSettings();

            var json = System.Text.Json.JsonSerializer.Serialize(settingsDict, new System.Text.Json.JsonSerializerOptions

            {

                WriteIndented = true

            });



            await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);



            ShowNotification(

                Loc["Common.Settings"],

                $"{Loc["Settings.ExportSuccess"]} ({Path.GetFileName(filePath)})",

                filePath,

                "📤",

                CompletionSound.Notify);

        }

        catch (Exception ex)

        {

            ShowNotification("Hata", $"Ayarlar dışa aktarılamadı: {ex.Message}", null, "⚠️", CompletionSound.Notify);

        }

    }



    /// <summary>

    /// Önceden yedeklenmiş bir JSON ayar dosyasını içe aktarır ve uygular.

    /// </summary>

    [RelayCommand]

    public async Task ImportSettings()

    {

        if (RequestOpenSettingsFile == null) return;



        try

        {

            var filePath = await RequestOpenSettingsFile();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;



            var json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);

            var settingsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);



            if (settingsDict == null || settingsDict.Count == 0)

            {

                ShowNotification("Hata", Loc["Settings.ImportFailed"], null, "⚠️", CompletionSound.Notify);

                return;

            }



            _downloadManager.ImportSettings(settingsDict);



            // Ayarları UI değişkenlerine yeniden yükle

            if (settingsDict.TryGetValue(SettingKeyLanguage, out var lang))

            {

                SetAppLanguage(lang);

            }



            if (settingsDict.TryGetValue(SettingKeyDefaultSaveDirectory, out var dir) && Directory.Exists(dir))

            {

                SettingsDefaultSaveDirectory = dir;

                AddDialogSavePath = dir;

            }



            if (settingsDict.TryGetValue(SettingKeyThemeMode, out var themeStr) &&

                int.TryParse(themeStr, out var themeInt) && Enum.IsDefined(typeof(AppThemeMode), themeInt))

            {

                SetThemeMode((AppThemeMode)themeInt);

                _savedThemeMode = (AppThemeMode)themeInt;

                ApplyThemeMode((AppThemeMode)themeInt);

            }



            if (settingsDict.TryGetValue(SettingKeyCompletionSound, out var soundStr) &&

                int.TryParse(soundStr, out var soundInt) && Enum.IsDefined(typeof(CompletionSound), soundInt))

            {

                SetCompletionSound((CompletionSound)soundInt);

            }



            if (settingsDict.TryGetValue(SettingKeyProgressBarStyle, out var styleStr) &&

                int.TryParse(styleStr, out var styleInt) && Enum.IsDefined(typeof(ProgressBarStyleKey), styleInt))

            {

                SetProgressBarStyle((ProgressBarStyleKey)styleInt);

            }



            if (settingsDict.TryGetValue(SettingKeyAutoExtractArchives, out var autoExt) && bool.TryParse(autoExt, out var bAutoExt))

                AutoExtractArchives = bAutoExt;



            if (settingsDict.TryGetValue(SettingKeyExtractArchivesToSubfolder, out var subf) && bool.TryParse(subf, out var bSubf))

                ExtractArchivesToSubfolder = bSubf;



            if (settingsDict.TryGetValue(SettingKeyDeleteArchiveAfterExtract, out var delArch) && bool.TryParse(delArch, out var bDelArch))

                DeleteArchiveAfterExtract = bDelArch;



            if (settingsDict.TryGetValue(SettingKeyShowSuggestions, out var showSug) && bool.TryParse(showSug, out var bShowSug))

            {

                EnableSuggestions = bShowSug;

                IsSuggestionBannerVisible = bShowSug;

            }



            if (settingsDict.TryGetValue(SettingKeyGlobalDownloadLimitKiB, out var gdl) && int.TryParse(gdl, out var igdl))

                GlobalDownloadLimitKiB = igdl;



            if (settingsDict.TryGetValue(SettingKeyGlobalUploadLimitKiB, out var gul) && int.TryParse(gul, out var igul))

                GlobalUploadLimitKiB = igul;



            if (settingsDict.TryGetValue(SettingKeyAltDownloadLimitKiB, out var adl) && int.TryParse(adl, out var iadl))

                AltDownloadLimitKiB = iadl;



            if (settingsDict.TryGetValue(SettingKeyAltUploadLimitKiB, out var aul) && int.TryParse(aul, out var iaul))

                AltUploadLimitKiB = iaul;



            if (settingsDict.TryGetValue(SettingKeyIsAltRateLimitActive, out var arl) && bool.TryParse(arl, out var bArl))

                IsAltRateLimitActive = bArl;



            if (settingsDict.TryGetValue(SettingKeyPreventSleepWhileDownloading, out var pswd) && bool.TryParse(pswd, out var bPswd))

                PreventSleepWhileDownloading = bPswd;



            if (settingsDict.TryGetValue(SettingKeyWarnOnExitWithActiveDownloads, out var woe) && bool.TryParse(woe, out var bWoe))

                WarnOnExitWithActiveDownloads = bWoe;



            UpdateLimitDisplays();

            UpdateDiskSpace();



            ShowNotification(

                Loc["Common.Settings"],

                $"{Loc["Settings.ImportSuccess"]} ({Path.GetFileName(filePath)})",

                filePath,

                "📥",

                CompletionSound.Notify);

        }

        catch (Exception ex)

        {

            ShowNotification("Hata", $"{Loc["Settings.ImportFailed"]}: {ex.Message}", null, "⚠️", CompletionSound.Notify);

        }

    }



    [RelayCommand]



    public void CancelSettings()



    {



        if (_currentThemeMode != _savedThemeMode)



        {



            SetThemeMode(_savedThemeMode);



        }



        if (OperatingSystem.IsWindows())



            StartWithWindows = Services.WindowsStartupService.IsStartupEnabled();



        else if (bool.TryParse(_downloadManager.GetSetting(SettingKeyStartWithWindows), out var sww))



            StartWithWindows = sww;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyStartMinimizedToTray), out var smt))



            StartMinimizedToTray = smt;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyMinimizeToTrayOnClose), out var mtc))



            MinimizeToTrayOnClose = mtc;



        else



            MinimizeToTrayOnClose = true;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyShowTrayIcon), out var sti))



            ShowTrayIcon = sti;



        else



            ShowTrayIcon = true;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyPreventSleepWhileDownloading), out var pswd))

            PreventSleepWhileDownloading = pswd;

        else

            PreventSleepWhileDownloading = true;



        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyWarnOnExitWithActiveDownloads), out var woe))

            WarnOnExitWithActiveDownloads = woe;

        else

            WarnOnExitWithActiveDownloads = true;



        if (long.TryParse(_downloadManager.GetSetting(SettingKeyAltDownloadLimitKiB), out var cadl))
            AltDownloadLimitKiB = cadl;
        if (long.TryParse(_downloadManager.GetSetting(SettingKeyAltUploadLimitKiB), out var caul))
            AltUploadLimitKiB = caul;
        if (long.TryParse(_downloadManager.GetSetting(SettingKeyGlobalDownloadLimitKiB), out var cgdl))
            GlobalDownloadLimitKiB = cgdl;
        if (long.TryParse(_downloadManager.GetSetting(SettingKeyGlobalUploadLimitKiB), out var cgul))
            GlobalUploadLimitKiB = cgul;
        if (bool.TryParse(_downloadManager.GetSetting(SettingKeyIsAltRateLimitActive), out var carl))
            IsAltRateLimitActive = carl;
        UpdateLimitDisplays();

        IsSettingsDialogOpen = false;



    }



    /// <summary>

    /// Aktif indirmeler varken çıkış yapılmak istendiğinde onay sürecini başlatır veya doğrudan pencereyi kapatır.

    /// </summary>

    public bool TryRequestAppExit()

    {

        var activeDownloadsCount = Downloads.Count(d => d.IsActive);

        if (WarnOnExitWithActiveDownloads && activeDownloadsCount > 0)

        {

            ExitConfirmMessage = string.Format(

                System.Globalization.CultureInfo.CurrentCulture,

                Loc["ExitConfirm.Message"],

                activeDownloadsCount);

            IsExitConfirmDialogOpen = true;

            return false;

        }



        return true;

    }



    [RelayCommand]

    public async Task ConfirmExitApp()

    {

        IsExitConfirmDialogOpen = false;

        await PauseAllAsync();

        RequestCloseWindow?.Invoke();

    }



    [RelayCommand]

    public void MinimizeToTrayFromExitDialog()

    {

        IsExitConfirmDialogOpen = false;

        RequestHideToTray?.Invoke();

    }



    [RelayCommand]

    public void CancelExitDialog()

    {

        IsExitConfirmDialogOpen = false;

    }



    // ─── BitTorrent Gelişmiş Komutları ────────────────────────



    [RelayCommand]



    public async Task ToggleSequentialDownload(DownloadItemViewModel? item)



    {



        var target = item ?? SelectedDownload;



        if (target == null || !target.IsTorrent) return;



        var newState = await _downloadManager.ToggleTorrentSequentialDownloadAsync(target.Id);



        target.IsSequentialDownload = newState;
    }

    [RelayCommand]
    public async Task StopTorrentSeeding(DownloadItemViewModel? item)
    {
        var target = item ?? SelectedDownload;
        if (target == null) return;

        await _downloadManager.StopTorrentSeedingAsync(target.Id);
        target.IsSeeding = false;
        target.UploadSpeedBytesPerSecond = 0;
        target.Status = DownloadStatus.Completed;
        ShowNotification("🌊 BitTorrent", $"'{target.FileName}' için gönderme (seeding) durduruldu.", null, "⏹️", CompletionSound.Notify);
    }



    [RelayCommand]



    public async Task ForceRecheckTorrent(DownloadItemViewModel? item)



    {



        var target = item ?? SelectedDownload;



        if (target == null || !target.IsTorrent) return;



        await _downloadManager.ForceRecheckTorrentAsync(target.Id);



        target.Status = DownloadStatus.Verifying;



    }



    [RelayCommand]



    public void OpenAddTrackerDialog()



    {



        if (SelectedDownload == null || !SelectedDownload.IsTorrent) return;



        NewTrackerUrlsText = string.Empty;



        IsAddTrackerDialogOpen = true;



    }



    [RelayCommand]



    public void CancelAddTrackerDialog()



    {



        IsAddTrackerDialogOpen = false;



        NewTrackerUrlsText = string.Empty;



    }



    [RelayCommand]



    public async Task ConfirmAddTrackers()



    {



        IsAddTrackerDialogOpen = false;



        if (SelectedDownload == null || !SelectedDownload.IsTorrent || string.IsNullOrWhiteSpace(NewTrackerUrlsText))



            return;



        var urls = NewTrackerUrlsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);



        await _downloadManager.AddTrackersToTorrentAsync(SelectedDownload.Id, urls);



        NewTrackerUrlsText = string.Empty;



        CurrentTorrentDetails = _downloadManager.GetTorrentDetails(SelectedDownload.Id);



    }



    [RelayCommand]



    public async Task ForceReannounceTrackers()



    {



        if (SelectedDownload == null || !SelectedDownload.IsTorrent) return;



        await _downloadManager.ForceReannounceTorrentAsync(SelectedDownload.Id);



    }



    [RelayCommand]



    public async Task SetTorrentFilePriorityLevel(object? parameter)



    {



        if (parameter is not string paramStr || SelectedDownload == null || !SelectedDownload.IsTorrent) return;



        var parts = paramStr.Split(':');



        if (parts.Length != 2 || !int.TryParse(parts[0], out var index)) return;



        var priority = parts[1] switch



        {



            "DoNotDownload" => MonoTorrent.Priority.DoNotDownload,



            "Low" => MonoTorrent.Priority.Low,



            "High" => MonoTorrent.Priority.High,



            _ => MonoTorrent.Priority.Normal



        };



        await _downloadManager.SetTorrentFilePriorityAsync(SelectedDownload.Id, index, priority);



        CurrentTorrentDetails = _downloadManager.GetTorrentDetails(SelectedDownload.Id);



    }



    // ─── Komutlar: 🌐 Site Yakalayıcı (Site Grabber) ──────────────────────



    [RelayCommand]



    public void OpenSiteGrabberDialog()



    {



        SiteGrabberUrl = string.Empty;



        SiteGrabberStatusText = "Taranacak web sitesi adresini girin.";



        GrabbedFiles.Clear();



        _savedThemeMode = _currentThemeMode;



        SettingsTabIndex = SettingsTabSiteGrabber;



        IsSettingsDialogOpen = true;



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



            var safeName = FileNameSanitizer.ResolveConflict(saveDir, file.FileName);



            var task = await _downloadManager.AddHttpDownloadAsync(file.Url, saveDir, segmentCount: 8, fileName: safeName, startImmediately: true);



            var newItem = new DownloadItemViewModel



            {



                Id = task.Id,



                FileName = safeName,



                OriginalUrl = file.Url,



                Category = file.Category,



                Status = DownloadStatus.Connecting,



                CreatedAt = task.CreatedAt,



                SavePath = Path.Combine(saveDir, safeName)



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



    public Func<Task<string?>>? RequestBrowseFolder { get; set; }



    public Func<Task<string?>>? RequestSaveSettingsFile { get; set; }



    public Func<Task<string?>>? RequestOpenSettingsFile { get; set; }



    [RelayCommand]

    public async Task BrowseSettingsDefaultSaveDirectory()

    {

        if (RequestBrowseFolder != null)

        {

            var folder = await RequestBrowseFolder();

            if (!string.IsNullOrWhiteSpace(folder))

            {

                SettingsDefaultSaveDirectory = folder;

            }

        }

    }



    [RelayCommand]



    public async Task BrowseAddDialogSavePath()



    {



        if (RequestBrowseFolder != null)



        {



            var folder = await RequestBrowseFolder();



            if (!string.IsNullOrWhiteSpace(folder))



            {



                AddDialogSavePath = folder;



            }



        }



    }



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



    public Task HandleDroppedFileOrUrlAsync(string pathOrUrl)



    {



        if (string.IsNullOrWhiteSpace(pathOrUrl)) return Task.CompletedTask;



        var clean = pathOrUrl.Trim().Trim('\"', '\'');



        AddDialogUrl = clean;



        IsAddDialogOpen = true;



        AnalyzeAddUrl(clean);



        return Task.CompletedTask;



    }



    [RelayCommand]



    public void OpenAddDownloadDialog()



    {



        AddDialogUrl = string.Empty;



        AddDialogTitle = string.Empty;



        AddDialogThumbnailUrl = null;



        AddDialogTorrentFiles.Clear();



        AddDialogHasTorrentFiles = false;



        AddDialogIsMediaDetected = false;



        AddDialogIsTorrentDetected = false;



        AddDialogIsPlaylistDetected = false;



        AddDialogIsMp3 = true;



        AddDialogIsMp4 = false;



        AddDialogDownloadSingleOnly = true;



        AddDialogDownloadWholePlaylist = false;



        AddDialogStatusText = "URL veya Magnet linki girin";



        UpdateAddDialogDiskSpace();



        IsAddDialogOpen = true;



    }



    public void OpenAddDialogWithUrl(string url, ClipboardUrlType urlType)



    {



        AddDialogReferrer = null;



        AddDialogCookies = null;



        AddDialogUserAgent = null;



        IsAddDialogOpen = true;



        if (!string.Equals(AddDialogUrl, url, StringComparison.Ordinal))



        {



            AddDialogUrl = url;



        }



        else



        {



            AnalyzeAddUrl(url);



        }



    }



    public void OpenAddDialogForBrowserCapture(
        string url, string? fileName, string? referrer, string? cookies, string? userAgent)
    {
        AddDialogReferrer = referrer;
        AddDialogCookies = cookies;
        AddDialogUserAgent = userAgent;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            AddDialogTitle = fileName;
        }
        IsAddDialogOpen = true;
        AddDialogUrl = url;

        // AnalyzeAddUrl(url, fileName); - Handled by AddDialogUrl property change



        if (!AddDialogIsTorrentDetected && !string.IsNullOrWhiteSpace(fileName))



        {



            AddDialogStatusText = $"🌐 Tarayıcıdan yakalandı: {fileName}";



        }



    }



    public string? GetBrowserExtensionFolder()



    {



        var candidates = new List<string>



        {



            Path.Combine(AppContext.BaseDirectory, "tools", "browser-extension"),



            Path.Combine(Directory.GetCurrentDirectory(), "tools", "browser-extension"),



            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", "browser-extension"),



            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FDownloader", "browser-extension"),



            @"C:\FDownloader\tools\browser-extension"



        };



        var found = candidates.FirstOrDefault(Directory.Exists);



        if (found != null) return Path.GetFullPath(found);



        var localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FDownloader", "browser-extension");



        try



        {



            if (!Directory.Exists(localFolder)) Directory.CreateDirectory(localFolder);



            return localFolder;



        }



        catch { }



        return null;



    }



    [RelayCommand]
    public void OpenBrowserExtensionFolder()
    {
        var folder = GetBrowserExtensionFolder();
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            try
            {
                var cleanFolder = Path.GetFullPath(folder).TrimEnd('\\', '/');
                var manifestPath = Path.Combine(cleanFolder, "manifest.json");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = File.Exists(manifestPath)
                        ? $"/select,\"{manifestPath}\""
                        : $"\"{cleanFolder}\"",
                    UseShellExecute = true
                });
                return;
            }
            catch (Exception ex)
            {
                FDownloader.Core.Logging.AppFileLoggerProvider.AppendLog(
                    Microsoft.Extensions.Logging.LogLevel.Warning,
                    "BrowserExtension",
                    $"Klasör gezginde açılamadı: {ex.Message}",
                    ex);
            }
        }

        // Klasör henüz yoksa oluştur ve aç
        try
        {
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FDownloader", "browser-extension");
            Directory.CreateDirectory(fallback);
            Process.Start(new ProcessStartInfo { FileName = fallback, UseShellExecute = true });
        }
        catch { }
    }



    [RelayCommand]
    public void CopyBrowserExtensionPath()
    {
        var folder = GetBrowserExtensionFolder();
        if (!string.IsNullOrEmpty(folder))
        {
            ClipboardWatcher.SetWindowsClipboardText(folder);
            ShowNotification("📋 Pano", "Eklenti klasör yolu panoya kopyalandı! 'Paketlenmemiş Öğe Yükle' penceresine yapıştırabilirsiniz.", folder, "📋", CompletionSound.Notify);
        }
    }

    [RelayCommand]
    public void ReloadBrowserExtension()
    {
        BrowserBridgeService.RequestExtensionReload();
        ShowNotification(
            "🔄 Tarayıcı Eklentisi",
            "Eklentiye anında yenileme sinyali iletildi! Açık olan tarayıcılarda eklenti dosyaları diskten otomatik olarak yenilenir.",
            null,
            "🔄",
            CompletionSound.Notify);
    }

    [RelayCommand]
    public void OpenBrowserExtensionsPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c start chrome://extensions || start edge://extensions",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch { }
    }



    [RelayCommand]



    public void CopyChromeExtensionsUrl()



    {



        const string url = "chrome://extensions";



        ClipboardWatcher.SetWindowsClipboardText(url);



        ShowNotification("🌐 Tarayıcı Adresi", "chrome://extensions panoya kopyalandı! Tarayıcınızın adres çubuğuna yapıştırıp Enter'a basın.", null, "🌐", CompletionSound.Notify);



    }



    [RelayCommand]



    public void CopyEdgeExtensionsUrl()



    {



        const string url = "edge://extensions";



        ClipboardWatcher.SetWindowsClipboardText(url);



        ShowNotification("🌐 Tarayıcı Adresi", "edge://extensions panoya kopyalandı! Edge adres çubuğuna yapıştırıp Enter'a basın.", null, "🌐", CompletionSound.Notify);



    }



    [RelayCommand]
    public void OpenChromeExtensionsPage()
    {
        // 1. Eklenti yolunu panoya kopyala
        CopyBrowserExtensionPath();

        // 2. Eklenti klasörünü aç
        OpenBrowserExtensionFolder();

        // 3. Tarayıcı eklentiler sayfasını aç
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chrome.exe",
                Arguments = "chrome://extensions",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "msedge.exe",
                    Arguments = "edge://extensions",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // 4. Kolay sürükle-bırak rehber bildirimi
        ShowNotification(
            "🧩 Kolay Kurulum (Sürükle-Bırak)",
            "Açılan eklenti klasörünü tarayıcınızdaki Eklentiler sayfasına sürükleyip bırakmanız yeterlidir.",
            null,
            "🚀",
            CompletionSound.Notify);
    }



    [RelayCommand]



    public void OpenExtensionSetup()



    {



        _savedThemeMode = _currentThemeMode;



        SettingsTabIndex = SettingsTabBrowser; // SEKME 7: 🧩 Tarayıcı (Index 6)



        IsSettingsDialogOpen = true;



    }



    private void OnExtensionHeartbeat(BrowserExtensionHeartbeatEvent e)



    {



        _lastExtensionHeartbeat = e.Timestamp;



        ConnectedExtensionVersion = e.Version;



        UpdateExtensionStatus();



    }



    public string GetExpectedExtensionVersion()



    {



        try



        {



            var folder = GetBrowserExtensionFolder();



            if (!string.IsNullOrWhiteSpace(folder))



            {



                var manifestPath = Path.Combine(folder, "manifest.json");



                if (File.Exists(manifestPath))



                {



                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));



                    if (doc.RootElement.TryGetProperty("version", out var vProp))



                    {



                        var vStr = vProp.GetString();



                        if (!string.IsNullOrWhiteSpace(vStr)) return vStr.Trim();



                    }



                }



            }



        }



        catch { }



        return TargetExtensionVersion;



    }



    public void UpdateExtensionStatus()



    {



        var now = DateTime.UtcNow;



        var isAlive = _lastExtensionHeartbeat.HasValue &&



                      (now - _lastExtensionHeartbeat.Value).TotalSeconds <= ExtensionHeartbeatTimeoutSeconds;



        var targetVersion = GetExpectedExtensionVersion();



        ExpectedExtensionVersion = targetVersion;



        if (!isAlive)



        {



            IsExtensionConnected = false;



            IsExtensionUpdateAvailable = false;



            ExtensionStatusBadgeText = Loc["Extension.Waiting"];



            ExtensionStatusBadgeBackground = ExtensionStatusColorWaiting;



            ExtensionStatusBadgeForeground = "#CBD5E1";



            ExtensionStatusBadgeToolTip = Loc["Extension.WaitingTooltip"];



            return;



        }



        IsExtensionConnected = true;



        var version = string.IsNullOrWhiteSpace(ConnectedExtensionVersion) ? TargetExtensionVersion : ConnectedExtensionVersion.Trim();



        if (IsVersionOlder(version, targetVersion))
        {
            IsExtensionUpdateAvailable = true;
            // Kullanıcıya hiçbir modal çıkarmadan arka planda otomatik yenileme sinyali gönder
            BrowserBridgeService.RequestExtensionReload();

            if (!_hasAutoPromptedExtensionUpdate)
            {
                _hasAutoPromptedExtensionUpdate = true;
                ShowNotification(
                    "🔄 Tarayıcı Eklentisi",
                    $"Tarayıcı eklentisi en güncel sürüme (v{targetVersion}) arka planda otomatik güncellendi.",
                    null,
                    "🧩",
                    CompletionSound.Notify);
            }

            ExtensionStatusBadgeText = Loc.Get("Extension.Connected", targetVersion);
            ExtensionStatusBadgeBackground = ExtensionStatusColorActive;
            ExtensionStatusBadgeForeground = "#FFFFFF";
            ExtensionStatusBadgeToolTip = Loc.Get("Extension.ConnectedTooltip", targetVersion);
        }
        else
        {
            IsExtensionUpdateAvailable = false;
            IsExtensionUpdateRequiredModalOpen = false;



            ExtensionStatusBadgeText = Loc.Get("Extension.Connected", version);



            ExtensionStatusBadgeBackground = ExtensionStatusColorActive;



            ExtensionStatusBadgeForeground = "#FFFFFF";



            ExtensionStatusBadgeToolTip = Loc.Get("Extension.ConnectedTooltip", version);



        }



    }



    // ─── İlk Açılış Sihirbazı Komutları (P6-01) ───────────────────────────



    [RelayCommand]

    public void OpenFirstRunWizard()

    {

        FirstRunWizardStep = 1;

        IsFirstRunWizardOpen = true;

    }



    [RelayCommand]

    public void NextFirstRunWizardStep()

    {

        if (FirstRunWizardStep < 3)

        {

            FirstRunWizardStep++;

        }

        else

        {

            CompleteFirstRunWizard();

        }

    }



    [RelayCommand]

    public void PreviousFirstRunWizardStep()

    {

        if (FirstRunWizardStep > 1)

        {

            FirstRunWizardStep--;

        }

    }



    [RelayCommand]

    public void CompleteFirstRunWizard()

    {

        IsFirstRunWizardOpen = false;

        IsFirstRunWizardCompleted = true;



        // Ayarları kalıcı kaydet

        _downloadManager.SetSetting(SettingKeyFirstRunCompleted, "True");

        SaveSettings();



        ShowNotification(

            "🎉 Hoş Geldiniz!",

            "FDownloader kurulum ve başlangıç ayarlarınız tamamlandı. Keyifli indirmeler dileriz!",

            null,

            "🚀",

            CompletionSound.Notify);

    }



    [RelayCommand]

    public void SkipFirstRunWizard()

    {

        CompleteFirstRunWizard();

    }



    [RelayCommand]



    public void UpdateExtensionGuide()



    {



        OpenBrowserExtensionFolder();



        CopyBrowserExtensionPath();



        ShowNotification(



            "🧩 Eklenti Güncelleme Kılavuzu",



            "Güncel eklenti klasörü açıldı ve yolu panoya kopyalandı. Tarayıcınızda 'chrome://extensions' (veya 'edge://extensions') sayfasına gidip FDownloader kartındaki Döngü (Yenile) simgesine basmanız yeterlidir.",



            null,



            "💡"



        );



    }



    private static bool IsVersionOlder(string currentVersion, string targetVersion)



    {



        if (Version.TryParse(currentVersion, out var curr) && Version.TryParse(targetVersion, out var target))



        {



            return curr < target;



        }



        return string.Compare(currentVersion, targetVersion, StringComparison.OrdinalIgnoreCase) < 0;



    }



    [RelayCommand]



    public void DismissSuggestions()



    {



        IsSuggestionBannerVisible = false;



        EnableSuggestions = false;



        _downloadManager.SetSetting(SettingKeyShowSuggestions, "False");



        ShowNotification("💡 Bilgi", "Başlangıç önerileri kapatıldı. Dilediğiniz zaman Ayarlar menüsünden tekrar açabilirsiniz.", null, "✓", CompletionSound.Sessiz);



    }



    private void AnalyzeAddUrl(string? rawUrl, string? suggestedFileName = null)



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



        bool isTorrentMatch = url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ||

                              cleanUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ||

                              url.Contains(".torrent?", StringComparison.OrdinalIgnoreCase) ||

                              (!string.IsNullOrWhiteSpace(suggestedFileName) && suggestedFileName.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)) ||

                              (!string.IsNullOrWhiteSpace(AddDialogTitle) && AddDialogTitle.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase));

        if (isTorrentMatch)

        {



            AddDialogIsTorrentDetected = true;



            AddDialogIsMediaDetected = false;



            AddDialogIsPlaylistDetected = false;



            if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))

            {

                var dnMatch = System.Text.RegularExpressions.Regex.Match(url, @"[?&]dn=([^&]+)");

                AddDialogTitle = dnMatch.Success ? Uri.UnescapeDataString(dnMatch.Groups[1].Value.Replace('+', ' ')) : "Magnet Torrent";

            }

            else

            {

                AddDialogTitle = !string.IsNullOrWhiteSpace(suggestedFileName) ? suggestedFileName : Path.GetFileName(cleanUrl);

            }



            AddDialogStatusText = "BitTorrent indirmesi algılandı. Dosyalar inceleniyor...";



            _ = LoadAddDialogTorrentFilesAsync(url);



            return;



        }



        // 2. YouTube & Medya Kontrolü

        bool isMedia = YtDlpMediaService.IsMediaUrl(url);

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
        bool isFacebook = url.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) || url.Contains("fb.watch", StringComparison.OrdinalIgnoreCase) || url.Contains("fbcdn.net", StringComparison.OrdinalIgnoreCase);
        AddDialogStatusText = isYouTube ? "YouTube bilgisi alınıyor..." : (isFacebook ? "Facebook video bilgisi alınıyor..." : "Medya bilgisi alınıyor...");



        try



        {



            var info = await _mediaService.GetMediaInfoAsync(url);



            AddDialogIsLoading = false;



            if (info != null)



            {



                AddDialogTitle = info.Title;



                AddDialogThumbnailUrl = info.ThumbnailUrl;



                AddDialogStatusText = $"{info.Title} ({info.Duration:mm\\:ss})";
                RefreshAddDialogQualityOptions(info);



            }



            else



            {



                AddDialogTitle = "Medya İndirme";



                AddDialogStatusText = "Medya algılandı. MP4 veya MP3 formatı seçebilirsiniz.";
                RefreshAddDialogQualityOptions(null);



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



            bool isMedia = AddDialogIsMediaDetected;



            bool isTorrent = url.StartsWith("magnet:") || 

                             url.Split('?')[0].Trim('"', '\'').EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) || 

                             (!string.IsNullOrWhiteSpace(AddDialogTitle) && AddDialogTitle.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)) ||

                             AddDialogIsTorrentDetected;



            string targetFileName;



            if (isMedia)



            {



                targetFileName = !string.IsNullOrWhiteSpace(AddDialogTitle) ? AddDialogTitle : "Medya";



                // Eğer dosya adında uzantı zaten varsa önce onu temizleyip tekilleştir

                if (targetFileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))

                {

                    targetFileName = targetFileName.Substring(0, targetFileName.Length - 4);

                }

                else if (targetFileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))

                {

                    targetFileName = targetFileName.Substring(0, targetFileName.Length - 4);

                }



                if (AddDialogIsMp3)

                {

                    targetFileName += ".mp3";

                }

                else

                {

                    targetFileName += ".mp4";

                }



            }



            else if (isTorrent)



            {



                if (!string.IsNullOrWhiteSpace(AddDialogTitle) && !AddDialogTitle.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))

                {

                    targetFileName = AddDialogTitle;

                }

                else if (AddDialogTorrentFiles.Count == 1)

                {

                    targetFileName = Path.GetFileName(AddDialogTorrentFiles[0].Path);

                }

                else if (!string.IsNullOrWhiteSpace(AddDialogTitle))

                {

                    targetFileName = Path.GetFileNameWithoutExtension(AddDialogTitle);

                }

                else

                {

                    targetFileName = "BitTorrent İndirmesi";

                }

            }



            else



            {



                targetFileName = !string.IsNullOrWhiteSpace(AddDialogTitle) ? AddDialogTitle : (FileNameSanitizer.ExtractFromUrl(url) ?? "download.dat");



            }



            targetFileName = FileNameSanitizer.Sanitize(targetFileName);



            var pendingReq = new PendingDownloadRequest



            {



                Url = url,



                SaveDir = saveDir,



                FileName = targetFileName,



                IsMedia = isMedia,



                IsTorrent = isTorrent,



                IsMp3 = AddDialogIsMp3,



                IsPlaylist = AddDialogIsPlaylistDetected && AddDialogDownloadWholePlaylist,



                Referrer = AddDialogReferrer,



                Cookies = AddDialogCookies,



                UserAgent = AddDialogUserAgent,



                TorrentFiles = AddDialogTorrentFiles.Select(f => new TorrentFileSelection { Index = f.Index, IsSelected = f.IsSelected }).ToList(),
                FormatSpecifier = AddDialogSelectedQuality?.FormatSpecifier,
                StopSeedingImmediately = AddDialogStopSeedingImmediately,
                SeedRatioLimit = AddDialogSeedRatioLimit



            };



            // Eğer hedef klasörde aynı isimde dosya zaten varsa, teknik özellikleri gösterip soruyoruz



            if (CheckAndPromptFileConflict(pendingReq))



            {



                IsAddDialogOpen = false;



                return;



            }



            IsAddDialogOpen = false;



            await ExecutePendingDownloadAsync(pendingReq, overwriteExisting: false);



        }



        catch (Exception ex)



        {



            AddDialogStatusText = $"Hata: {ex.Message}";



            IsAddDialogOpen = true;



        }



    }



    private bool CheckAndPromptFileConflict(PendingDownloadRequest request)



    {



        var targetPath = Path.Combine(request.SaveDir, request.FileName);



        if (File.Exists(targetPath))



        {



            var fi = new FileInfo(targetPath);



            ConflictExistingFileName = request.FileName;



            ConflictExistingFilePath = targetPath;



            ConflictExistingFileSizeDisplay = $"{SpeedCalculator.FormatBytes(fi.Length)} ({fi.Length:N0} bayt)";



            ConflictExistingFileDateDisplay = fi.LastWriteTime.ToString("dd.MM.yyyy HH:mm:ss");



            var cat = DownloadCategoryExtensions.FromFileName(request.FileName);



            ConflictExistingFileCategoryIcon = cat.ToCategoryIcon();



            ConflictExistingFileTypeDisplay = cat.ToCategoryDisplayName();



            ConflictNewUrl = request.Url;



            ConflictNewFileSizeDisplay = !string.IsNullOrWhiteSpace(AddDialogTorrentSelectedSizeText) ? AddDialogTorrentSelectedSizeText : "Bilinmiyor";



            ConflictNewFileCategoryIcon = cat.ToCategoryIcon();



            ConflictNewFileTypeDisplay = cat.ToCategoryDisplayName();



            ConflictSaveDirectory = request.SaveDir;



            // Önerilen otomatik yeniden adlandırma (örn: "dosya (1).ext")



            ConflictNewFileName = FileNameSanitizer.ResolveConflict(request.SaveDir, request.FileName);



            _pendingDownloadRequest = request;



            IsFileConflictDialogOpen = true;



            return true;



        }



        return false;



    }



    [RelayCommand]



    public async Task ConflictRenameAsync()



    {



        if (_pendingDownloadRequest == null)



        {



            IsFileConflictDialogOpen = false;



            return;



        }



        var newName = !string.IsNullOrWhiteSpace(ConflictNewFileName)



            ? FileNameSanitizer.Sanitize(ConflictNewFileName.Trim())



            : FileNameSanitizer.ResolveConflict(_pendingDownloadRequest.SaveDir, _pendingDownloadRequest.FileName);



        if (File.Exists(Path.Combine(_pendingDownloadRequest.SaveDir, newName)))



        {



            newName = FileNameSanitizer.ResolveConflict(_pendingDownloadRequest.SaveDir, newName);



        }



        _pendingDownloadRequest.FileName = newName;



        var req = _pendingDownloadRequest;



        _pendingDownloadRequest = null;



        IsFileConflictDialogOpen = false;



        await ExecutePendingDownloadAsync(req, overwriteExisting: false);



    }



    [RelayCommand]



    public async Task ConflictOverwriteAsync()



    {



        if (_pendingDownloadRequest == null)



        {



            IsFileConflictDialogOpen = false;



            return;



        }



        var req = _pendingDownloadRequest;



        _pendingDownloadRequest = null;



        IsFileConflictDialogOpen = false;



        await ExecutePendingDownloadAsync(req, overwriteExisting: true);



    }



    [RelayCommand]



    public void ConflictCancel()



    {



        _pendingDownloadRequest = null;



        IsFileConflictDialogOpen = false;



    }



    // ─── 8. Link Refresh (İndirme Adresini Yenile) Komutları ────────────────

    [RelayCommand]

    public void OpenRefreshLinkDialog(DownloadItemViewModel? item)

    {

        var target = item ?? SelectedDownload;

        if (target == null) return;



        _targetRefreshItem = target;

        RefreshLinkOldUrl = target.OriginalUrl;

        RefreshLinkNewUrl = string.Empty;

        RefreshLinkStatusText = "Tarayıcıdan aldığınız yeni / taze indirme bağlantısını yapıştırın:";

        RefreshLinkIsLoading = false;

        IsRefreshLinkDialogOpen = true;

    }



    [RelayCommand]

    public async Task ConfirmRefreshLinkAsync()

    {

        if (_targetRefreshItem == null || string.IsNullOrWhiteSpace(RefreshLinkNewUrl))

        {

            RefreshLinkStatusText = "Lütfen geçerli bir URL girin.";

            return;

        }



        var newUrl = RefreshLinkNewUrl.Trim();

        RefreshLinkIsLoading = true;

        RefreshLinkStatusText = "Yeni bağlantı doğrulanıyor ve indirme güncelleniyor...";



        try

        {

            _targetRefreshItem.OriginalUrl = newUrl;

            await _downloadManager.UpdateTaskUrlAsync(_targetRefreshItem.Id, newUrl, resumeImmediately: true);

            _targetRefreshItem.Status = DownloadStatus.Connecting;

            _targetRefreshItem.IsStalled = false;



            IsRefreshLinkDialogOpen = false;

            ShowNotification("🔄 Link Yenilendi", $"'{_targetRefreshItem.FileName}' yeni bağlantı ile kaldığı yerden devam ediyor.", null, "🔄", CompletionSound.Notify);

        }

        catch (Exception ex)

        {

            RefreshLinkStatusText = $"Hata: {ex.Message}";

        }

        finally

        {

            RefreshLinkIsLoading = false;

        }

    }



    [RelayCommand]

    public void CancelRefreshLinkDialog()

    {

        _targetRefreshItem = null;

        IsRefreshLinkDialogOpen = false;

    }



    // ─── 9. Artık / Geçici Dosya Temizleyici Komutları ──────────────────────

    [RelayCommand]

    public void OpenCleanTempFilesDialog()

    {

        OrphanTempFiles.Clear();

        CleanTempFilesStatusText = "Artık dosyalar taranıyor...";

        IsCleanTempFilesDialogOpen = true;



        var saveDir = SettingsDefaultSaveDirectory;

        var files = _downloadManager.ScanOrphanTempFiles(saveDir);



        long totalSize = 0;

        foreach (var fi in files)

        {

            totalSize += fi.Length;

            OrphanTempFiles.Add(new TempFileInfoItem

            {

                FullPath = fi.FullName,

                FileName = fi.Name,

                FileSizeBytes = fi.Length,

                FileSizeDisplay = SpeedCalculator.FormatBytes(fi.Length),

                LastModifiedDisplay = fi.LastWriteTime.ToString("dd.MM.yyyy HH:mm")

            });

        }



        TotalOrphanSizeText = SpeedCalculator.FormatBytes(totalSize);

        CleanTempFilesStatusText = files.Count > 0

            ? $"Toplam {files.Count} adet yetim geçici dosya ({TotalOrphanSizeText}) tespit edildi."

            : "Klasörünüz temiz! Hiçbir artık geçici dosya bulunamadı.";

    }



    [RelayCommand]

    public void ConfirmCleanTempFiles()

    {

        var selected = OrphanTempFiles.Where(f => f.IsSelected).Select(f => f.FullPath).ToList();

        if (selected.Count == 0)

        {

            IsCleanTempFilesDialogOpen = false;

            return;

        }



        var freed = _downloadManager.CleanOrphanTempFiles(selected);

        IsCleanTempFilesDialogOpen = false;

        ShowNotification("🧹 Temizlik Tamamlandı", $"{SpeedCalculator.FormatBytes(freed)} disk alanı başarıyla geri kazanıldı.", null, "🧹", CompletionSound.Notify);

    }



    [RelayCommand]

    public void SelectAllOrphanTempFiles()

    {

        foreach (var item in OrphanTempFiles)

        {

            item.IsSelected = true;

        }

    }



    [RelayCommand]

    public void DeselectAllOrphanTempFiles()

    {

        foreach (var item in OrphanTempFiles)

        {

            item.IsSelected = false;

        }

    }



    [RelayCommand]

    public void CancelCleanTempFilesDialog()

    {

        IsCleanTempFilesDialogOpen = false;

    }



    // ─── 10. Dosya Bütünlüğü (Checksum) Komutları ───────────────────────────

    partial void OnChecksumExpectedHashChanged(string value)

    {

        VerifyExpectedHash();

    }



    [RelayCommand]

    public async Task StartTorrentPayloadAsync(DownloadItemViewModel? item)

    {

        var target = item ?? SelectedDownload;

        if (target == null || string.IsNullOrWhiteSpace(target.SavePath) || !File.Exists(target.SavePath)) return;



        try

        {

            var saveDir = Path.GetDirectoryName(target.SavePath) ?? SettingsDefaultSaveDirectory;

            var task = await _downloadManager.AddTorrentDownloadAsync(target.SavePath, saveDir, startImmediately: true);

            var newTorrentVm = DownloadItemViewModel.FromTask(task);

            newTorrentVm.Status = DownloadStatus.Downloading;

            Downloads.Insert(0, newTorrentVm);

            ApplyFilter();

            UpdateGlobalStats();

            ShowNotification("🌊 Torrent Başlatıldı", $"'{task.FileName}' içeriği indirilmeye başlandı.", target.SavePath, "🌊", CompletionSound.Notify);

        }

        catch (Exception ex)

        {

            ShowNotification("Hata", $"Torrent başlatılamadı: {ex.Message}", null, "⚠️", CompletionSound.Notify);

        }

    }



    [RelayCommand]

    public void CopyChecksumToClipboard(string? text)

    {

        if (string.IsNullOrWhiteSpace(text)) return;

        ClipboardWatcher.SetWindowsClipboardText(text);

        ShowNotification("Kopyalandı", "Hash panoya kopyalandı.", null, "📋", CompletionSound.Notify);

    }



    [RelayCommand]

    public async Task OpenChecksumDialogAsync(DownloadItemViewModel? item)

    {

        var target = item ?? SelectedDownload;

        if (target == null || string.IsNullOrEmpty(target.SavePath) || !File.Exists(target.SavePath))

        {

            ShowNotification("Dosya Bulunamadı", "Doğrulanacak dosya diskte bulunamadı.", null, "⚠️", CompletionSound.Notify);

            return;

        }



        ChecksumFileName = target.FileName;

        ChecksumFilePath = target.SavePath;

        ChecksumMd5 = "Hesaplanıyor...";

        ChecksumSha1 = "Hesaplanıyor...";

        ChecksumSha256 = "Hesaplanıyor...";

        ChecksumExpectedHash = string.Empty;

        ChecksumMatchStatusText = string.Empty;

        ChecksumProgressPercent = 0;

        IsCalculatingChecksum = true;

        IsChecksumDialogOpen = true;



        try

        {

            var progress = new Progress<double>(p => ChecksumProgressPercent = p);

            var hashes = await HashCalculator.ComputeHashesAsync(target.SavePath, progress);



            ChecksumMd5 = hashes.Md5;

            ChecksumSha1 = hashes.Sha1;

            ChecksumSha256 = hashes.Sha256;

        }

        catch (Exception ex)

        {

            ChecksumMd5 = $"Hata: {ex.Message}";

            ChecksumSha1 = "-";

            ChecksumSha256 = "-";

        }

        finally

        {

            IsCalculatingChecksum = false;

        }

    }



    [RelayCommand]

    public void VerifyExpectedHash()

    {

        if (string.IsNullOrWhiteSpace(ChecksumExpectedHash))

        {

            ChecksumMatchStatusText = string.Empty;

            return;

        }



        var cleanExpected = ChecksumExpectedHash.Trim().ToLowerInvariant();

        bool isMatch = cleanExpected == ChecksumMd5.ToLowerInvariant() ||

                       cleanExpected == ChecksumSha1.ToLowerInvariant() ||

                       cleanExpected == ChecksumSha256.ToLowerInvariant();



        if (isMatch)

        {

            ChecksumMatchStatusText = "✅ EŞLEŞTİ: Dosya orijinal ve bozulmamış!";

            ChecksumMatchStatusColor = "#10B981";

        }

        else

        {

            ChecksumMatchStatusText = "❌ UYUŞMADI: Hash eşleşmiyor, dosya bozuk veya farklı olabilir!";

            ChecksumMatchStatusColor = "#EF4444";

        }

    }



    [RelayCommand]

    public void CancelChecksumDialog()

    {

        IsChecksumDialogOpen = false;

    }



    // ─── 11. Gelişmiş Zamanlayıcı Komutları ──────────────────────────────────

    private void InitializeScheduler()

    {

        var enabledStr = _downloadManager.GetSetting("Scheduler_Enabled");

        if (bool.TryParse(enabledStr, out var en)) IsSchedulerEnabled = en;



        var startStr = _downloadManager.GetSetting("Scheduler_StartTime");

        if (!string.IsNullOrWhiteSpace(startStr)) SchedulerStartTime = startStr;



        var endStr = _downloadManager.GetSetting("Scheduler_EndTime");

        if (!string.IsNullOrWhiteSpace(endStr)) SchedulerEndTime = endStr;



        UpdateSchedulerSummary();



        _schedulerTimer = new System.Timers.Timer(25000); // 25 saniyede bir kontrol et

        _schedulerTimer.Elapsed += (_, _) => CheckSchedulerTick();

        _schedulerTimer.Start();

    }



    private void CheckSchedulerTick()

    {

        if (!IsSchedulerEnabled) return;



        var now = DateTime.Now;

        if (!IsDayActiveForScheduler(now.DayOfWeek)) return;



        var timeStr = now.ToString("HH:mm");



        if (timeStr == SchedulerStartTime && now.Second < 30)

        {

            Dispatcher.UIThread.Post(async () =>

            {

                if (SchedulerStartActionIndex == 0)

                {

                    await StartAllAsync();

                }

                ShowNotification("⏰ Zamanlayıcı", "İndirme zamanlayıcısı devreye girdi ve indirmeler başlatıldı.", null, "⏰", CompletionSound.Notify);

            });

        }

        else if (timeStr == SchedulerEndTime && now.Second < 30)

        {

            Dispatcher.UIThread.Post(async () =>

            {

                if (SchedulerEndActionIndex == 0)

                {

                    await PauseAllAsync();

                }

                else if (SchedulerEndActionIndex == 2)

                {

                    try { System.Diagnostics.Process.Start("shutdown", "/s /t 60"); } catch { }

                }

                ShowNotification("⏰ Zamanlayıcı", "Zamanlayıcı süresi doldu ve indirmeler duraklatıldı.", null, "⏰", CompletionSound.Notify);

            });

        }

    }



    private bool IsDayActiveForScheduler(DayOfWeek day) => day switch

    {

        DayOfWeek.Monday => SchedulerMonday,

        DayOfWeek.Tuesday => SchedulerTuesday,

        DayOfWeek.Wednesday => SchedulerWednesday,

        DayOfWeek.Thursday => SchedulerThursday,

        DayOfWeek.Friday => SchedulerFriday,

        DayOfWeek.Saturday => SchedulerSaturday,

        DayOfWeek.Sunday => SchedulerSunday,

        _ => false

    };



    private void UpdateSchedulerSummary()

    {

        if (!IsSchedulerEnabled)

        {

            SchedulerStatusSummary = "Zamanlayıcı Kapalı";

        }

        else

        {

            SchedulerStatusSummary = $"Aktif: {SchedulerStartTime} - {SchedulerEndTime}";

        }

    }



    [RelayCommand]

    public void Dispose()

    {

        _schedulerTimer?.Stop();

        _schedulerTimer?.Dispose();

        _schedulerTimer = null;

        GC.SuppressFinalize(this);

    }



    public void OpenSchedulerDialog()

    {

        IsSchedulerDialogOpen = true;

    }



    [RelayCommand]

    public void SaveSchedulerSettings()

    {

        _downloadManager.SetSetting("Scheduler_Enabled", IsSchedulerEnabled.ToString());

        _downloadManager.SetSetting("Scheduler_StartTime", SchedulerStartTime);

        _downloadManager.SetSetting("Scheduler_EndTime", SchedulerEndTime);

        UpdateSchedulerSummary();

        IsSchedulerDialogOpen = false;

        // ShowNotification sessize alındı

    }



    [RelayCommand]

    public void CancelSchedulerDialog()

    {

        IsSchedulerDialogOpen = false;

    }



    private async Task ExecutePendingDownloadAsync(PendingDownloadRequest req, bool overwriteExisting)



    {



        try



        {



            var fullPath = Path.Combine(req.SaveDir, req.FileName);



            if (overwriteExisting && File.Exists(fullPath))



            {



                try { File.Delete(fullPath); } catch { }



            }



            if (req.IsMedia)



            {



                var task = await _downloadManager.AddMediaDownloadAsync(



                    req.Url,



                    req.SaveDir,



                    isAudioOnlyMp3: req.IsMp3,



                    isPlaylist: req.IsPlaylist,



                    title: req.FileName,
                    startImmediately: true,
                    formatSpecifier: req.FormatSpecifier);



                var newItem = new DownloadItemViewModel



                {



                    Id = task.Id,



                    FileName = task.FileName,



                    OriginalUrl = task.OriginalUrl,



                    Category = task.Category,



                    Status = DownloadStatus.Downloading,



                    CreatedAt = task.CreatedAt,



                    SavePath = Path.Combine(req.SaveDir, task.FileName)



                };



                Downloads.Insert(0, newItem);



                ApplyFilter();



                UpdateGlobalStats();



                FocusNewDownload(newItem);



            }



            else if (req.IsTorrent)



            {



                var task = await _downloadManager.AddTorrentDownloadAsync(

                    req.Url, req.SaveDir, startImmediately: true,

                    cookies: req.Cookies, referrer: req.Referrer, userAgent: req.UserAgent);



                if (req.TorrentFiles != null && req.TorrentFiles.Count > 0)



                {



                    bool anySelected = req.TorrentFiles.Any(f => f.IsSelected);



                    if (anySelected)



                    {



                        for (int i = 0; i < req.TorrentFiles.Count; i++)



                        {



                            if (!req.TorrentFiles[i].IsSelected)



                            {



                                await _downloadManager.SetTorrentFilePriorityAsync(task.Id, i, false);



                            }



                        }



                    }



                }



                var newItem = new DownloadItemViewModel



                {



                    Id = task.Id,



                    FileName = task.FileName,



                    OriginalUrl = task.OriginalUrl,



                    Category = DownloadCategory.Torrent,



                    Status = DownloadStatus.Downloading,



                    CreatedAt = task.CreatedAt,



                    SavePath = req.SaveDir



                };



                Downloads.Insert(0, newItem);



                ApplyFilter();



                UpdateGlobalStats();



                FocusNewDownload(newItem);



            }



            else



            {



                var detectedCategory = DownloadCategoryExtensions.FromFileName(req.FileName);



                var task = await _downloadManager.AddHttpDownloadAsync(



                    req.Url,



                    req.SaveDir,



                    segmentCount: SettingsDefaultSegments,



                    category: detectedCategory,



                    fileName: req.FileName,



                    referer: req.Referrer,



                    cookies: req.Cookies,



                    userAgent: req.UserAgent,



                    startImmediately: true);



                var finalCategory = task.Category != DownloadCategory.Other ? task.Category : detectedCategory;



                var newItem = new DownloadItemViewModel



                {



                    Id = task.Id,



                    FileName = !string.IsNullOrWhiteSpace(task.FileName) ? task.FileName : req.FileName,



                    OriginalUrl = task.OriginalUrl,



                    Category = finalCategory,



                    Status = DownloadStatus.Connecting,



                    CreatedAt = task.CreatedAt,



                    SavePath = Path.Combine(req.SaveDir, task.FileName)



                };



                Downloads.Insert(0, newItem);



                ApplyFilter();



                UpdateGlobalStats();



                FocusNewDownload(newItem);



            }



        }



        catch (Exception ex)



        {



            ShowNotification("İndirme Hatası", $"İndirme başlatılamadı: {ex.Message}", null, "❌", CompletionSound.Notify);



        }



    }



    private async Task LoadAddDialogTorrentFilesAsync(string pathOrUrl)
    {
        int requestId = Interlocked.Increment(ref _torrentInspectCounter);
        AddDialogTorrentFiles.Clear();
        AddDialogHasTorrentFiles = false;
        AddDialogIsTorrentResolving = pathOrUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);
        if (AddDialogIsTorrentResolving)
        {
            AddDialogStatusText = "🔄 Eşlerden dosya listesi (meta verisi) taranıyor... Bekleyebilir veya doğrudan 'İndirmeyi Başlat' diyebilirsiniz.";
        }
        try
        {
            var files = await _downloadManager.InspectTorrentFilesAsync(
                pathOrUrl, AddDialogCookies, AddDialogReferrer, AddDialogUserAgent);
            if (requestId != _torrentInspectCounter)
                return;
            AddDialogTorrentFiles.Clear();
            if (files.Count > 0)
            {
                var distinctFiles = files.DistinctBy(f => (f.Index, f.Path)).ToList();
                foreach (var f in distinctFiles)
                {
                    f.PropertyChanged += (_, _) => UpdateAddDialogDiskSpace();
                    AddDialogTorrentFiles.Add(f);
                }
                AddDialogHasTorrentFiles = true;
                UpdateAddDialogDiskSpace();
                AddDialogStatusText = $"{files.Count} adet dosya listelendi. İndirmek istediklerinizi seçebilirsiniz.";
            }
            else
            {
                UpdateAddDialogDiskSpace();
                AddDialogStatusText = pathOrUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)
                    ? "Magnet linki hazır (Dosya listesi indirme başladığında eşlerden alınacaktır)."
                    : "BitTorrent indirmesi hazır.";
            }
        }
        catch (Exception ex)
        {
            if (requestId != _torrentInspectCounter) return;
            AddDialogStatusText = $"Hata: {ex.Message}";
        }
        finally
        {
            if (requestId == _torrentInspectCounter)
            {
                AddDialogIsTorrentResolving = false;
            }
        }
    }

    public void UpdateAddDialogDiskSpace()



    {



        AddDialogDiskSpaceText = DiskSpaceChecker.GetDriveInfoSummary(AddDialogSavePath);



        if (AddDialogHasTorrentFiles && AddDialogTorrentFiles.Count > 0)



        {



            long selectedBytes = AddDialogTorrentFiles.Where(f => f.IsSelected).Sum(f => f.Length);



            long totalBytes = AddDialogTorrentFiles.Sum(f => f.Length);



            AddDialogTorrentSelectedSizeText = $"Seçilen: {SpeedCalculator.FormatBytes(selectedBytes)} / Toplam: {SpeedCalculator.FormatBytes(totalBytes)}";



            long freeSpace = DiskSpaceChecker.GetAvailableSpace(AddDialogSavePath);



            if (selectedBytes > 0 && selectedBytes > freeSpace)



            {



                AddDialogHasDiskWarning = true;



                AddDialogDiskWarningText = $"⚠️ Yetersiz Disk Alanı! (Seçilen: {SpeedCalculator.FormatBytes(selectedBytes)}, Boş: {SpeedCalculator.FormatBytes(freeSpace)})";



            }



            else



            {



                AddDialogHasDiskWarning = false;



                AddDialogDiskWarningText = string.Empty;



            }



            bool allSelected = AddDialogTorrentFiles.All(f => f.IsSelected);



            bool noneSelected = AddDialogTorrentFiles.All(f => !f.IsSelected);



            if (allSelected)



            {



                AddDialogSelectAllText = "✅ Tümünü Seç";



                AddDialogDeselectAllText = "⬜ Tümünü Bırak";



                AddDialogSelectAllBackground = "#2510B981";



                AddDialogSelectAllBorderBrush = "#10B981";



                AddDialogDeselectAllBackground = "Transparent";



                AddDialogDeselectAllBorderBrush = "#33FFFFFF";



            }



            else if (noneSelected)



            {



                AddDialogSelectAllText = "⬜ Tümünü Seç";



                AddDialogDeselectAllText = "✅ Tümünü Bırak";



                AddDialogSelectAllBackground = "Transparent";



                AddDialogSelectAllBorderBrush = "#33FFFFFF";



                AddDialogDeselectAllBackground = "#25EF4444";



                AddDialogDeselectAllBorderBrush = "#EF4444";



            }



            else



            {



                AddDialogSelectAllText = "⬜ Tümünü Seç";



                AddDialogDeselectAllText = "⬜ Tümünü Bırak";



                AddDialogSelectAllBackground = "Transparent";



                AddDialogSelectAllBorderBrush = "#33FFFFFF";



                AddDialogDeselectAllBackground = "Transparent";



                AddDialogDeselectAllBorderBrush = "#33FFFFFF";



            }



        }



        else



        {



            AddDialogTorrentSelectedSizeText = string.Empty;



            AddDialogHasDiskWarning = false;



            AddDialogDiskWarningText = string.Empty;



            AddDialogSelectAllText = "✅ Tümünü Seç";



            AddDialogDeselectAllText = "⬜ Tümünü Bırak";



            AddDialogSelectAllBackground = "Transparent";



            AddDialogSelectAllBorderBrush = "#33FFFFFF";



            AddDialogDeselectAllBackground = "Transparent";



            AddDialogDeselectAllBorderBrush = "#33FFFFFF";



        }



    }



    [RelayCommand]



    public void SelectAllAddDialogTorrentFiles()



    {



        foreach (var file in AddDialogTorrentFiles)



        {



            file.IsSelected = true;



        }



        UpdateAddDialogDiskSpace();



    }



    [RelayCommand]



    public void DeselectAllAddDialogTorrentFiles()



    {



        foreach (var file in AddDialogTorrentFiles)



        {



            file.IsSelected = false;



        }



        UpdateAddDialogDiskSpace();



    }



    [RelayCommand]



    public void CancelAddDownload()
    {
        IsAddDialogOpen = false;
        AddDialogUrl = string.Empty;
        AddDialogTitle = string.Empty;
        AddDialogTorrentFiles.Clear();
        AddDialogHasTorrentFiles = false;
        AddDialogIsTorrentResolving = false;
        AddDialogHasDiskWarning = false;
        AddDialogDiskWarningText = string.Empty;
        AddDialogTorrentSelectedSizeText = string.Empty;
        _ = _downloadManager.CleanUnattachedTorrentManagersAsync();
    }



    [RelayCommand]



    public void ToggleSpeedGraph()



    {



        IsSpeedGraphVisible = !IsSpeedGraphVisible;



    }



    private void UpdateGlobalStats()



    {



        UpdateExtensionStatus();



        var active = Downloads.Where(d => d.IsActive).ToList();



        ActiveCountText = Loc.Get("Status.ActiveDownloading", active.Count);



        var totalDownSpeed = active.Sum(d => d.SpeedBytesPerSecond);



        TotalDownloadSpeedText = totalDownSpeed > 0 ? SpeedCalculator.FormatSpeed(totalDownSpeed) : "0 B/s";



        var totalUpSpeed = active.Sum(d => d.UploadSpeedBytesPerSecond);



        TotalUploadSpeedText = totalUpSpeed > 0 ? SpeedCalculator.FormatSpeed(totalUpSpeed) : "0 B/s";



                // Global Max / Min Hız Takibi (Aktif indirme yoksa sıfırla ki yeni indirme eski tepe hızına göre ezilmesin)
        if (active.Count == 0)
        {
            _globalMaxSpeedRecorded = 0;
            _globalMinSpeedRecorded = 0;
        }
        else
        {
            if (totalDownSpeed > _globalMaxSpeedRecorded)
                _globalMaxSpeedRecorded = totalDownSpeed;

            if (totalDownSpeed > 0)
            {
                if (_globalMinSpeedRecorded == 0 || totalDownSpeed < _globalMinSpeedRecorded)
                    _globalMinSpeedRecorded = totalDownSpeed;
            }
        }

        // Canlı Hız Grafiği
        double barHeight = 2.0;
        if (totalDownSpeed > 0)
        {
            double scale = Math.Max(64 * 1024.0, _globalMaxSpeedRecorded);
            double ratio = Math.Clamp(totalDownSpeed / scale, 0.0, 1.0);
            double visualRatio = Math.Sqrt(ratio);
            barHeight = Math.Max(7.0, visualRatio * 38.0);
        }

        // Tüm indirmelerin hız geçmişini 500ms'lik ritmik saat gibi güncelle
        // İndirme durduysa veya tamamlandıysa da 2px taban akışını devam ettir (böylece tepe noktaları sola kayar)
        foreach (var d in Downloads)
        {
            if (d.IsActive)
            {
                d.PushSpeedBar(d.SpeedBytesPerSecond);
            }
            else
            {
                d.PushSpeedBar(0);
            }
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



        // P6-07: Aktif indirme durumuna gore Windows uyku modunu yonet

        if (PreventSleepWhileDownloading && Downloads.Any(d => d.IsActive))

        {

            Services.PowerManagementService.PreventSleep();

        }

        else

        {

            Services.PowerManagementService.AllowSleep();

        }

    }



    public void SetHistoryBarCount(int barCount)
    {
        if (barCount < 10 || barCount == DownloadItemViewModel.MaxHistoryBars) return;
        DownloadItemViewModel.MaxHistoryBars = barCount;

        while (_globalSpeedHistory.Count < barCount)
        {
            _globalSpeedHistory.Insert(0, 2.0);
        }
        while (_globalSpeedHistory.Count > barCount)
        {
            _globalSpeedHistory.RemoveAt(0);
        }

        foreach (var d in Downloads)
        {
            while (d.SpeedHistory.Count < barCount)
            {
                d.SpeedHistory.Insert(0, 2.0);
            }
            while (d.SpeedHistory.Count > barCount)
            {
                d.SpeedHistory.RemoveAt(0);
            }
        }
    }

    public void ResetGlobalSpeedHistory()
    {
        for (int i = 0; i < _globalSpeedHistory.Count; i++)
        {
            _globalSpeedHistory[i] = 2.0;
        }
    }

    public void RefreshSpeedGraph()



    {



        // 1. Artık Downloads içinde yer almayan silinmiş öğeleri temizle



        if (SelectedDownloads != null && SelectedDownloads.Count > 0)



        {



            for (int i = SelectedDownloads.Count - 1; i >= 0; i--)



            {



                if (!Downloads.Contains(SelectedDownloads[i]))



                {



                    SelectedDownloads.RemoveAt(i);



                }



            }



        }



        if (SelectedDownload != null && !Downloads.Contains(SelectedDownload))



        {



            SelectedDownload = null;



        }



        DownloadItemViewModel? singleTarget = null;



        var selectedList = SelectedDownloads != null && SelectedDownloads.Count > 0



            ? SelectedDownloads.Where(d => Downloads.Contains(d)).ToList()



            : Downloads.Where(d => d.IsSelected && Downloads.Contains(d)).ToList();



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



        else if (SelectedDownload != null && Downloads.Contains(SelectedDownload))



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



            if (Downloads.Count == 0)



            {



                GraphTitle = string.Empty;



                GraphCurrentSpeedText = "0 B/s";



                GraphMinSpeedText = "0 B/s";



                GraphMaxSpeedText = "0 B/s";



                                GraphDetailsText = Loc["Status.EmptyList"];
                while (_globalSpeedHistory.Count < DownloadItemViewModel.MaxHistoryBars)
                {
                    _globalSpeedHistory.Insert(0, 2.0);
                }
                while (_globalSpeedHistory.Count > DownloadItemViewModel.MaxHistoryBars)
                {
                    _globalSpeedHistory.RemoveAt(0);
                }
                DisplaySpeedHistory = _globalSpeedHistory;



            }



            else



            {



                var activeList = count > 1 



                    ? selectedList.Where(d => d.IsActive).ToList() 



                    : Downloads.Where(d => d.IsActive).ToList();



                var totalDownSpeed = activeList.Sum(d => d.SpeedBytesPerSecond);



                GraphTitle = count > 1 ? $"🌐 {count} Dosya" : string.Empty;



                GraphCurrentSpeedText = totalDownSpeed > 0 ? SpeedCalculator.FormatSpeed(totalDownSpeed) : "0 B/s";



                GraphMinSpeedText = _globalMinSpeedRecorded > 0 ? SpeedCalculator.FormatSpeed(_globalMinSpeedRecorded) : "0 B/s";



                GraphMaxSpeedText = _globalMaxSpeedRecorded > 0 ? SpeedCalculator.FormatSpeed(_globalMaxSpeedRecorded) : "0 B/s";



                GraphDetailsText = Loc.Get("Status.Overview", activeList.Count, Downloads.Count(d => d.IsCompleted), Downloads.Count);



                if (DisplaySpeedHistory != _globalSpeedHistory)



                {



                    DisplaySpeedHistory = _globalSpeedHistory;



                }



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
        try
        {
            var info = await _updateService.CheckForUpdatesAsync();
            if (info != null)
            {
                var targetVer = $"v{info.TargetFullRelease.Version}";
                if (!targetVer.TrimStart('v').Equals(AppVersionText.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                {
                    _availableUpdateInfo = info;
                    IsUpdateAvailable = true;
                    UpdateVersionText = targetVer;
                    UpdateTargetVersion = targetVer;
                    UpdateStatusText = $"Yeni sürüm mevcut ({UpdateVersionText}).";
                    await OpenUpdateDialogAsync();
                    return;
                }
            }

            // Canlı GitHub Releases kontrolü (Geliştirici modunda da çalışır)
            var latest = await _updateService.FetchLatestGitHubReleaseAsync();
            if (latest != null && !string.IsNullOrWhiteSpace(latest.TagName) &&
                !latest.TagName.TrimStart('v').Equals(AppVersionText.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
            {
                IsUpdateAvailable = true;
                UpdateVersionText = latest.TagName;
                UpdateTargetVersion = latest.TagName;
                UpdateReleaseTitle = latest.Name;
                UpdateReleaseDate = latest.PublishedAt?.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "";
                UpdateChangelog = await _updateService.FetchReleaseNotesAsync(latest.TagName);
                UpdateStatusText = $"Yeni GitHub sürümü mevcut ({latest.TagName}).";
                await OpenUpdateDialogAsync();
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateCurrentVersion = AppVersionText;
                UpdateTargetVersion = AppVersionText;
                UpdateStatusText = Loc["Update.UpToDate"];
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Güncelleme denetleme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task OpenUpdateDialogAsync()
    {
        UpdateCurrentVersion = AppVersionText;
        if (string.IsNullOrWhiteSpace(UpdateTargetVersion))
        {
            UpdateTargetVersion = !string.IsNullOrWhiteSpace(UpdateVersionText) ? UpdateVersionText : AppVersionText;
        }

        if (IsCurrentVersionUpToDate)
        {
            IsUpdateAvailable = false;
            UpdateStatusText = Loc["Update.UpToDate"];
        }
        else if (IsUpdateAvailable)
        {
            UpdateStatusText = $"Yeni sürüm mevcut ({UpdateTargetVersion}).";
        }

        IsUpdateDialogOpen = true;

        UpdateReleaseTitle = $"FDownloader {UpdateTargetVersion} Güncellemesi";
        UpdateReleaseDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        IsLoadingChangelog = true;
        UpdateChangelog = "Sürüm notları GitHub üzerinden alınıyor...";
        try
        {
            UpdateChangelog = await _updateService.FetchReleaseNotesAsync(UpdateTargetVersion);
        }
        catch (Exception ex)
        {
            UpdateChangelog = "Sürüm notları alınırken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoadingChangelog = false;
        }
    }

    [RelayCommand]
    public void ConfirmExtensionUpdate()
    {
        BrowserBridgeService.RequestExtensionReload();
        IsExtensionUpdateRequiredModalOpen = false;
        ShowNotification(
            "🔄 Eklenti Güncellendi",
            "Tarayıcı eklentinize güncelleme komutu iletildi ve diskteki güncel sürüm etkinleştirildi.",
            null,
            "✔️",
            CompletionSound.Notify);
    }

    [RelayCommand]
    public void CloseUpdateDialog()
    {
        if (IsUpdating) return;
        IsUpdateDialogOpen = false;
    }

    [RelayCommand]
    public async Task PreviewUpdateDialogAsync()
    {
        UpdateCurrentVersion = AppVersionText;
        UpdateTargetVersion = AppVersionText;
        IsUpdateAvailable = false;
        UpdateStatusText = Loc["Update.UpToDate"];
        UpdateReleaseTitle = $"FDownloader {AppVersionText}";
        UpdateReleaseDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        UpdateChangelog = "### 🚀 FDownloader v2.0 Yenilikleri:\n\n" +
                          "• 🔒 Şifreli arşiv desteği (Şifre hafızası, tek tıkla silme ve otomatik çıkarma)\n" +
                          "• ⚡ Çok parçalı indirmede erken kopma ve 429 Rate Limit stabilizasyonu\n" +
                          "• 🥞 Bildirim (Toast) yığılması: Bildirimlerin dikey olarak sağ altta yığılması\n" +
                          "• 🛡️ Aynı kök domain/subdomain için güvenli çerez aktarımı (Gofile vb.)\n" +
                          "• 💤 Aktif indirmede Windows uyku modunu engelleme (PowerManagement)\n" +
                          "• 🔐 Dosya bütünlüğü doğrulama (MD5, SHA-1, SHA-256 Checksum)\n" +
                          "• 🎨 4K & Yüksek DPI ekran optimizasyonları ve tema iyileştirmeleri";
        await OpenUpdateDialogAsync();
    }

    [RelayCommand]
    public async Task ApplyUpdateAsync()
    {
        // Doğrudan güncellemek yerine onay ve sürüm notları modalını aç
        await OpenUpdateDialogAsync();
    }

    [RelayCommand]
    public async Task ConfirmApplyUpdateAsync()
    {
        if (IsUpdating) return;

        try
        {
            IsUpdating = true;
            UpdateStatusText = "Güncelleme indiriliyor...";
            UpdateDownloadProgress = 0;

            var info = _availableUpdateInfo ?? await _updateService.CheckForUpdatesAsync();

            if (info == null)
            {
                if (!_updateService.IsInstalledApp)
                {
                    for (int i = 0; i <= 100; i += 25)
                    {
                        UpdateDownloadProgress = i;
                        UpdateStatusText = $"Geliştirici modu simülasyonu: %{i}";
                        await Task.Delay(250);
                    }
                    UpdateStatusText = "Geliştirici modu: Velopack kurulumu olmadığından yeniden başlatma atlandı.";
                    await Task.Delay(1500);
                    IsUpdating = false;
                    IsUpdateDialogOpen = false;
                    return;
                }

                IsUpdating = false;
                UpdateStatusText = "Güncelleme paketi bulunamadı.";
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
            await Task.Delay(1000);
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
            // Pencerenin ve bileşenlerin sorunsuz yüklenmesi için başlangıçta 2.5 saniye bekle
            await Task.Delay(2500);

            var info = await _updateService.CheckForUpdatesAsync();
            if (info != null)
            {
                var targetVer = $"v{info.TargetFullRelease.Version}";
                if (!targetVer.TrimStart('v').Equals(AppVersionText.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                {
                    _availableUpdateInfo = info;
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        IsUpdateAvailable = true;
                        UpdateVersionText = targetVer;
                        UpdateTargetVersion = targetVer;
                        UpdateStatusText = $"Yeni sürüm mevcut ({UpdateVersionText}).";

                        // İlk kurulum sihirbazı açık değilse doğrudan açılışta kullanıcıya sor
                        if (!IsFirstRunWizardOpen)
                        {
                            await OpenUpdateDialogAsync();
                        }
                    });
                    return;
                }
            }
            else
            {
                // Canlı GitHub Releases kontrolü (Hem kurulu hem taşınabilir/geliştirici modları için)
                var latest = await _updateService.FetchLatestGitHubReleaseAsync();
                if (latest != null && !string.IsNullOrWhiteSpace(latest.TagName) &&
                    !latest.TagName.TrimStart('v').Equals(AppVersionText.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        IsUpdateAvailable = true;
                        UpdateVersionText = latest.TagName;
                        UpdateTargetVersion = latest.TagName;
                        UpdateReleaseTitle = latest.Name;
                        UpdateReleaseDate = latest.PublishedAt?.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "";
                        // Ham GitHub karşılaştırma linki yerine zengin kullanıcı sürüm notlarını al
                        UpdateChangelog = await _updateService.FetchReleaseNotesAsync(latest.TagName);
                        UpdateStatusText = $"Yeni GitHub sürümü mevcut ({latest.TagName}).";

                        if (!IsFirstRunWizardOpen)
                        {
                            await OpenUpdateDialogAsync();
                        }
                    });
                }
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



public sealed class PendingDownloadRequest



{



    public string Url { get; set; } = string.Empty;



    public string SaveDir { get; set; } = string.Empty;



    public string FileName { get; set; } = string.Empty;



    public bool IsMedia { get; set; }



    public bool IsTorrent { get; set; }



    public bool IsMp3 { get; set; }



    public bool IsPlaylist { get; set; }



    public string? Referrer { get; set; }



    public string? Cookies { get; set; }



    public string? UserAgent { get; set; }



    public List<TorrentFileSelection>? TorrentFiles { get; set; }
    public string? FormatSpecifier { get; set; }
    public bool StopSeedingImmediately { get; set; }
    public double? SeedRatioLimit { get; set; }



}



public sealed class TorrentFileSelection



{



    public int Index { get; set; }



    public bool IsSelected { get; set; }



}





public class TempFileInfoItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject

{

    public string FullPath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string FileSizeDisplay { get; set; } = string.Empty;

    public string LastModifiedDisplay { get; set; } = string.Empty;

    private bool _isSelected = true;

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

}

