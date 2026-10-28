using Avalonia;
using System;
using System.IO;
using Velopack;

namespace FDownloader.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

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
