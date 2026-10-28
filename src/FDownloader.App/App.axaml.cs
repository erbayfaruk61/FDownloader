using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FDownloader.App.ViewModels;
using FDownloader.App.Views;
using FDownloader.Core.Events;
using FDownloader.Core.Manager;
using FDownloader.Core.Storage;
using FDownloader.Core.Torrent;
using FDownloader.Core.Utilities;
using FDownloader.Core.Video;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FDownloader.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            var bridge = _serviceProvider.GetRequiredService<FDownloader.Core.Network.BrowserBridgeService>();
            bridge.Start();

            var hasTrayArg = desktop.Args?.Any(a =>
                a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-tray", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--autostart", StringComparison.OrdinalIgnoreCase)) == true;

            // SADECE Windows açılış argümanı (--tray) varsa ve ayarlarda "Açılışta sistem tepsisinde başlat" seçiliyse küçült.
            // Kullanıcı masaüstünden/başlat menüsünden çift tıkladığında hiçbir argüman gelmez; pencere doğrudan ekranda açılır!
            var startMinimized = hasTrayArg && mainVm.StartMinimizedToTray;

            var mainWindow = new MainWindow
            {
                DataContext = mainVm,
                StartMinimized = startMinimized
            };

            if (startMinimized)
            {
                mainWindow.Opacity = 0;
            }

            mainVm.RequestBringToFront += () => mainWindow.BringToFront();

            var eventBus = _serviceProvider.GetRequiredService<FDownloader.Core.Events.DownloadEventBus>();
            eventBus.Subscribe<FDownloader.Core.Events.ShowMainWindowEvent>(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => mainWindow.BringToFront());
            });

            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) =>
            {
                bridge.Stop();
            };

            SetupTrayIcon(mainWindow, mainVm);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private TrayIcon? _trayIcon;

    private void SetupTrayIcon(MainWindow mainWindow, MainViewModel mainVm)
    {
        try
        {
            _trayIcon = new TrayIcon
            {
                ToolTipText = "FDownloader - İndirme Yöneticisi",
                IsVisible = mainVm.ShowTrayIcon
            };

            if (mainWindow.Icon != null)
            {
                _trayIcon.Icon = mainWindow.Icon;
            }

            var nativeMenu = new NativeMenu();

            var showItem = new NativeMenuItem("⚡ FDownloader'ı Göster");
            showItem.Click += (_, _) => mainWindow.BringToFront();
            nativeMenu.Add(showItem);

            nativeMenu.Add(new NativeMenuItemSeparator());

            var resumeAllItem = new NativeMenuItem("▶️ Bekleyenleri Başlat");
            resumeAllItem.Click += (_, _) =>
            {
                if (mainVm.StartAllCommand.CanExecute(null))
                    mainVm.StartAllCommand.Execute(null);
            };
            nativeMenu.Add(resumeAllItem);

            var pauseAllItem = new NativeMenuItem("⏸️ Tümünü Duraklat");
            pauseAllItem.Click += (_, _) =>
            {
                if (mainVm.PauseAllCommand.CanExecute(null))
                    mainVm.PauseAllCommand.Execute(null);
            };
            nativeMenu.Add(pauseAllItem);

            var settingsItem = new NativeMenuItem("⚙️ Ayarlar");
            settingsItem.Click += (_, _) =>
            {
                mainWindow.BringToFront();
                mainVm.OpenSettingsDialog();
            };
            nativeMenu.Add(settingsItem);

            nativeMenu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("❌ Çıkış");
            exitItem.Click += (_, _) =>
            {
                mainWindow.RequestExit();
            };
            nativeMenu.Add(exitItem);

            _trayIcon.Menu = nativeMenu;
            _trayIcon.Clicked += (_, _) => mainWindow.BringToFront();

            var icons = new TrayIcons { _trayIcon };
            TrayIcon.SetIcons(this, icons);

            mainVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ShowTrayIcon))
                {
                    _trayIcon.IsVisible = mainVm.ShowTrayIcon;
                }
            };
        }
        catch
        {
            // Tepsi simgesi yüklenemezse sessizce devam et
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // HttpClient (SocketsHttpHandler ile otomatik redirect ve ham akış)
        services.AddSingleton(sp =>
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression = System.Net.DecompressionMethods.None
            };
            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        });

        // Core Veritabanı ve Repository
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fdownloader", "downloads.db");
        services.AddSingleton(sp =>
        {
            var db = new Database(dbPath, sp.GetRequiredService<ILogger<Database>>());
            db.Initialize();
            return db;
        });
        services.AddSingleton<DownloadRepository>();

        // Event Bus
        services.AddSingleton<DownloadEventBus>();

        // Motorlar
        services.AddSingleton<TorrentService>();
        services.AddSingleton<YtDlpMediaService>();
        services.AddSingleton<ClipboardWatcher>();
        services.AddSingleton<SiteGrabberService>();
        services.AddSingleton<FDownloader.Core.Network.BrowserBridgeService>();
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<FDownloader.App.Services.UpdateService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
    }
}