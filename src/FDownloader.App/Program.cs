using Avalonia;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FDownloader.App.Utilities;
using Sentry;
using Velopack;

namespace FDownloader.App;

sealed class Program
{
    private const string AppMutexName = @"Local\FDownloader_SingleInstance_Mutex_9666";

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        // Tekil Oturum (Single Instance) Yönetimi & Kendini Onaran Başlatma
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
                bool wakeSuccess = false;
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        AllowSetForegroundWindow(ASFW_ANY);
                    }

                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(1500) };
                    var response = client.PostAsync("http://127.0.0.1:9666/show", null).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        wakeSuccess = true;
                    }
                }
                catch { }

                if (wakeSuccess)
                {
                    return; // Mevcut çalışan ana kopya başarıyla uyandırıldı ve ekrana getirildi
                }

                // HTTP çağrısı yanıt vermediyse arka plandaki kopya donmuş/zombi durumdadır
                try
                {
                    var currentPid = Environment.ProcessId;
                    var staleProcesses = System.Diagnostics.Process.GetProcessesByName("FDownloader.App")
                        .Concat(System.Diagnostics.Process.GetProcessesByName("FDownloader"))
                        .Where(p => p.Id != currentPid);

                    foreach (var p in staleProcesses)
                    {
                        try
                        {
                            p.Kill(entireProcessTree: true);
                            p.WaitForExit(1000);
                        }
                        catch { }
                    }
                }
                catch { }

                // Zombi süreç temizlendi; artık bu yeni süreç ana uygulama olarak devam edebilir
            }
            else
            {
                return; // Windows açılışı zaten ana kopya tarafından başlatılmış
            }
        }

        // Güncelleme sonrası veya açılışta eski versiyondan kalan masaüstü simgesini ve hedefini arka planda güvenle güncelle (0ms UI gecikmesi)
        _ = System.Threading.Tasks.Task.Run(() => DesktopShortcutHelper.UpdateDesktopShortcuts());

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
                o.Release = "fdownloader@1.5.7";
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
