using Avalonia;
using System;
using System.IO;
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
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] UNHANDLED EXCEPTION: {ex?.Message}\n{ex?.StackTrace}\n\n");
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] UNOBSERVED TASK EXCEPTION: {e.Exception.Message}\n{e.Exception.StackTrace}\n\n");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
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
