using Avalonia;
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

            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            mainVm.RequestBringToFront += () => mainWindow.BringToFront();

            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) =>
            {
                bridge.Stop();
            };
        }

        base.OnFrameworkInitializationCompleted();
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