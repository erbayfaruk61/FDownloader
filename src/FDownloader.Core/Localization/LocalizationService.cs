using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace FDownloader.Core.Localization;

/// <summary>
/// Uygulama genelinde çoklu dil (i18n) desteği sağlayan reaktif servis.
/// Dil değiştirildiğinde UI bağlamlarını (INotifyPropertyChanged) otomatik tetikler.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    public const string DefaultLanguage = "tr-TR";
    public const string EnglishLanguage = "en-US";

    private string _currentLanguage = DefaultLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Aktif dil kodu (Örn: "tr-TR", "en-US").
    /// </summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            var normalized = NormalizeLanguageCode(value);
            if (_currentLanguage != normalized)
            {
                _currentLanguage = normalized;
                OnPropertyChanged();
                // "Item[]" bildirimi ile indeksleyici üzerinden XAML'a bağlı tüm metinler güncellenir
                OnPropertyChanged("Item[]");
                OnPropertyChanged(nameof(IsTurkish));
                OnPropertyChanged(nameof(IsEnglish));
            }
        }
    }

    public bool IsTurkish => _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
    public bool IsEnglish => _currentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// XAML içerisinden {Binding [Key], Source={x:Static loc:LocalizationService.Instance}}
    /// şeklinde doğrudan erişim sağlayan indeksleyici.
    /// </summary>
    public string this[string key] => Get(key);

    public string Get(string key, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var dict = IsEnglish ? EnglishStrings : TurkishStrings;
        if (!dict.TryGetValue(key, out var text))
        {
            // Fallback: Türkçe sözlükten dene
            if (!TurkishStrings.TryGetValue(key, out text))
            {
                text = key; // Sözlükte yoksa anahtarı dön
            }
        }

        if (args != null && args.Length > 0)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, text, args);
            }
            catch
            {
                return text;
            }
        }

        return text;
    }

    /// <summary>
    /// İşletim sisteminin geçerli kültür koduna göre uygun dili seçer (Türkçe ise tr-TR, aksi takdirde en-US).
    /// </summary>
    public static string DetectSystemLanguage()
    {
        try
        {
            var uiCulture = CultureInfo.CurrentUICulture.Name;
            if (uiCulture.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultLanguage;
            }
        }
        catch
        {
            // Hata durumunda varsayılan Türkçe
        }

        return EnglishLanguage;
    }

    private static string NormalizeLanguageCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return DefaultLanguage;
        if (code.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) return DefaultLanguage;
        if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return EnglishLanguage;
        return DefaultLanguage;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ─── Türkçe Çeviri Sözlüğü ─────────────────────────────────────────────
    private static readonly Dictionary<string, string> TurkishStrings = new(StringComparer.Ordinal)
    {
        // Genel & Başlıklar
        ["App.Title"] = "FDownloader - Yüksek Hızlı İndirme Yöneticisi",
        ["Common.Add"] = "Ekle",
        ["Common.Cancel"] = "Vazgeç",
        ["Common.Save"] = "Kaydet",
        ["Common.Close"] = "Kapat",
        ["Common.Delete"] = "Sil",
        ["Common.Start"] = "Başlat",
        ["Common.Pause"] = "Duraklat",
        ["Common.Resume"] = "Devam Et",
        ["Common.Browse"] = "Gözat...",
        ["Common.Copy"] = "Kopyala",
        ["Common.OK"] = "Tamam",
        ["Common.Settings"] = "Ayarlar",
        ["Common.Completed"] = "Tamamlandı",
        ["Common.Downloading"] = "İndiriliyor",
        ["Common.Paused"] = "Duraklatıldı",
        ["Common.Queued"] = "Kuyrukta",
        ["Common.Failed"] = "Başarısız",

        // Toolbar & Butonlar
        ["Toolbar.NewDownload"] = "➕ Yeni İndirme",
        ["Toolbar.BatchDownload"] = "📋 Toplu İndirme",
        ["Toolbar.StartAll"] = "▶️ Tümünü Başlat",
        ["Toolbar.PauseAll"] = "⏸️ Tümünü Duraklat",
        ["Toolbar.SiteGrabber"] = "🕸️ Site Yakalayıcı",
        ["Toolbar.CleanOrphans"] = "🧹 Artık Dosyaları Temizle",
        ["Toolbar.Settings"] = "⚙️ Ayarlar",

        // Kategoriler
        ["Category.All"] = "📥 Tümü",
        ["Category.Downloading"] = "⚡ İndiriliyor",
        ["Category.Completed"] = "✔️ Tamamlandı",
        ["Category.Paused"] = "⏸ Duraklatıldı",
        ["Category.Torrent"] = "🌊 Torrent",
        ["Category.Video"] = "🎬 Videolar",
        ["Category.Music"] = "🎵 Ses & MP3",
        ["Category.Archive"] = "📦 Sıkıştırılmış",
        ["Category.Document"] = "📄 Belgeler",

        // Tablo Sütunları
        ["Grid.FileName"] = "Dosya Adı",
        ["Grid.Size"] = "Boyut",
        ["Grid.Progress"] = "İlerleme",
        ["Grid.Speed"] = "Hız",
        ["Grid.Eta"] = "Kalan Süre",
        ["Grid.Status"] = "Durum",

        // Sihirbaz (Wizard)
        ["Wizard.Title"] = "FDownloader Kurulum Sihirbazı",
        ["Wizard.Subtitle"] = "Uygulama deneyiminizi kişiselleştirmek için birkaç hızlı adım.",
        ["Wizard.Step1Title"] = "Hoş Geldiniz! 🌟",
        ["Wizard.Step1Description"] = "FDownloader; yüksek hızlı çok parçalı indirmeler, akıllı video yakalayıcı, torrent ve güvenli dosya yönetimi sunan modern indirme yöneticinizdir.",
        ["Wizard.SelectTheme"] = "Lütfen tercih ettiğiniz görsel temayı seçin:",
        ["Wizard.Step2Title"] = "Klasör & Windows Başlangıç Tercihleri",
        ["Wizard.DefaultFolder"] = "📁 Varsayılan İndirme Klasörü",
        ["Wizard.DefaultFolderDesc"] = "İndirilen tüm dosyaların ve torrent verilerinin kaydedileceği ana dizin:",
        ["Wizard.WindowsIntegration"] = "🖥️ Windows Entegrasyonu",
        ["Wizard.Autostart"] = "Windows açıldığında FDownloader'ı otomatik başlat",
        ["Wizard.AutostartDesc"] = "Bilgisayarınız her açıldığında indirmeleriniz hemen hazır olur.",
        ["Wizard.StartMinimized"] = "Açılışta sistem tepsisinde (arkaplanda) sessizce başla",
        ["Wizard.StartMinimizedDesc"] = "Masaüstünüzü kalabalıklaştırmadan görev çubuğu simgesinde bekler.",
        ["Wizard.MinimizeOnClose"] = "Kapat butonuna basıldığında simge durumuna küçült",
        ["Wizard.MinimizeOnCloseDesc"] = "Aktif indirmeler kesilmeden sistem tepsisinde devam eder.",
        ["Wizard.Step3Title"] = "Tarayıcı Yakalayıcı Eklentisi",
        ["Wizard.BrowserExtensionDesc"] = "Web tarayıcınızda tıkladığınız video ve indirmelerin otomatik olarak FDownloader'a aktarılması için eklentiyi yükleyebilirsiniz.",
        ["Wizard.Next"] = "İleri ▶",
        ["Wizard.Previous"] = "◀ Geri",
        ["Wizard.Complete"] = "Tamamla 🚀",
        ["Wizard.Skip"] = "Sihirbazı Atla",
        ["Wizard.WelcomeTitle"] = "🎉 Hoş Geldiniz!",
        ["Wizard.WelcomeMessage"] = "FDownloader kurulum ve başlangıç ayarlarınız tamamlandı. Keyifli indirmeler dileriz!",

        // Ayarlar & Dil
        ["Settings.Language"] = "Uygulama Dili (Language):",
        ["Settings.LanguageDesc"] = "Uygulama arayüzünün görüntüleneceği dili seçin (Türkçe / English).",

        // P6-06 Ayarlar Düzeni, Sıfırlama ve Yedekleme
        ["Settings.ResetDefaults"] = "🔄 Varsayılana Sıfırla",
        ["Settings.ResetDefaultsTooltip"] = "Tüm ayarları fabrika ayarlarına geri döndürür.",
        ["Settings.ResetConfirmTitle"] = "Ayarları Sıfırla",
        ["Settings.ResetConfirmMessage"] = "Tüm uygulama ve indirme ayarları varsayılan fabrika değerlerine sıfırlanacak. Devam etmek istiyor musunuz?",
        ["Settings.Export"] = "📤 Dışa Aktar",
        ["Settings.ExportTooltip"] = "Mevcut ayarları JSON dosyası olarak dışa aktarır.",
        ["Settings.Import"] = "📥 İçe Aktar",
        ["Settings.ImportTooltip"] = "Daha önce yedeklenen JSON ayar dosyasını içe aktarır.",
        ["Settings.ExportSuccess"] = "Ayarlar başarıyla dışa aktarıldı.",
        ["Settings.ImportSuccess"] = "Ayarlar başarıyla içe aktarıldı ve uygulandı.",
        ["Settings.ImportFailed"] = "Ayar dosyası okunamadı veya biçimi geçersiz."
    };

    // ─── İngilizce Çeviri Sözlüğü ──────────────────────────────────────────
    private static readonly Dictionary<string, string> EnglishStrings = new(StringComparer.Ordinal)
    {
        // General & Titles
        ["App.Title"] = "FDownloader - High Speed Download Manager",
        ["Common.Add"] = "Add",
        ["Common.Cancel"] = "Cancel",
        ["Common.Save"] = "Save",
        ["Common.Close"] = "Close",
        ["Common.Delete"] = "Delete",
        ["Common.Start"] = "Start",
        ["Common.Pause"] = "Pause",
        ["Common.Resume"] = "Resume",
        ["Common.Browse"] = "Browse...",
        ["Common.Copy"] = "Copy",
        ["Common.OK"] = "OK",
        ["Common.Settings"] = "Settings",
        ["Common.Completed"] = "Completed",
        ["Common.Downloading"] = "Downloading",
        ["Common.Paused"] = "Paused",
        ["Common.Queued"] = "Queued",
        ["Common.Failed"] = "Failed",

        // Toolbar & Buttons
        ["Toolbar.NewDownload"] = "➕ New Download",
        ["Toolbar.BatchDownload"] = "📋 Batch Download",
        ["Toolbar.StartAll"] = "▶️ Start All",
        ["Toolbar.PauseAll"] = "⏸️ Pause All",
        ["Toolbar.SiteGrabber"] = "🕸️ Site Grabber",
        ["Toolbar.CleanOrphans"] = "🧹 Clean Orphan Files",
        ["Toolbar.Settings"] = "⚙️ Settings",

        // Categories
        ["Category.All"] = "📥 All",
        ["Category.Downloading"] = "⚡ Downloading",
        ["Category.Completed"] = "✔️ Completed",
        ["Category.Paused"] = "⏸ Paused",
        ["Category.Torrent"] = "🌊 Torrent",
        ["Category.Video"] = "🎬 Videos",
        ["Category.Music"] = "🎵 Audio & MP3",
        ["Category.Archive"] = "📦 Compressed",
        ["Category.Document"] = "📄 Documents",

        // Grid Columns
        ["Grid.FileName"] = "File Name",
        ["Grid.Size"] = "Size",
        ["Grid.Progress"] = "Progress",
        ["Grid.Speed"] = "Speed",
        ["Grid.Eta"] = "Time Left",
        ["Grid.Status"] = "Status",

        // Wizard
        ["Wizard.Title"] = "FDownloader Setup Wizard",
        ["Wizard.Subtitle"] = "A few quick steps to personalize your download experience.",
        ["Wizard.Step1Title"] = "Welcome! 🌟",
        ["Wizard.Step1Description"] = "FDownloader is your modern download manager offering high-speed multi-part downloads, smart media grabber, BitTorrent, and secure file handling.",
        ["Wizard.SelectTheme"] = "Please select your preferred theme:",
        ["Wizard.Step2Title"] = "Folder & Windows Startup Preferences",
        ["Wizard.DefaultFolder"] = "📁 Default Download Folder",
        ["Wizard.DefaultFolderDesc"] = "The main directory where downloaded files and torrents will be saved:",
        ["Wizard.WindowsIntegration"] = "🖥️ Windows Integration",
        ["Wizard.Autostart"] = "Start FDownloader automatically when Windows boots",
        ["Wizard.AutostartDesc"] = "Your downloads are ready to roll each time you turn on your PC.",
        ["Wizard.StartMinimized"] = "Start minimized to system tray (background)",
        ["Wizard.StartMinimizedDesc"] = "Keeps your desktop tidy by resting quietly in the notification area.",
        ["Wizard.MinimizeOnClose"] = "Minimize to system tray when close button is clicked",
        ["Wizard.MinimizeOnCloseDesc"] = "Active downloads continue in background without interruption.",
        ["Wizard.Step3Title"] = "Browser Grabber Extension",
        ["Wizard.BrowserExtensionDesc"] = "Install the browser extension to automatically capture video streams and download links from Chrome, Edge, Brave, Opera, and Firefox.",
        ["Wizard.Next"] = "Next ▶",
        ["Wizard.Previous"] = "◀ Back",
        ["Wizard.Complete"] = "Finish 🚀",
        ["Wizard.Skip"] = "Skip Wizard",
        ["Wizard.WelcomeTitle"] = "🎉 Welcome Aboard!",
        ["Wizard.WelcomeMessage"] = "Setup complete! Enjoy ultra-fast and seamless downloads with FDownloader.",

        // Settings & Language
        ["Settings.Language"] = "Language:",
        ["Settings.LanguageDesc"] = "Choose your preferred interface language (Türkçe / English).",

        // P6-06 Settings Layout, Reset & Backup
        ["Settings.ResetDefaults"] = "🔄 Reset to Defaults",
        ["Settings.ResetDefaultsTooltip"] = "Reverts all settings to their original factory defaults.",
        ["Settings.ResetConfirmTitle"] = "Reset Settings",
        ["Settings.ResetConfirmMessage"] = "All application and download preferences will be reset to factory defaults. Do you wish to continue?",
        ["Settings.Export"] = "📤 Export Settings",
        ["Settings.ExportTooltip"] = "Exports current configuration to a JSON backup file.",
        ["Settings.Import"] = "📥 Import Settings",
        ["Settings.ImportTooltip"] = "Restores configuration from a previously exported JSON backup file.",
        ["Settings.ExportSuccess"] = "Settings successfully exported.",
        ["Settings.ImportSuccess"] = "Settings successfully restored and applied.",
        ["Settings.ImportFailed"] = "Failed to import settings file or format is invalid."
    };
}

