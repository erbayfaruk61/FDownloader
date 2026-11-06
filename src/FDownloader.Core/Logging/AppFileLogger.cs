using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Logging;

/// <summary>
/// Uygulama loglarını hem %USERPROFILE%/.fdownloader/logs/app.log dosyasına yazar
/// hem de son 50 satırı UI'da anlık görüntülenmek üzere bellekte tutar.
/// </summary>
public sealed class AppFileLoggerProvider : ILoggerProvider
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fdownloader", "logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, "app.log");

    private static readonly ConcurrentQueue<string> _recentLogs = new();
    private const int MaxRecentLogs = 50;
    private static readonly object _fileLock = new();

    public static IReadOnlyList<string> RecentLogs => _recentLogs.ToArray();
    public static string LogPath => LogFilePath;
    public static string LogDir => LogDirectory;

    static AppFileLoggerProvider()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }
        catch { }
    }

    public static void OpenLogFile()
    {
        try
        {
            if (!File.Exists(LogFilePath))
            {
                File.WriteAllText(LogFilePath, $"--- FDownloader Log Başlangıcı: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---\n");
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = LogFilePath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void OpenLogFolder()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDirectory,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void AppendLog(LogLevel level, string category, string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "LOG"
        };

        var shortCategory = category.Contains('.') ? category.Substring(category.LastIndexOf('.') + 1) : category;
        var line = $"[{timestamp}] [{levelStr}] [{shortCategory}] {message}";
        if (exception != null)
        {
            line += $" | Hata: {exception.GetType().Name}: {exception.Message}";
        }

        _recentLogs.Enqueue(line);
        while (_recentLogs.Count > MaxRecentLogs)
        {
            _recentLogs.TryDequeue(out _);
        }

        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch { }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new AppFileLogger(categoryName);
    }

    public void Dispose() { }
}

internal sealed class AppFileLogger : ILogger
{
    private readonly string _category;

    public AppFileLogger(string category)
    {
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        AppFileLoggerProvider.AppendLog(logLevel, _category, message, exception);
    }
}

