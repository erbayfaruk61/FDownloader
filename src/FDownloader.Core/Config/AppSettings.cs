namespace FDownloader.Core.Config;

/// <summary>
/// Uygulamanın tüm yapılandırma ayarları. SQLite'ta saklanır.
/// </summary>
public class AppSettings
{
    // ─── İndirme Klasörleri ───────────────────────────────────────────────

    /// <summary>Varsayılan indirme klasörü.</summary>
    public string DefaultDownloadDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>Kategori bazlı otomatik klasör eşlemeleri. Boşsa tümü DefaultDownloadDirectory'ye gider.</summary>
    public Dictionary<string, string> CategoryDirectories { get; set; } = new();

    // ─── İndirme Motoru ───────────────────────────────────────────────────

    /// <summary>Varsayılan parça sayısı (4-16 arası önerilir).</summary>
    public int DefaultSegmentCount { get; set; } = 8;

    /// <summary>Maksimum parça sayısı.</summary>
    public int MaxSegmentCount { get; set; } = 16;

    /// <summary>Aynı anda aktif olabilecek maksimum indirme sayısı.</summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>Paralel indirmeye geçme eşiği (byte). Bu altındaki dosyalar tek bağlantıyla indirilir.</summary>
    public long SegmentThresholdBytes { get; set; } = 5 * 1024 * 1024; // 5 MB

    // ─── Hız Limiti ───────────────────────────────────────────────────────

    /// <summary>Genel indirme hız limiti (byte/sn). 0 = sınırsız.</summary>
    public long GlobalSpeedLimitBytesPerSecond { get; set; } = 0;

    // ─── Yeniden Deneme ───────────────────────────────────────────────────

    /// <summary>Maksimum yeniden deneme sayısı (görev başına).</summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>İlk yeniden deneme bekleme süresi (saniye). Sonraki denemeler üstel artar.</summary>
    public int RetryInitialDelaySeconds { get; set; } = 3;

    /// <summary>Maksimum yeniden deneme bekleme süresi (saniye).</summary>
    public int RetryMaxDelaySeconds { get; set; } = 120;

    // ─── Zaman Aşımları ───────────────────────────────────────────────────

    /// <summary>Bağlantı zaman aşımı (saniye).</summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>Okuma zaman aşımı (saniye).</summary>
    public int ReadTimeoutSeconds { get; set; } = 60;

    /// <summary>Veri akışı durdu (stall) zaman aşımı (saniye). Bu süre veri gelmezse bağlantı yenilenir.</summary>
    public int StallTimeoutSeconds { get; set; } = 30;

    // ─── Checkpoint ───────────────────────────────────────────────────────

    /// <summary>İlerleme checkpoint aralığı (saniye).</summary>
    public int CheckpointIntervalSeconds { get; set; } = 3;

    /// <summary>İlerleme checkpoint aralığı (MB). Her X MB'da da kaydeder.</summary>
    public int CheckpointIntervalMb { get; set; } = 10;

    // ─── Dosya Sistemi ────────────────────────────────────────────────────

    /// <summary>Geçici dosya uzantısı.</summary>
    public string TempFileExtension { get; set; } = ".fdtmp";

    /// <summary>Dosya çakışmasında davranış.</summary>
    public FileConflictBehavior FileConflictBehavior { get; set; } = FileConflictBehavior.AutoRename;

    /// <summary>Tamamlandığında dosyayı kategori klasörüne otomatik taşı.</summary>
    public bool AutoMoveToCategory { get; set; } = true;

    // ─── Proxy ────────────────────────────────────────────────────────────

    public bool UseSystemProxy { get; set; } = true;
    public string? ManualProxyUrl { get; set; }
    public string? ProxyUsername { get; set; }

    // ─── Kullanıcı Arayüzü ────────────────────────────────────────────────

    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.OceanBlue;
    public CompletionSound SoundEffect { get; set; } = CompletionSound.Notify;
    public string Language { get; set; } = "tr-TR";
    public ProgressBarStyleKey ProgressBarStyle { get; set; } = ProgressBarStyleKey.Klasik;

    /// <summary>İlerleme güncellemelerini UI'ya gönderme sıklığı (ms).</summary>
    public int UiUpdateIntervalMs { get; set; } = 400;

    // ─── Sistem ───────────────────────────────────────────────────────────

    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;

    /// <summary>İndirme bitince bilgisayarda yapılacak eylem.</summary>
    public PostDownloadAction PostDownloadAction { get; set; } = PostDownloadAction.Nothing;

    // ─── Tarayıcı Entegrasyonu ────────────────────────────────────────────

    /// <summary>Yakalanacak minimum dosya boyutu (byte).</summary>
    public long BrowserCaptureMinSizeBytes { get; set; } = 1024 * 1024; // 1 MB

    /// <summary>Yakalanacak dosya uzantıları. Boşsa kategori bazlı otomatik karar verilir.</summary>
    public HashSet<string> BrowserCaptureExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum FileConflictBehavior { Overwrite, AutoRename, Ask, Skip }
public enum AppTheme { Dark, Light, System }
public enum PostDownloadAction { Nothing, Shutdown, Sleep, Hibernate, RunCommand }

/// <summary>Uygulama renk temaları</summary>
public enum AppThemeMode
{
    OceanBlue = 0,
    CyberPurple = 1,
    PlatinumGray = 2,
    LightClean = 3
}

/// <summary>İndirme tamamlanınca çalınacak zil sesi</summary>
public enum CompletionSound
{
    Notify = 0,
    Chimes = 1,
    Ding = 2,
    Tada = 3,
    Chord = 4,
    Sessiz = 5
}

/// <summary>İndirme listesinde ilerleme sütununda gösterilecek çubuk stili.</summary>
public enum ProgressBarStyleKey
{
    Klasik = 0,
    Gradyan = 1,
    Neon = 2,
    Ince = 3
}

