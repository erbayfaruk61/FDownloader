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

                OnPropertyChanged(nameof(CurrentLanguage));

                OnPropertyChanged(string.Empty);

                OnPropertyChanged(null);

                OnPropertyChanged("Item");

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

        ["Common.Connecting"] = "Bağlanıyor...",

        ["Common.Merging"] = "Birleştiriliyor",

        ["Common.Verifying"] = "Doğrulanıyor",

        ["Common.Retrying"] = "Yeniden deneniyor...",

        ["Common.Cancelled"] = "İptal edildi",

        ["Common.ConnectionLost"] = "⚠️ Bağlantı Koptu",

        ["Common.ConnectionClosed"] = "⚠️ Bağlantı Kesildi",

        ["Common.SearchingMeta"] = "🔍 Meta Verisi Aranıyor...",

        ["Common.Restart"] = "Yeniden Başlat",



        // Toolbar Hibrit

        ["Toolbar.PauseSelected"] = "Seçilenleri Duraklat",

        ["Toolbar.StartSelected"] = "Seçilenleri Başlat",

        ["Toolbar.PauseAllShort"] = "Tümünü Duraklat",

        ["Toolbar.StartShort"] = "Başlat",



        // Durum Çubuğu

        ["Status.Total"] = "Listedeki Toplam: {0}",

        ["Status.Completed"] = "Tamamlanan: {0}",

        ["Status.Active"] = "Aktif: {0}",

        ["Status.Paused"] = "Duraklatıldı: {0}",

        ["Status.Failed"] = "Hata: {0}",

        ["Status.ActiveDownloading"] = "{0} İndiriliyor",

        ["Status.HighSpeed"] = "Yüksek Hız",

        ["Status.LowSpeed"] = "Yavaş Hız",

        ["Status.Unlimited"] = "[Sınırsız]",

        ["Status.Limited"] = "[Limitli]",

        ["Status.EmptyList"] = "İndirme listesi boş",

        ["Status.Overview"] = "{0} İndiriliyor • {1} Tamamlandı • Toplam {2}",



        // Detay Sekmeleri & Alanları

        ["Detail.TabGeneral"] = "ℹ️ Genel",

        ["Detail.TabSegments"] = "🟩 Parçalar",

        ["Detail.TabTorrent"] = "🌊 Torrent (Beta)",

        ["Detail.TabFiles"] = "📁 Dosyalar",

        ["Detail.TabPeers"] = "👥 Eşler",

        ["Detail.TabTrackers"] = "📡 İzleyiciler",

        ["Detail.FileName"] = "Dosya Adı:",

        ["Detail.SavePath"] = "Kayıt Yolu:",

        ["Detail.SourceUrl"] = "Kaynak URL:",

        ["Detail.DownloadedSize"] = "İndirilen Boyut:",

        ["Detail.LastError"] = "Son Hata:",

        ["Detail.NoError"] = "Hata yok",

        ["Detail.SegmentsMap"] = "Segment / Parça Doluluk Haritası (Eşzamanlı Parçalar):",

        ["Detail.NoItemSelected"] = "Seçili öğe yok",

        // Yeni İndirme Ekle Diyaloğu

        ["Add.Title"] = "Yeni İndirme Ekle",

        ["Add.UrlLabel"] = "İndirme Bağlantısı (URL, Magnet veya Dosya):",

        ["Add.UrlPlaceholder"] = "https://... veya magnet:?xt=... veya dosya yolu",

        ["Add.BrowseTorrent"] = "📁 Dosya / Torrent Seç...",

        ["Add.BrowseTorrentTooltip"] = "Bilgisayardan .torrent dosyası seçin",

        ["Add.DefaultStatus"] = "URL veya Magnet linki girin",

        ["Add.DefaultHint"] = "Video veya ses bağlantılarında MP4 veya MP3 formatı seçebilirsiniz.",

        ["Add.SaveDirectory"] = "Kayıt Klasörü:",

        ["Add.Browse"] = "📁 Gözat",

        ["Add.Start"] = "İndirmeyi Başlat",

        ["Add.Cancel"] = "İptal",

        ["Add.MediaOptions"] = "Medya Seçenekleri:",

        ["Add.TitlePlaceholder"] = "Dosya / Video Başlığı",

        ["Add.Format"] = "İndirme Formatı:",

        ["Add.FormatMp3"] = "🎵 MP3 Olarak İndir (Yalnızca Ses)",

        ["Add.FormatMp4"] = "🎬 MP4 Olarak İndir (Video)",

        ["Add.FilesToDownload"] = "İndirilecek Dosyalar:",

        ["Add.SelectAll"] = "Tümünü Seç",

        ["Add.DeselectAll"] = "Tümünü Kaldır",

        // Uygulama Ayarları Sekmeleri

        ["Settings.Title"] = "Uygulama Ayarları",

        ["Settings.TabSpeed"] = "⚡ Hız & Ağ",

        ["Settings.TabStorage"] = "📁 Depolama",

        ["Settings.TabNotification"] = "🔔 Bildirim",

        ["Settings.TabTorrent"] = "🧲 Torrent",

        ["Settings.TabAppearance"] = "🎨 Görünüm",

        ["Settings.TabGrabber"] = "🕸️ Yakalayıcı",

        ["Settings.TabBrowser"] = "🧩 Tarayıcı",

        ["Settings.TabBehavior"] = "🖥️ Davranış",

        ["Settings.TabAbout"] = "🔄 Hakkında",

        // Ayarlar: Hız & Ağ

        ["Settings.GeneralRateLimits"] = "Genel Oran Sınırları (0 = Sınırsız ∞):",

        ["Settings.DownloadLimit"] = "İndirme Limiti (KiB/s):",

        ["Settings.UploadLimit"] = "Gönderme Limiti (KiB/s):",

        ["Settings.AltSpeedLimits"] = "Alternatif Hız Sınırları (Hız İbresi Aktifken):",

        ["Settings.AltSpeedDownload"] = "İndirme (KiB/s):",

        ["Settings.AltSpeedUpload"] = "Gönderme (KiB/s):",

        ["Settings.EnableLowSpeed"] = "⏱️ Yavaş Hız (Limitli) Modunu Etkinleştir (Limitleri Devreye Al)",

        ["Settings.SchedulerTitle"] = "Gelişmiş İndirme Zamanlayıcısı",

        ["Settings.SchedulerDesc"] = "Belirlenen saat aralığında otomatik indirme başlatma, durdurma veya kapatma.",

        ["Settings.StartTime"] = "Başlangıç Saati:",

        ["Settings.StartAction"] = "Başlangıç Aksiyonu:",

        ["Settings.EndTime"] = "Bitiş Saati:",

        ["Settings.EndAction"] = "Bitiş Aksiyonu:",

        ["Settings.StartPending"] = "▶ Bekleyenleri Başlat",

        ["Settings.FullSpeed"] = "⚡ Tam Hıza Geç (Sınırsız)",

        ["Settings.PauseAll"] = "⏸ Tümünü Duraklat",

        ["Settings.SlowSpeed"] = "🐢 Yavaş Hız Moduna Geç",

        ["Settings.PauseShutdown"] = "🛑 Duraklat ve Bilgisayarı Kapat",

        ["Settings.SchedulerDays"] = "Zamanlayıcının Çalışacağı Günler:",

        ["Settings.Monday"] = "Pazartesi",

        ["Settings.Tuesday"] = "Salı",

        ["Settings.Wednesday"] = "Çarşamba",

        ["Settings.Thursday"] = "Perşembe",

        ["Settings.Friday"] = "Cuma",

        ["Settings.Saturday"] = "Cumartesi",

        ["Settings.Sunday"] = "Pazar",

        // Ayarlar: Depolama

        ["Settings.DefaultFolderAndEngine"] = "Varsayılan Klasör ve Motor Parametreleri:",

        ["Settings.ConcurrentDownloads"] = "Eşzamanlı İndirme:",

        ["Settings.SegmentCount"] = "Segment (Parça):",

        ["Settings.BrowseButton"] = "📁 Gözat...",

        // Log & Sistem

        ["Settings.OpenLogFile"] = "📄 Log Dosyasını Aç",

        ["Settings.OpenLogFolder"] = "📁 Log Klasörünü Aç",

        ["Settings.CopyLogs"] = "📋 Logları Kopyala",

        ["Settings.RecentLogsTitle"] = "🔍 Sistem & Ağ Hata Logları:",

        // Genişletilmiş Ayarlar Sözlüğü (TR)

        ["Settings.ActionStartPending"] = "▶ Bekleyenleri Başlat",

        ["Settings.ActionFullSpeed"] = "⚡ Tam Hıza Geç (Sınırsız)",

        ["Settings.ActionPauseAll"] = "⏸ Tümünü Duraklat",

        ["Settings.ActionSlowSpeed"] = "🐢 Yavaş Hız Moduna Geç",

        ["Settings.ActionPauseShutdown"] = "🛑 Duraklat ve Bilgisayarı Kapat",

        ["Settings.SaveDirectory"] = "Kayıt Klasörü:",

        ["Settings.BrowseButtonDots"] = "📁 Gözat...",

        ["Settings.AutoExtractTitle"] = "Otomatik Arşiv Çıkarma (Auto-Extract):",

        ["Settings.AutoExtractDesc"] = "İndirilen .zip, .rar, .7z, .tar, .gz arşivlerini indirme tamamlandığında otomatik olarak klasöre açar.",

        ["Settings.AutoExtractEnabled"] = "İndirilen arşiv dosyalarını otomatik çıkart",

        ["Settings.AutoExtractSubfolder"] = "Arşiv adıyla alt klasör oluşturup içine çıkart",

        ["Settings.AutoExtractDelete"] = "Başarıyla çıkarıldıktan sonra orijinal arşiv dosyasını sil",

        ["Settings.NotifyTitle"] = "Bildirim ve Ses Uyarıları:",

        ["Settings.NotifyToastEnabled"] = "İndirme tamamlandığında masaüstü bildirim balonu göster",

        ["Settings.NotifySoundEnabled"] = "İndirme bittiğinde ses çal",

        ["Settings.TorrentTitle"] = "BitTorrent Motor Ayarları:",

        ["Settings.TorrentPort"] = "Dinleme Portu (0 = Otomatik):",

        ["Settings.TorrentMaxPeers"] = "Torrent Başına Maksimum Eş (Peer):",

        ["Settings.TorrentGlobalPeers"] = "Genel Maksimum Eş Sınırı:",

        ["Settings.TorrentEnableDht"] = "DHT Ağını Etkinleştir (Trackersız torrentler için)",

        ["Settings.TorrentEnablePex"] = "Peer Exchange (PEX) Etkinleştir",

        ["Settings.TorrentEnableUtp"] = "uTP Protokolünü Etkinleştir (Ağ tıkanıklığını önler)",

        ["Settings.AppearanceTitle"] = "Tema ve Renk Paleti:",

        ["Settings.ThemeDark"] = "🌙 Koyu Tema (Varsayılan)",

        ["Settings.ThemeLight"] = "☀️ Açık Tema",

        ["Settings.ThemeCyberpunk"] = "🌆 Siberpunk / Neon",

        ["Settings.ThemeMonochrome"] = "🖤 Monokrom / Minimalist",

        ["Settings.GrabberTitle"] = "Toplu Site İndirme & Derinlik:",

        ["Settings.GrabberMaxDepth"] = "Maksimum Tarama Derinliği:",

        ["Settings.GrabberIgnoreExternal"] = "Dış alan adlarındaki bağlantıları yoksay",

        ["Settings.BrowserTitle"] = "Tarayıcı Eklentisi Entegrasyonu:",

        ["Settings.BrowserStatusActive"] = "Köprü Durumu: Aktif (Port 9666)",

        ["Settings.BrowserCopyFolder"] = "📋 Eklenti Klasörünü Kopyala",

        ["Settings.BrowserOpenFolder"] = "📁 Klasörü Aç",

        ["Settings.BehaviorTitle"] = "Sistem & Tepsi (Tray) Davranışı:",

        ["Settings.BehaviorDesc"] = "Windows başlangıcı ve görev çubuğu sistem tepsisi (bildirim alanı) entegrasyon ayarları.",

        ["Settings.BehaviorAutoStart"] = "Windows başladığında FDownloader'ı otomatik çalıştır",

        ["Settings.BehaviorAutoStartDesc"] = "Bilgisayarınız açıldığında indirme yöneticisi otomatik olarak hazır bekler.",

        ["Settings.BehaviorStartMinimized"] = "Açılışta sistem tepsisinde (simge durumunda) başlat",

        ["Settings.BehaviorStartMinimizedDesc"] = "Pencereyi doğrudan ekrana getirmeden sağ alttaki sistem tepsisinde sessizce başlatır.",

        ["Settings.BehaviorMinimizeToTray"] = "Pencere kapatıldığında (X) tamamen kapatmak yerine sistem tepsisine küçült",

        ["Settings.BehaviorMinimizeToTrayDesc"] = "Kapat butonuna basıldığında indirmeler kesilmez, uygulama arka planda çalışmaya devam eder.",

        ["Settings.BehaviorShowTrayIcon"] = "Sistem tepsisinde simge göster",

        ["Settings.BehaviorShowTrayIconDesc"] = "Görev çubuğu bildirim alanında FDownloader hızlı erişim simgesini görüntüler.",

        ["Settings.AboutUpdatesTitle"] = "Otomatik Güncelleme (Velopack & GitHub Releases)",

        ["Settings.AboutUpdatesDesc"] = "GitHub deposundaki en son yayınlanan sürümleri otomatik denetler ve sessizce günceller.",

        ["Settings.AboutCheckUpdates"] = "Güncellemeleri Şimdi Denetle",

        ["Settings.AboutDownloadUpdate"] = "Yeni Sürümü İndir ve Yeniden Başlat",

        ["Settings.AboutChannel"] = "📦 Dağıtım Kanalı:",

        ["Settings.AboutContact"] = "✉️ Hata ve Geliştirme Bildirimleri:",

        ["Settings.AboutContactDesc"] = "Öneri, hata bildirimi ve geliştirmeler için doğrudan iletişim adresi:",

        ["Settings.AboutLicense"] = "📜 Açık Kaynak Lisansları ve Yasal Bildirim:",

        ["Settings.AboutLicenseDesc"] = "FDownloader, MIT lisanslı açık kaynaklı bir yazılımdır. MonoTorrent, yt-dlp, SharpCompress, Velopack ve Avalonia UI bileşenlerini kullanır. Ayrıntılı bildirimler için repo kökündeki THIRD_PARTY_NOTICES.md belgesini inceleyebilirsiniz.",

        ["Settings.AboutLegalWarn"] = "Telif haklarına ve sitelerin kullanım koşullarına uymak kullanıcının sorumluluğundadır.",

        // Tüm Sekme Detayları (TR)

        ["Settings.SoundTitle"] = "İndirme Tamamlanma Zil Sesi:",

        ["Settings.SoundDesc"] = "Bir dosya başarıyla indirildiğinde çalınacak zil sesini seçin ve test edin.",

        ["Settings.SoundListenSelected"] = "Seçileni Dinle",

        ["Settings.SoundPreview"] = "▶ Önizle",

        ["Settings.SoundNotify"] = "🔔 Windows Notify (Varsayılan Zarif Zil)",

        ["Settings.SoundChimes"] = "✨ Chimes (Rüzgar Çanı / Melodik)",

        ["Settings.SoundDing"] = "🛎️ Ding (Kısa & Net Zil Sesi)",

        ["Settings.SoundTada"] = "🎺 Tada (Klasik Windows Kutlama Melodisi)",

        ["Settings.SoundChord"] = "🎹 Chord (Yumuşak Akor Sesi)",

        ["Settings.SoundSilent"] = "🔇 Sessiz (Tamamlanınca ses çalma)",

        ["Settings.TipsTitle"] = "Kullanıcı Önerileri ve İpuçları:",

        ["Settings.TipsDesc"] = "Uygulama açılışında gösterilen tarayıcı eklentisi kurulum rehberi ve faydalı ipucu bildirimlerini yönetin.",

        ["Settings.TipsEnable"] = "Başlangıçta İpuçları ve Önerileri Göster (Eklenti kurulum rehberi vb.)",

        ["Settings.TorrentPortSection"] = "Dinleme Portu ve Bağlantılar",

        ["Settings.TorrentIncomingPort"] = "Gelen Bağlantı Portu:",

        ["Settings.TorrentMaxConnectionsTotal"] = "Genel Azami Bağlantı Sayısı:",

        ["Settings.TorrentMaxConnectionsPerTorrent"] = "Torrent Başına Azami Bağlantı:",

        ["Settings.TorrentProtocolSection"] = "Protokol Özellikleri ve Gizlilik",

        ["Settings.TorrentEnableUpnp"] = "UPnP / NAT-PMP Port Yönlendirmeyi Etkinleştir",

        ["Settings.TorrentEnableDht"] = "DHT (Dağıtık Ağ) ile Eş Bulmayı Etkinleştir",

        ["Settings.TorrentEnablePex"] = "PeX (Eş Değişimi - Peer Exchange) Etkinleştir",

        ["Settings.TorrentEnableLsd"] = "LSD (Yerel Ağ Eş Keşfi) Etkinleştir",

        ["Settings.TorrentEncryption"] = "Protokol Şifreleme (Encryption):",

        ["Settings.TorrentEncAllow"] = "İzin Ver (Tercih Edilen)",

        ["Settings.TorrentEncRequire"] = "Zorunlu Kıl (Yalnızca Şifreli)",

        ["Settings.TorrentEncDisable"] = "Devre Dışı",

        ["Settings.TorrentKillSwitchSection"] = "Ağ Adaptörü ve VPN Kilidi (Kill-Switch)",

        ["Settings.TorrentKillSwitchDesc"] = "BitTorrent trafiğini yalnızca seçtiğiniz ağ kartına (VPN) kilitler. VPN bağlantısı koptuğunda gerçek IP adresinizin sızmaması için indirme ve gönderme anında durdurulur.",

        ["Settings.TorrentBindAdapter"] = "Bağlanılacak Ağ Kartı:",

        ["Settings.TorrentRulesSection"] = "İndirme ve Gönderme (Seeding) Kuralları",

        ["Settings.TorrentAutoSequential"] = "Yeni eklenen torrentleri otomatik 'Sıralı İndir' (Streaming) modunda başlat",

        ["Settings.TorrentStopSeeding"] = "Paylaşım Oranına Ulaşılınca Göndermeyi Durdur:",

        ["Settings.ThemeSelectionTitle"] = "Renk Teması Seçimi:",

        ["Settings.ThemeSelectionDesc"] = "Arayüzün renk tonunu ve kontrastını tercihinize göre anında uyarlayın.",

        ["Settings.ThemeOcean"] = "Okyanus",

        ["Settings.ThemeOceanDesc"] = "Deniz mavisi & ferah vurgular",

        ["Settings.ThemeCyber"] = "Siber Mor",

        ["Settings.ThemeCyberDesc"] = "Neon mor & karanlık estetik",

        ["Settings.ThemeEmerald"] = "Zümrüt Yeşil",

        ["Settings.ThemeEmeraldDesc"] = "Doğal yeşil & dengeli kontrast",

        ["Settings.ThemeAmber"] = "Amber Altın",

        ["Settings.ThemeAmberDesc"] = "Sıcak altın & enerji dolu stil",

        ["Settings.ThemeOled"] = "Karanlık OLED",

        ["Settings.ThemeOledDesc"] = "Saf siyah arka plan & pil dostu",

        ["Settings.BrowserBridgeActive"] = "✓ Köprü Aktif",

        ["Settings.BrowserQuickActions"] = "🚀 Hızlı İşlemler (Tek Tıkla Kurulum Kolaylığı):",

        ["Settings.BrowserOpenDir"] = "Eklenti Klasörünü Aç",

        ["Settings.BrowserCopyPath"] = "Klasör Yolunu Kopyala",

        ["Settings.BrowserOpenChromeExt"] = "Chrome Eklentilerini Aç & Kopyala",

        ["Settings.BrowserCopyEdgeExt"] = "Edge Adresini Kopyala (edge://extensions)",

        ["Settings.BrowserGuideTitle"] = "📋 3 Adımda Tarayıcıya Ekleme Rehberi:",

        ["Settings.BrowserStep1Title"] = "Tarayıcınızda Eklentiler Sayfasını Açın",

        ["Settings.BrowserStep1Desc"] = "Chrome için 'chrome://extensions', Edge için 'edge://extensions' yazıp Enter'a basın.",

        ["Settings.BrowserStep2Title"] = "Geliştirici Modunu (Developer Mode) Açın",

        ["Settings.BrowserStep2Desc"] = "Açılan sayfanın sağ üst köşesindeki 'Geliştirici Modu' (Developer Mode) anahtarını açın.",

        ["Settings.BrowserStep3Title"] = "Paketlenmemiş Öğe Yükle (Load unpacked)",

        ["Settings.BrowserStep3Desc"] = "Sol üstteki 'Paketlenmemiş Öğe Yükle' butonuna basıp yukarıdan kopyaladığınız FDownloader eklenti klasörünü seçin.",

        ["Settings.BrowserInstalledSuccess"] = "🎉 Eklenti Kuruldu mu?",

        ["Settings.BrowserInstalledDesc"] = "Artık videolarda fareyi hareket ettirdiğinizde sağ üstte '⚡ Bu Videoyu İndir' butonu çıkacaktır.",

        ["Settings.BrowserDismissSuggestions"] = "✓ Eklentiyi Kurdum, Önerileri Kapat",

        ["Settings.ThemePlatinum"] = "Platin Gri",

        ["Settings.ThemePlatinumDesc"] = "Mat platin & dengeli gri tonlar",

        ["Settings.ThemeLightClean"] = "Aydınlık",

        ["Settings.ThemeLightCleanDesc"] = "Aydınlık, sade ve net zemin",

        ["Settings.ThemeHighContrast"] = "Yüksek Kontrast",

        ["Settings.ThemeHighContrastDesc"] = "Siyah zemin, keskin kontrast",

        ["Settings.ProgressBarDesignTitle"] = "İlerleme Çubuğu Tasarımı:",

        ["Settings.ProgressBarDesignDesc"] = "İndirme listesindeki ilerleme çubuğunun görsel tarzını tercihinize göre belirleyin.",



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

        ["Wizard.Complete"] = "Finish 🚀",

        // Dinamik Butonlar ve İpuçları

        ["Common.RetryResume"] = "Yeniden Dene / Devam Et",

        ["Common.RetryTooltip"] = "Kopan bağlantıyı tekrar kur ve indirmeye devam et",

        ["Common.StartDownloadTooltip"] = "İndirmeyi başlat",

        ["Common.ResumeTooltip"] = "Duraklatılan indirmeye devam et",

        // Sağ Tık Menüleri

        ["Menu.SelectAll"] = "Tümünü Seç",

        ["Menu.StartAll"] = "Tümünü Başlat",

        ["Menu.PauseAll"] = "Tümünü Duraklat",

        ["Menu.CleanOrphans"] = "Artık Dosyaları Temizle...",

        ["Menu.ClearCompleted"] = "Tamamlananları Listeden Temizle",

        ["Menu.RemoveAll"] = "Tümünü Listeden Kaldır...",

        ["Menu.OpenFolder"] = "Dosya Konumunu Aç",

        ["Menu.OpenFile"] = "Dosyayı Aç",

        ["Menu.ExtractArchive"] = "Arşivi Klasöre Çıkart",

        ["Menu.DownloadTorrentContent"] = "Torrent İçeriğini İndir...",

        ["Menu.Restart"] = "Yeniden Başlat (Baştan İndir)",

        ["Menu.RefreshLink"] = "İndirme Adresini Yenile (Link Refresh)...",

        ["Menu.VerifyChecksum"] = "Dosya Bütünlüğü (MD5 / SHA-256)...",

        ["Menu.SequentialDownload"] = "Sıralı İndir (Ardışık / Akış)",

        ["Menu.CopyLink"] = "Bağlantıyı Kopyala",

        ["Menu.Delete"] = "Kaldır / Sil...",

        ["Menu.ResetColumnWidths"] = "🔄 Sütun Boyutlarını Sıfırla",

        ["Menu.Columns"] = "Sütunları Göster / Gizle",

        // Ekleme Diyaloğu Ekleri

        ["Add.PlaylistSingle"] = "🎯 Yalnızca Bu Şarkıyı İndir (Tek Dosya)",

        ["Add.PlaylistAll"] = "📚 Tüm Çalma Listesini İndir (Listedeki Bütün Şarkılar)",

        ["Add.BetaNotice"] = "* BitTorrent modülü geliştirme (Beta) aşamasındadır. Seçili olmayan dosyalar indirilmeyecektir. Hiçbiri seçilmezse tümü indirilir.",

        // Onay ve Çakışma Diyalogları

        ["Dialog.Delete.No"] = "Hayır (Vazgeç)",

        ["Dialog.Delete.YesList"] = "Evet (Listeden Kaldır)",

        ["Dialog.Delete.YesDisk"] = "Evet (Diskten de Kalıcı Olarak Sil)",

        ["Dialog.Conflict.Title"] = "Hedef klasörde aynı isimde bir dosya zaten bulunuyor.",

        ["Dialog.Conflict.Cancel"] = "❌ İptal Et",

        ["Dialog.Conflict.Overwrite"] = "Üzerine Yaz",

        ["Dialog.Conflict.Rename"] = "Yeniden Adlandır",

        ["Dialog.Refresh.Title"] = "İndirme Adresini Yenile (Link Refresh)",

        ["Dialog.Refresh.Desc"] = "Süresi dolan veya 403 hatası veren indirmeler için yeni URL girin.",

        ["Dialog.Refresh.OldUrl"] = "Mevcut (Eski) Bağlantı:",

        ["Dialog.Refresh.NewUrl"] = "Yeni / Güncel İndirme Bağlantısı:",

        ["Dialog.Refresh.Placeholder"] = "Tarayıcınızdan aldığınız taze indirme linkini (https://...) buraya yapıştırın",

        ["Dialog.Refresh.Cancel"] = "İptal",

        ["Dialog.Refresh.Confirm"] = "Bağlantıyı Yenile ve Devam Et",



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

        ["Settings.ImportFailed"] = "Ayar dosyası okunamadı veya biçimi geçersiz.",



        // P6-07 Güç ve Sistem Davranışları

        ["Settings.PreventSleepWhileDownloading"] = "İndirme sürerken bilgisayarın uyku moduna geçmesini engelle",

        ["Settings.PreventSleepDesc"] = "Aktif indirmeler devam ettiği sürece sistemin uyku veya askıya alma moduna geçmesini önler.",

        ["Settings.WarnOnExitWithActiveDownloads"] = "Aktif indirmeler varken uygulamadan çıkışta onay iste",

        ["Settings.WarnOnExitDesc"] = "İndirmeler sürerken uygulama tamamen kapatılmak istendiğinde uyarı penceresi görüntüler.",

        ["ExitConfirm.Title"] = "Aktif İndirmeler Var",

        ["ExitConfirm.Message"] = "Şu anda devam eden {0} adet indirme bulunuyor. Uygulamadan çıkarsanız indirmeler duraklatılacak.\n\nNe yapmak istersiniz?",

        ["ExitConfirm.MinimizeToTray"] = "Arka Planda Devam Et (Tepsiye Küçült)",

        ["ExitConfirm.ExitAnyway"] = "Yine de Çık (İndirmeleri Duraklat)",



        // Temalar

        ["Theme.OceanBlue"] = "🌊 Okyanus Mavisi (Koyu)",

        ["Theme.CyberPurple"] = "🔮 Siber Mor (Koyu)",

        ["Theme.PlatinumGray"] = "🌪️ Platin Gri (Açık/Modern)",

        ["Theme.LightClean"] = "☀️ Sade & Aydınlık (Açık)",



        // Sihirbaz Ek Metinleri

        ["Wizard.ThemeTip"] = "💡 İpucu: Seçtiğiniz temayı ve renkleri dilediğiniz zaman Ayarlar > Görünüm sekmesinden değiştirebilirsiniz.",

        ["Wizard.Browse"] = "📁 Değiştir...",

        ["Wizard.ExtTitle"] = "Tarayıcı Yakalayıcı Eklentisi (Chrome, Edge, Brave, Opera, Firefox)",

        ["Wizard.ExtStep1"] = "1️⃣ 'Eklenti Klasörünü Aç' butonuna tıklayın ve klasör yolunu kopyalayın.",

        ["Wizard.ExtStep2"] = "2️⃣ Tarayıcınızda 'chrome://extensions' adresini açıp 'Geliştirici modu'nu aktif edin.",

        ["Wizard.ExtStep3"] = "3️⃣ 'Paketlenmemiş öğe yükle' diyerek açılan klasörü seçin.",

        ["Wizard.OpenExtFolder"] = "📂 Eklenti Klasörünü Aç",

        ["Wizard.CopyExtPath"] = "📋 Klasör Yolunu Kopyala",

        ["Wizard.ExtTip"] = "ℹ️ Eklentiyi daha sonra kurmak isterseniz bu adımı atlayabilir, dilediğiniz zaman Ayarlar > Tarayıcı sekmesinden kurabilirsiniz.",



        // Toolbar & Durum

        ["Toolbar.ClipboardWatcher"] = "📋 Pano İzleyici",

        ["Graph.CurrentSpeed"] = "⚡ Anlık:",

        ["Graph.MaxSpeed"] = "🚀 Maks:",

        ["Category.Header"] = "KATEGORİLER",



        // Sütun Başlıkları

                ["Speed.Current"] = "⚡ Anlık:",
        ["Speed.Max"] = "🚀 Maks:",
        ["Settings.ProgClassic"] = "Klasik",
        ["Settings.ProgClassicDesc"] = "Klasik mavi",
        ["Settings.ProgGradient"] = "Gradyan",
        ["Settings.ProgGradientDesc"] = "Mavi-Mor-Yeşil geçiş",
        ["Settings.ProgNeon"] = "Neon",
        ["Settings.ProgNeonDesc"] = "Parlak cyan parlama",
        ["Settings.ProgThin"] = "İnce Çizgi",
        ["Settings.ProgThinDesc"] = "Minimalist zarif çizgi",
                ["Settings.GrabberScan"] = "Sayfayı Tara",
        ["Settings.GrabberAddSelected"] = "Seçilenleri İndirmeye Ekle",
        ["Settings.BrowserBridgeTitle"] = "FDownloader Tarayıcı Köprüsü (127.0.0.1:9666)",
        ["Settings.BrowserBridgeDesc"] = "Chrome, Microsoft Edge, Brave, Opera ve Firefox ile doğrudan çift yönlü haberleşir.",
        ["Settings.BrowserExtUpdateAvailable"] = "Tarayıcı Eklentiniz İçin Yeni Bir Sürüm Mevcut!",
        ["Settings.BrowserUpdateExtButton"] = "Eklentiyi Güncelle & Klasörü Aç",
                ["Extension.Waiting"] = "💤 Tarayıcı Bekleniyor",
        ["Extension.WaitingTooltip"] = "FDownloader yerel köprüsü (127.0.0.1:9666) hazır ve dinliyor. Chrome, Edge vb. tarayıcınızı açtığınızda eklenti otomatik bağlanacaktır.",
        ["Extension.UpdateRequired"] = "⚠️ Güncelleme Gerekli (v{0} → v{1})",
        ["Extension.UpdateRequiredTooltip"] = "Tarayıcınızdaki eklenti sürümü (v{0}) eski. FDownloader ile gelen güncel sürüm: v{1}. Eklentiyi güncellemek için tıklayabilirsiniz.",
        ["Extension.Connected"] = "✓ Eklenti Bağlı (v{0})",
        ["Extension.ConnectedTooltip"] = "Tarayıcı eklentisi devrede ve güncel (v{0}). Çift yönlü köprü aktif.",
        ["Column.Status"] = "Durum",

        ["Column.Type"] = "Tür",

        ["Column.FileName"] = "Dosya Adı",

        ["Column.Size"] = "Boyut",

        ["Column.Progress"] = "İlerleme",

        ["Column.Speed"] = "Anlık Hız",

        ["Column.MaxSpeed"] = "Maks. Hız",

        ["Column.TimeRemaining"] = "Kalan Süre",

        ["Column.Peers"] = "Eşler"

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

        ["Common.Connecting"] = "Connecting...",

        ["Common.Merging"] = "Merging",

        ["Common.Verifying"] = "Verifying",

        ["Common.Retrying"] = "Retrying...",

        ["Common.Cancelled"] = "Cancelled",

        ["Common.ConnectionLost"] = "⚠️ Connection Lost",

        ["Common.ConnectionClosed"] = "⚠️ Disconnected",

        ["Common.SearchingMeta"] = "🔍 Searching Metadata...",

        ["Common.Restart"] = "Restart",



        // Toolbar Hybrid

        ["Toolbar.PauseSelected"] = "Pause Selected",

        ["Toolbar.StartSelected"] = "Start Selected",

        ["Toolbar.PauseAllShort"] = "Pause All",

        ["Toolbar.StartShort"] = "Start",



        // Status Bar

        ["Status.Total"] = "Total: {0}",

        ["Status.Completed"] = "Completed: {0}",

        ["Status.Active"] = "Active: {0}",

        ["Status.Paused"] = "Paused: {0}",

        ["Status.Failed"] = "Failed: {0}",

        ["Status.ActiveDownloading"] = "{0} Downloading",

        ["Status.HighSpeed"] = "High Speed",

        ["Status.LowSpeed"] = "Slow Speed",

        ["Status.Unlimited"] = "[Unlimited]",

        ["Status.Limited"] = "[Limited]",

        ["Status.EmptyList"] = "Download list is empty",

        ["Status.Overview"] = "{0} Downloading • {1} Completed • Total {2}",



        // Detail Tabs & Fields

        ["Detail.TabGeneral"] = "ℹ️ General",

        ["Detail.TabSegments"] = "🟩 Segments",

        ["Detail.TabTorrent"] = "🌊 Torrent (Beta)",

        ["Detail.TabFiles"] = "📁 Files",

        ["Detail.TabPeers"] = "👥 Peers",

        ["Detail.TabTrackers"] = "📡 Trackers",

        ["Detail.FileName"] = "File Name:",

        ["Detail.SavePath"] = "Save Path:",

        ["Detail.SourceUrl"] = "Source URL:",

        ["Detail.DownloadedSize"] = "Downloaded:",

        ["Detail.LastError"] = "Last Error:",

        ["Detail.NoError"] = "No error",

        ["Detail.SegmentsMap"] = "Segment / Chunk Allocation Map (Concurrent Parts):",

        ["Detail.NoItemSelected"] = "No item selected",

        // Add Download Dialog

        ["Add.Title"] = "Add New Download",

        ["Add.UrlLabel"] = "Download Link (URL, Magnet or File):",

        ["Add.UrlPlaceholder"] = "https://... or magnet:?xt=... or file path",

        ["Add.BrowseTorrent"] = "📁 Select File / Torrent...",

        ["Add.BrowseTorrentTooltip"] = "Select .torrent file from computer",

        ["Add.DefaultStatus"] = "Enter URL or Magnet link",

        ["Add.DefaultHint"] = "You can choose MP4 or MP3 format for video or audio links.",

        ["Add.SaveDirectory"] = "Save Directory:",

        ["Add.Browse"] = "📁 Browse",

        ["Add.Start"] = "Start Download",

        ["Add.Cancel"] = "Cancel",

        ["Add.MediaOptions"] = "Media Options:",

        ["Add.TitlePlaceholder"] = "File / Video Title",

        ["Add.Format"] = "Download Format:",

        ["Add.FormatMp3"] = "🎵 Download as MP3 (Audio Only)",

        ["Add.FormatMp4"] = "🎬 Download as MP4 (Video)",

        ["Add.FilesToDownload"] = "Files to Download:",

        ["Add.SelectAll"] = "Select All",

        ["Add.DeselectAll"] = "Deselect All",

        // Settings Dialog Tabs

        ["Settings.Title"] = "Application Settings",

        ["Settings.TabSpeed"] = "⚡ Speed & Network",

        ["Settings.TabStorage"] = "📁 Storage",

        ["Settings.TabNotification"] = "🔔 Notification",

        ["Settings.TabTorrent"] = "🧲 Torrent",

        ["Settings.TabAppearance"] = "🎨 Appearance",

        ["Settings.TabGrabber"] = "🕸️ Site Grabber",

        ["Settings.TabBrowser"] = "🧩 Browser",

        ["Settings.TabBehavior"] = "🖥️ Behavior",

        ["Settings.TabAbout"] = "🔄 About",

        // Settings: Speed & Network

        ["Settings.GeneralRateLimits"] = "General Rate Limits (0 = Unlimited ∞):",

        ["Settings.DownloadLimit"] = "Download Limit (KiB/s):",

        ["Settings.UploadLimit"] = "Upload Limit (KiB/s):",

        ["Settings.AltSpeedLimits"] = "Alternative Speed Limits (When Speed Dial Active):",

        ["Settings.AltSpeedDownload"] = "Download (KiB/s):",

        ["Settings.AltSpeedUpload"] = "Upload (KiB/s):",

        ["Settings.EnableLowSpeed"] = "⏱️ Enable Low Speed (Limited) Mode (Apply limits)",

        ["Settings.SchedulerTitle"] = "Advanced Download Scheduler",

        ["Settings.SchedulerDesc"] = "Automatically start, stop downloads or shut down in defined hours.",

        ["Settings.StartTime"] = "Start Time:",

        ["Settings.StartAction"] = "Start Action:",

        ["Settings.EndTime"] = "End Time:",

        ["Settings.EndAction"] = "End Action:",

        ["Settings.StartPending"] = "▶ Start Pending",

        ["Settings.FullSpeed"] = "⚡ Switch to Full Speed (Unlimited)",

        ["Settings.PauseAll"] = "⏸ Pause All",

        ["Settings.SlowSpeed"] = "🐢 Switch to Low Speed Mode",

        ["Settings.PauseShutdown"] = "🛑 Pause and Shut Down PC",

        ["Settings.SchedulerDays"] = "Days Scheduler Will Run:",

        ["Settings.Monday"] = "Monday",

        ["Settings.Tuesday"] = "Tuesday",

        ["Settings.Wednesday"] = "Wednesday",

        ["Settings.Thursday"] = "Thursday",

        ["Settings.Friday"] = "Friday",

        ["Settings.Saturday"] = "Saturday",

        ["Settings.Sunday"] = "Sunday",

        // Settings: Storage

        ["Settings.DefaultFolderAndEngine"] = "Default Folder and Engine Parameters:",

        ["Settings.ConcurrentDownloads"] = "Concurrent Downloads:",

        ["Settings.SegmentCount"] = "Segments (Connections):",

        ["Settings.BrowseButton"] = "📁 Browse...",

        // Log & System

        ["Settings.OpenLogFile"] = "📄 Open Log File",

        ["Settings.OpenLogFolder"] = "📁 Open Log Folder",

        ["Settings.CopyLogs"] = "📋 Copy Logs",

        ["Settings.RecentLogsTitle"] = "🔍 System & Network Error Logs:",

        // Extended Settings Dictionary (EN)

        ["Settings.ActionStartPending"] = "▶ Start Pending",

        ["Settings.ActionFullSpeed"] = "⚡ Switch to Full Speed (Unlimited)",

        ["Settings.ActionPauseAll"] = "⏸ Pause All",

        ["Settings.ActionSlowSpeed"] = "🐢 Switch to Low Speed Mode",

        ["Settings.ActionPauseShutdown"] = "🛑 Pause and Shut Down PC",

        ["Settings.SaveDirectory"] = "Save Directory:",

        ["Settings.BrowseButtonDots"] = "📁 Browse...",

        ["Settings.AutoExtractTitle"] = "Automatic Archive Extraction (Auto-Extract):",

        ["Settings.AutoExtractDesc"] = "Automatically extracts .zip, .rar, .7z, .tar, .gz archives to folder when download completes.",

        ["Settings.AutoExtractEnabled"] = "Automatically extract downloaded archives",

        ["Settings.AutoExtractSubfolder"] = "Extract into a subfolder with archive name",

        ["Settings.AutoExtractDelete"] = "Delete original archive file after successful extraction",

        ["Settings.NotifyTitle"] = "Notification and Sound Alerts:",

        ["Settings.NotifyToastEnabled"] = "Show desktop toast notification when download completes",

        ["Settings.NotifySoundEnabled"] = "Play sound when download finishes",

        ["Settings.TorrentTitle"] = "BitTorrent Engine Settings:",

        ["Settings.TorrentPort"] = "Listening Port (0 = Auto):",

        ["Settings.TorrentMaxPeers"] = "Maximum Peers per Torrent:",

        ["Settings.TorrentGlobalPeers"] = "Global Maximum Peer Limit:",

        ["Settings.TorrentEnableDht"] = "Enable DHT Network (For trackerless torrents)",

        ["Settings.TorrentEnablePex"] = "Enable Peer Exchange (PEX)",

        ["Settings.TorrentEnableUtp"] = "Enable uTP Protocol (Prevents network congestion)",

        ["Settings.AppearanceTitle"] = "Theme and Color Palette:",

        ["Settings.ThemeDark"] = "🌙 Dark Theme (Default)",

        ["Settings.ThemeLight"] = "☀️ Light Theme",

        ["Settings.ThemeCyberpunk"] = "🌆 Cyberpunk / Neon",

        ["Settings.ThemeMonochrome"] = "🖤 Monochrome / Minimalist",

        ["Settings.GrabberTitle"] = "Batch Site Grabber & Depth:",

        ["Settings.GrabberMaxDepth"] = "Maximum Crawl Depth:",

        ["Settings.GrabberIgnoreExternal"] = "Ignore links to external domains",

        ["Settings.BrowserTitle"] = "Browser Extension Integration:",

        ["Settings.BrowserStatusActive"] = "Bridge Status: Active (Port 9666)",

        ["Settings.BrowserCopyFolder"] = "📋 Copy Extension Folder",

        ["Settings.BrowserOpenFolder"] = "📁 Open Folder",

        ["Settings.BehaviorTitle"] = "System & Tray Behavior:",

        ["Settings.BehaviorDesc"] = "Windows startup and system tray integration settings.",

        ["Settings.BehaviorAutoStart"] = "Run FDownloader automatically on Windows startup",

        ["Settings.BehaviorAutoStartDesc"] = "Download manager starts automatically when your PC boots.",

        ["Settings.BehaviorStartMinimized"] = "Start minimized to system tray on boot",

        ["Settings.BehaviorStartMinimizedDesc"] = "Starts silently in the tray without showing the main window.",

        ["Settings.BehaviorMinimizeToTray"] = "Minimize to tray when window is closed (X) instead of quitting",

        ["Settings.BehaviorMinimizeToTrayDesc"] = "Downloads continue in the background when the close button is clicked.",

        ["Settings.BehaviorShowTrayIcon"] = "Show icon in system tray",

        ["Settings.BehaviorShowTrayIconDesc"] = "Displays FDownloader icon in the notification area.",

        ["Settings.AboutUpdatesTitle"] = "Automatic Updates (Velopack & GitHub Releases)",

        ["Settings.AboutUpdatesDesc"] = "Automatically checks for releases on GitHub and silently updates.",

        ["Settings.AboutCheckUpdates"] = "Check for Updates Now",

        ["Settings.AboutDownloadUpdate"] = "Download Update and Restart",

        ["Settings.AboutChannel"] = "📦 Distribution Channel:",

        ["Settings.AboutContact"] = "✉️ Bug & Feature Inquiries:",

        ["Settings.AboutContactDesc"] = "Direct contact address for suggestions and bug reports:",

        ["Settings.AboutLicense"] = "📜 Open Source Licenses & Legal Notice:",

        ["Settings.AboutLicenseDesc"] = "FDownloader is open source software licensed under MIT. It uses MonoTorrent, yt-dlp, SharpCompress, Velopack and Avalonia UI components.",

        ["Settings.AboutLegalWarn"] = "It is the user's responsibility to comply with copyrights and site terms of service.",

        // All Tab Details (EN)

        ["Settings.SoundTitle"] = "Download Completion Ringtone:",

        ["Settings.SoundDesc"] = "Select and test the ringtone to play when a file completes downloading.",

        ["Settings.SoundListenSelected"] = "Listen Selected",

        ["Settings.SoundPreview"] = "▶ Preview",

        ["Settings.SoundNotify"] = "🔔 Windows Notify (Default Elegant Ringtone)",

        ["Settings.SoundChimes"] = "✨ Chimes (Wind Chimes / Melodic)",

        ["Settings.SoundDing"] = "🛎️ Ding (Short & Clear Ringtone)",

        ["Settings.SoundTada"] = "🎺 Tada (Classic Celebration Melodic)",

        ["Settings.SoundChord"] = "🎹 Chord (Soft Chord Sound)",

        ["Settings.SoundSilent"] = "🔇 Silent (Do not play sound on completion)",

        ["Settings.TipsTitle"] = "User Tips and Suggestions:",

        ["Settings.TipsDesc"] = "Manage browser extension installation guide and helpful tip notifications shown at startup.",

        ["Settings.TipsEnable"] = "Show Tips and Suggestions at Startup (Extension install guide, etc.)",

        ["Settings.TorrentPortSection"] = "Listening Port and Connections",

        ["Settings.TorrentIncomingPort"] = "Incoming Connection Port:",

        ["Settings.TorrentMaxConnectionsTotal"] = "Global Maximum Connections:",

        ["Settings.TorrentMaxConnectionsPerTorrent"] = "Maximum Connections per Torrent:",

        ["Settings.TorrentProtocolSection"] = "Protocol Features and Privacy",

        ["Settings.TorrentEnableUpnp"] = "Enable UPnP / NAT-PMP Port Forwarding",

        ["Settings.TorrentEnableDht"] = "Enable DHT (Distributed Network) Peer Discovery",

        ["Settings.TorrentEnablePex"] = "Enable PeX (Peer Exchange)",

        ["Settings.TorrentEnableLsd"] = "Enable LSD (Local Peer Discovery)",

        ["Settings.TorrentEncryption"] = "Protocol Encryption:",

        ["Settings.TorrentEncAllow"] = "Allow (Preferred)",

        ["Settings.TorrentEncRequire"] = "Require (Encrypted Only)",

        ["Settings.TorrentEncDisable"] = "Disabled",

        ["Settings.TorrentKillSwitchSection"] = "Network Adapter & VPN Kill-Switch",

        ["Settings.TorrentKillSwitchDesc"] = "Locks BitTorrent traffic strictly to the selected network adapter (VPN). If the VPN disconnects, traffic stops immediately to prevent IP leaks.",

        ["Settings.TorrentBindAdapter"] = "Bind to Network Interface:",

        ["Settings.TorrentRulesSection"] = "Download and Seeding Rules",

        ["Settings.TorrentAutoSequential"] = "Start new torrents automatically in Sequential Download (Streaming) mode",

        ["Settings.TorrentStopSeeding"] = "Stop Seeding When Share Ratio Reached:",

        ["Settings.ThemeSelectionTitle"] = "Color Theme Selection:",

        ["Settings.ThemeSelectionDesc"] = "Instantly adjust the UI color scheme and contrast to your preference.",

        ["Settings.ThemeOcean"] = "Ocean Blue",

        ["Settings.ThemeOceanDesc"] = "Deep blue & crisp accents",

        ["Settings.ThemeCyber"] = "Cyber Purple",

        ["Settings.ThemeCyberDesc"] = "Neon violet & dark aesthetics",

        ["Settings.ThemeEmerald"] = "Emerald Green",

        ["Settings.ThemeEmeraldDesc"] = "Natural green & balanced contrast",

        ["Settings.ThemeAmber"] = "Amber Gold",

        ["Settings.ThemeAmberDesc"] = "Warm gold & energetic style",

        ["Settings.ThemeOled"] = "Dark OLED",

        ["Settings.ThemeOledDesc"] = "Pure black background & eye friendly",

        ["Settings.BrowserBridgeActive"] = "✓ Bridge Active",

        ["Settings.BrowserQuickActions"] = "🚀 Quick Actions (One-Click Setup Ease):",

        ["Settings.BrowserOpenDir"] = "Open Extension Folder",

        ["Settings.BrowserCopyPath"] = "Copy Folder Path",

        ["Settings.BrowserOpenChromeExt"] = "Open Chrome Extensions & Copy",

        ["Settings.BrowserCopyEdgeExt"] = "Copy Edge Address (edge://extensions)",

        ["Settings.BrowserGuideTitle"] = "📋 3-Step Browser Setup Guide:",

        ["Settings.BrowserStep1Title"] = "Open Extensions Page in Your Browser",

        ["Settings.BrowserStep1Desc"] = "For Chrome type 'chrome://extensions', for Edge type 'edge://extensions' and press Enter.",

        ["Settings.BrowserStep2Title"] = "Turn on Developer Mode",

        ["Settings.BrowserStep2Desc"] = "Toggle the 'Developer mode' switch in the top right corner.",

        ["Settings.BrowserStep3Title"] = "Load Unpacked Extension",

        ["Settings.BrowserStep3Desc"] = "Click 'Load unpacked' in top left and select the FDownloader extension folder.",

        ["Settings.BrowserInstalledSuccess"] = "🎉 Extension Installed?",

        ["Settings.BrowserInstalledDesc"] = "When hovering over videos, a '⚡ Download This Video' button will now appear.",

        ["Settings.BrowserDismissSuggestions"] = "✓ I Installed Extension, Dismiss Guide",

        ["Settings.ThemePlatinum"] = "Platinum Gray",

        ["Settings.ThemePlatinumDesc"] = "Matte platinum & balanced gray tones",

        ["Settings.ThemeLightClean"] = "Light Clean",

        ["Settings.ThemeLightCleanDesc"] = "Bright, clean and clear background",

        ["Settings.ThemeHighContrast"] = "High Contrast",

        ["Settings.ThemeHighContrastDesc"] = "Pure black background & sharp contrast",

        ["Settings.ProgressBarDesignTitle"] = "Progress Bar Design:",

        ["Settings.ProgressBarDesignDesc"] = "Customize the visual appearance and style of progress bars in download list.",



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

        // Dynamic Actions & Tooltips

        ["Common.RetryResume"] = "Retry / Resume",

        ["Common.RetryTooltip"] = "Reconnect and resume download",

        ["Common.StartDownloadTooltip"] = "Start download",

        ["Common.ResumeTooltip"] = "Resume paused download",

        // Context Menus

        ["Menu.SelectAll"] = "Select All",

        ["Menu.StartAll"] = "Start All",

        ["Menu.PauseAll"] = "Pause All",

        ["Menu.CleanOrphans"] = "Clean Orphan Files...",

        ["Menu.ClearCompleted"] = "Clear Completed from List",

        ["Menu.RemoveAll"] = "Remove All from List...",

        ["Menu.OpenFolder"] = "Open File Location",

        ["Menu.OpenFile"] = "Open File",

        ["Menu.ExtractArchive"] = "Extract Archive to Folder",

        ["Menu.DownloadTorrentContent"] = "Download Torrent Content...",

        ["Menu.Restart"] = "Restart (Download Again)",

        ["Menu.RefreshLink"] = "Refresh Download Link...",

        ["Menu.VerifyChecksum"] = "File Integrity (MD5 / SHA-256)...",

        ["Menu.SequentialDownload"] = "Sequential Download (Stream)",

        ["Menu.CopyLink"] = "Copy Link",

        ["Menu.Delete"] = "Delete / Remove...",

        ["Menu.ResetColumnWidths"] = "🔄 Reset Column Widths",

        ["Menu.Columns"] = "Show / Hide Columns",

        // Add Download Dialog Additions

        ["Add.PlaylistSingle"] = "🎯 Download Only This Song (Single File)",

        ["Add.PlaylistAll"] = "📚 Download Full Playlist (All Songs)",

        ["Add.BetaNotice"] = "* BitTorrent module is in development (Beta). Unselected files will not be downloaded. If none selected, all are downloaded.",

        // Confirmation & Conflict Dialogs

        ["Dialog.Delete.No"] = "No (Cancel)",

        ["Dialog.Delete.YesList"] = "Yes (Remove from List)",

        ["Dialog.Delete.YesDisk"] = "Yes (Delete from Disk Permanently)",

        ["Dialog.Conflict.Title"] = "A file with the same name already exists in the target folder.",

        ["Dialog.Conflict.Cancel"] = "❌ Cancel",

        ["Dialog.Conflict.Overwrite"] = "Overwrite",

        ["Dialog.Conflict.Rename"] = "Rename",

        ["Dialog.Refresh.Title"] = "Refresh Download Link (Link Refresh)",

        ["Dialog.Refresh.Desc"] = "Enter new URL for expired links or 403 errors.",

        ["Dialog.Refresh.OldUrl"] = "Current (Old) Link:",

        ["Dialog.Refresh.NewUrl"] = "New / Fresh Download Link:",

        ["Dialog.Refresh.Placeholder"] = "Paste fresh download link from browser here (https://...)",

        ["Dialog.Refresh.Cancel"] = "Cancel",

        ["Dialog.Refresh.Confirm"] = "Refresh Link and Resume",



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

        ["Settings.ImportFailed"] = "Failed to import settings file or format is invalid.",



        // P6-07 Power & System Behaviors

        ["Settings.PreventSleepWhileDownloading"] = "Prevent computer from sleeping while downloading",

        ["Settings.PreventSleepDesc"] = "Prevents Windows from entering sleep or standby mode while downloads are in progress.",

        ["Settings.WarnOnExitWithActiveDownloads"] = "Warn before closing if there are active downloads",

        ["Settings.WarnOnExitDesc"] = "Shows a confirmation dialog when attempting to close the app while downloads are running.",

        ["ExitConfirm.Title"] = "Active Downloads in Progress",

        ["ExitConfirm.Message"] = "There are currently {0} active download(s). If you exit, the downloads will be paused.\n\nWhat would you like to do?",

        ["ExitConfirm.MinimizeToTray"] = "Keep Running in Background (Minimize to Tray)",

        ["ExitConfirm.ExitAnyway"] = "Exit Anyway (Pause Downloads)",



        // Themes

        ["Theme.OceanBlue"] = "🌊 Ocean Blue (Dark)",

        ["Theme.CyberPurple"] = "🔮 Cyber Purple (Dark)",

        ["Theme.PlatinumGray"] = "🌪️ Platinum Gray (Light/Modern)",

        ["Theme.LightClean"] = "☀️ Pure & Clean (Light)",



        // Wizard Additional Strings

        ["Wizard.ThemeTip"] = "💡 Tip: You can change the theme and accent colors anytime in Settings > Appearance.",

        ["Wizard.Browse"] = "📁 Browse...",

        ["Wizard.ExtTitle"] = "Browser Grabber Extension (Chrome, Edge, Brave, Opera, Firefox)",

        ["Wizard.ExtStep1"] = "1️⃣ Click 'Open Extension Folder' and copy the folder path.",

        ["Wizard.ExtStep2"] = "2️⃣ Open 'chrome://extensions' in your browser and enable 'Developer mode'.",

        ["Wizard.ExtStep3"] = "3️⃣ Click 'Load unpacked' and select the extension folder.",

        ["Wizard.OpenExtFolder"] = "📂 Open Extension Folder",

        ["Wizard.CopyExtPath"] = "📋 Copy Folder Path",

        ["Wizard.ExtTip"] = "ℹ️ You can skip this step now and install the browser extension later from Settings > Browser.",



        // Toolbar & Status

        ["Toolbar.ClipboardWatcher"] = "📋 Clipboard Monitor",

        ["Graph.CurrentSpeed"] = "⚡ Speed:",

        ["Graph.MaxSpeed"] = "🚀 Peak:",

        ["Category.Header"] = "CATEGORIES",



        // Column Headers

                ["Speed.Current"] = "⚡ Current:",
        ["Speed.Max"] = "🚀 Max:",
        ["Settings.ProgClassic"] = "Classic",
        ["Settings.ProgClassicDesc"] = "Classic blue",
        ["Settings.ProgGradient"] = "Gradient",
        ["Settings.ProgGradientDesc"] = "Blue-Purple-Green gradient",
        ["Settings.ProgNeon"] = "Neon",
        ["Settings.ProgNeonDesc"] = "Bright cyan glow",
        ["Settings.ProgThin"] = "Thin Line",
        ["Settings.ProgThinDesc"] = "Minimalist elegant line",
                ["Settings.GrabberScan"] = "Scan Page",
        ["Settings.GrabberAddSelected"] = "Add Selected to Downloads",
        ["Settings.BrowserBridgeTitle"] = "FDownloader Browser Bridge (127.0.0.1:9666)",
        ["Settings.BrowserBridgeDesc"] = "Communicates directly with Chrome, Microsoft Edge, Brave, Opera and Firefox.",
        ["Settings.BrowserExtUpdateAvailable"] = "A New Version of Your Browser Extension is Available!",
        ["Settings.BrowserUpdateExtButton"] = "Update Extension & Open Folder",
                ["Extension.Waiting"] = "💤 Waiting for Browser",
        ["Extension.WaitingTooltip"] = "FDownloader local bridge (127.0.0.1:9666) is ready and listening. The extension will automatically connect when you open Chrome, Edge, etc.",
        ["Extension.UpdateRequired"] = "⚠️ Update Required (v{0} → v{1})",
        ["Extension.UpdateRequiredTooltip"] = "The browser extension version (v{0}) is outdated. Current bundled version: v{1}. Click to update.",
        ["Extension.Connected"] = "✓ Extension Connected (v{0})",
        ["Extension.ConnectedTooltip"] = "Browser extension is connected and up to date (v{0}). Two-way bridge is active.",
        ["Column.Status"] = "Status",

        ["Column.Type"] = "Type",

        ["Column.FileName"] = "File Name",

        ["Column.Size"] = "Size",

        ["Column.Progress"] = "Progress",

        ["Column.Speed"] = "Speed",

        ["Column.MaxSpeed"] = "Max Speed",

        ["Column.TimeRemaining"] = "ETA",

        ["Column.Peers"] = "Peers"

    };

}



