using Avalonia;
using System;
using System.IO;
using Sentry;
using Velopack;

namespace FDownloader.App;

sealed class Program
{
    private const string AppMutexName = "FDownloader_SingleInstance_Mutex_9666";

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        // Tekil Oturum (Single Instance) Yönetimi
        using var mutex = new System.Threading.Mutex(true, AppMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            var isAutoStart = args.Any(a =>
                a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-tray", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

            if (!isAutoStart)
            {
                // Kullanıcı masaüstünden/başlat menüsünden elle tıkladı: Çalışan kopyayı tepsiden ekrana öne getir
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                    _ = client.PostAsync("http://127.0.0.1:9666/show", null).GetAwaiter().GetResult();
                }
                catch { }
            }

            return; // İkinci kopyayı sonlandır
        }

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null) SentrySdk.CaptureException(ex);
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] UNHANDLED EXCEPTION: {ex?.Message}\n{ex?.StackTrace}\n\n");
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            if (e.Exception != null) SentrySdk.CaptureException(e.Exception);
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] UNOBSERVED TASK EXCEPTION: {e.Exception.Message}\n{e.Exception.StackTrace}\n\n");
            e.SetObserved();
        };

        // Sentry Canlı Hata Takibi (DSN tanımlıysa aktifleşir, tanımlı değilse sessiz kalır)
        var sentryDsn = Environment.GetEnvironmentVariable("FDOWNLOADER_SENTRY_DSN");
        using var sentry = !string.IsNullOrWhiteSpace(sentryDsn) 
            ? SentrySdk.Init(o =>
            {
                o.Dsn = sentryDsn;
                o.TracesSampleRate = 1.0;
                o.Release = "fdownloader@1.5.6";
                // P3-09: Kişisel Veri & Gizlilik Filtresi (BeforeSend)
                o.SetBeforeSend((sentryEvent, hint) =>
                {
                    if (sentryEvent.Message?.Message != null)
                    {
                        sentryEvent.Message = Core.Logging.SensitiveDataMasker.MaskMessage(sentryEvent.Message.Message);
                    }
                    if (sentryEvent.User != null)
                    {
                        sentryEvent.User.Username = null;
                        sentryEvent.User.IpAddress = null;
                        sentryEvent.User.Email = null;
                    }
                    return sentryEvent;
                });
            }) 
            : null;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] APP CRASH: {ex.Message}\n{ex.StackTrace}\n\n");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
