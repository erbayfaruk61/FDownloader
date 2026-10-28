using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using FDownloader.Core.Domain;
using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Video;

/// <summary>
/// YouTube, Twitter, Instagram, TikTok vb. platformlardan video/ses indirmek için yt-dlp sarmalayıcısı.
/// Video meta verisini alır, MP4/MP3 format seçimini destekler ve canlı ilerleme yayını yapar.
/// </summary>
public sealed class YtDlpMediaService
{
    private const string DefaultExtractorArgs = "--extractor-args \"youtube:player_client=web,default,android\"";
    private const string DefaultVideoFormat = "-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best[ext=mp4]/best\" --merge-output-format mp4";
    private const string DefaultAudioFormat = "-x --audio-format mp3 --audio-quality 0 --embed-metadata";
    private const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string YtDlpExecutableName = "yt-dlp.exe";
    private const string FfmpegExecutableName = "ffmpeg.exe";
    private const string FfprobeExecutableName = "ffprobe.exe";
    private const string NodeExecutableName = "node.exe";

    private static readonly string[] NodePathCandidates =
    [
        @"C:\Program Files\nodejs\node.exe",
        @"C:\Program Files (x86)\nodejs\node.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "node", "node.exe")
    ];

    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<YtDlpMediaService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _toolsDir;
    private readonly string _ytDlpPath;
    private readonly ConcurrentDictionary<long, Process> _runningProcesses = new();

    /// <summary>
    /// Belirtilen göreve ait çalışan yt-dlp ve ffmpeg süreç ağacını anında sonlandırır.
    /// </summary>
    public void StopDownload(long taskId)
    {
        if (_runningProcesses.TryRemove(taskId, out var proc))
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch { }
            finally
            {
                try { proc.Dispose(); } catch { }
            }
        }
    }

    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+([0-9\.]+)%\s+of\s+~?([0-9\.]+[KMGkmg]?[iI]?[bB]?)\s+at\s+([0-9\.]+[KMGkmg]?[iI]?[bB]?/s)\s+ETA\s+([0-9:]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static double ParseSpeed(string speedStr)
    {
        var s = speedStr.Replace("/s", "", StringComparison.OrdinalIgnoreCase).Trim();
        return ParseBytes(s);
    }

    private static long ParseBytes(string sizeStr)
    {
        sizeStr = sizeStr.Trim('~', ' ');
        if (sizeStr.EndsWith("GiB", StringComparison.OrdinalIgnoreCase) || sizeStr.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            var num = sizeStr.EndsWith("GiB", StringComparison.OrdinalIgnoreCase) ? sizeStr[..^3] : sizeStr[..^2];
            if (double.TryParse(num, System.Globalization.CultureInfo.InvariantCulture, out var val))
                return (long)(val * 1024 * 1024 * 1024);
        }
        if (sizeStr.EndsWith("MiB", StringComparison.OrdinalIgnoreCase) || sizeStr.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            var num = sizeStr.EndsWith("MiB", StringComparison.OrdinalIgnoreCase) ? sizeStr[..^3] : sizeStr[..^2];
            if (double.TryParse(num, System.Globalization.CultureInfo.InvariantCulture, out var val))
                return (long)(val * 1024 * 1024);
        }
        if (sizeStr.EndsWith("KiB", StringComparison.OrdinalIgnoreCase) || sizeStr.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            var num = sizeStr.EndsWith("KiB", StringComparison.OrdinalIgnoreCase) ? sizeStr[..^3] : sizeStr[..^2];
            if (double.TryParse(num, System.Globalization.CultureInfo.InvariantCulture, out var val))
                return (long)(val * 1024);
        }
        if (sizeStr.EndsWith("B", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(sizeStr[..^1], System.Globalization.CultureInfo.InvariantCulture, out var val))
                return (long)val;
        }
        return 0;
    }

    private static TimeSpan? ParseEta(string etaStr)
    {
        if (TimeSpan.TryParse(etaStr, out var ts)) return ts;
        var parts = etaStr.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var s))
            return new TimeSpan(0, m, s);
        if (parts.Length == 3 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var min) && int.TryParse(parts[2], out var sec))
            return new TimeSpan(h, min, sec);
        return null;
    }

    /// <summary>
    /// Sistemde yüklü olan Node.js binary'sinin yolunu tespit eder.
    /// </summary>
    private static string? DetectNodeJsPath()
    {
        foreach (var candidate in NodePathCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var path in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var fullPath = Path.Combine(path.Trim(), NodeExecutableName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Kısıtlı videoların (YouTube Kids / Music vb.) indirilmesini sağlayan ortak yt-dlp argümanlarını üretir.
    /// </summary>
    public static string GetCommonArguments()
    {
        var nodePath = DetectNodeJsPath();
        var jsRuntimesArg = !string.IsNullOrEmpty(nodePath)
            ? $"--js-runtimes node:\"{nodePath}\" --js-runtimes quickjs --js-runtimes bun --js-runtimes deno"
            : "--js-runtimes node --js-runtimes quickjs --js-runtimes bun --js-runtimes deno";

        return $"{jsRuntimesArg} {DefaultExtractorArgs}";
    }

    public YtDlpMediaService(
        DownloadEventBus eventBus,
        ILogger<YtDlpMediaService> logger,
        HttpClient httpClient,
        string? toolsDirectory = null)
    {
        _eventBus = eventBus;
        _logger = logger;
        _httpClient = httpClient;
        _toolsDir = toolsDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp");
        _ytDlpPath = Path.Combine(_toolsDir, YtDlpExecutableName);
    }

    /// <summary>
    /// yt-dlp binary'sinin mevcut olup olmadığını kontrol eder.
    /// </summary>
    public bool IsAvailable()
    {
        if (File.Exists(_ytDlpPath)) return true;

        // PATH'te var mı kontrol et
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// yt-dlp binary'sini otomatik olarak resmi kaynaktan indirir.
    /// </summary>
    public async Task<bool> EnsureYtDlpAsync(CancellationToken cancellationToken = default)
    {
        if (IsAvailable()) return true;

        try
        {
            Directory.CreateDirectory(_toolsDir);

            // Proje kök dizinindeki tools/yt-dlp kontrolü
            var rootToolsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools", "yt-dlp", YtDlpExecutableName));
            if (File.Exists(rootToolsPath))
            {
                File.Copy(rootToolsPath, _ytDlpPath, true);
                return true;
            }

            _logger.LogInformation("yt-dlp bulunamadı, indiriliyor: {Path}", _ytDlpPath);

            using var response = await _httpClient.GetAsync(YtDlpDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(_ytDlpPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);

            _logger.LogInformation("yt-dlp başarıyla indirildi.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp indirilemedi.");
            return false;
        }
    }

    /// <summary>
    /// ffmpeg ve ffprobe binary'lerinin mevcut olup olmadığını kontrol eder ve gerekirse ayarlar.
    /// </summary>
    public Task<bool> EnsureFfmpegAsync(CancellationToken cancellationToken = default)
    {
        var ffmpegPath = Path.Combine(_toolsDir, FfmpegExecutableName);
        if (File.Exists(ffmpegPath)) return Task.FromResult(true);

        try
        {
            Directory.CreateDirectory(_toolsDir);

            // Proje kök dizinindeki tools/ffmpeg kontrolü
            var rootToolsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools", "ffmpeg", FfmpegExecutableName));
            if (File.Exists(rootToolsPath))
            {
                File.Copy(rootToolsPath, ffmpegPath, true);
                var rootProbePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools", "ffmpeg", FfprobeExecutableName));
                if (File.Exists(rootProbePath))
                {
                    File.Copy(rootProbePath, Path.Combine(_toolsDir, FfprobeExecutableName), true);
                }
                return Task.FromResult(true);
            }

            // WinGet önbelleğinden kopyala
            var wingetSearch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetSearch))
            {
                var files = Directory.GetFiles(wingetSearch, FfmpegExecutableName, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    File.Copy(files[0], ffmpegPath, true);
                    var probeSource = Path.Combine(Path.GetDirectoryName(files[0])!, FfprobeExecutableName);
                    if (File.Exists(probeSource))
                    {
                        File.Copy(probeSource, Path.Combine(_toolsDir, FfprobeExecutableName), true);
                    }
                    _logger.LogInformation("ffmpeg WinGet dizininden kopyalandı: {Path}", ffmpegPath);
                    return Task.FromResult(true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpeg kopyalama hatası");
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Verilen medya URL'sinin (YouTube vb.) bilgilerini JSON olarak çeker.
    /// </summary>
    public async Task<MediaMetadata?> GetMediaInfoAsync(string url, CancellationToken cancellationToken = default)
    {
        await EnsureYtDlpAsync(cancellationToken);
        await EnsureFfmpegAsync(cancellationToken);
        var binary = File.Exists(_ytDlpPath) ? _ytDlpPath : "yt-dlp";
        var commonArgs = GetCommonArguments();

        var psi = new ProcessStartInfo
        {
            FileName = binary,
            Arguments = $"{commonArgs} --dump-single-json --no-warnings --no-playlist \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var outputTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);

            await proc.WaitForExitAsync(cancellationToken);

            var jsonOutput = await outputTask;
            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(jsonOutput))
            {
                var error = await errorTask;
                _logger.LogWarning("yt-dlp meta verisi alamadı: {Error}", error);
                return null;
            }

            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            var rawTitle = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            var uploader = root.TryGetProperty("uploader", out var upProp) ? upProp.GetString() ?? "" : "";
            var channel = root.TryGetProperty("channel", out var chanProp) ? chanProp.GetString() ?? "" : "";
            var artist = root.TryGetProperty("artist", out var artProp) ? artProp.GetString() ?? "" : "";
            var author = !string.IsNullOrWhiteSpace(artist) ? artist : (!string.IsNullOrWhiteSpace(uploader) ? uploader : channel);
            var title = FormatMediaTitle(author, rawTitle);

            var thumbnail = root.TryGetProperty("thumbnail", out var thumbProp) ? thumbProp.GetString() ?? "" : "";
            var duration = root.TryGetProperty("duration", out var durProp) ? TimeSpan.FromSeconds(durProp.GetDouble()) : TimeSpan.Zero;

            bool isPlaylist = url.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
                              url.Contains("/playlist", StringComparison.OrdinalIgnoreCase);

            return new MediaMetadata
            {
                Url = url,
                Title = title,
                Uploader = author,
                ThumbnailUrl = thumbnail,
                Duration = duration,
                IsPlaylist = isPlaylist
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMediaInfoAsync başarısız: {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Medya başlığına sanatçı/kanal adını (eğer başlıkta yer almıyorsa) "Sanatçı - Başlık" formatında ekler.
    /// </summary>
    public static string FormatMediaTitle(string? author, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return author ?? "Medya";
        if (string.IsNullOrWhiteSpace(author)) return title;

        var cleanAuthor = author.Trim();
        if (cleanAuthor.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase))
        {
            cleanAuthor = cleanAuthor[..^8].Trim();
        }

        if (string.IsNullOrWhiteSpace(cleanAuthor)) return title;

        // Başlıkta zaten sanatçı adı varsa tekrar ekleme
        if (title.StartsWith(cleanAuthor, StringComparison.OrdinalIgnoreCase) ||
            title.Contains($" {cleanAuthor} ", StringComparison.OrdinalIgnoreCase) ||
            title.Contains($"{cleanAuthor} -", StringComparison.OrdinalIgnoreCase) ||
            title.Contains($"- {cleanAuthor}", StringComparison.OrdinalIgnoreCase) ||
            title.Contains($"| {cleanAuthor}", StringComparison.OrdinalIgnoreCase) ||
            title.Contains($"{cleanAuthor} |", StringComparison.OrdinalIgnoreCase))
        {
            return title;
        }

        return $"{cleanAuthor} - {title}";
    }

    /// <summary>
    /// Medyayı seçilen formata (MP4 veya MP3) ve kapsama (Tek video veya Playlist) göre indirir.
    /// </summary>
    public async Task DownloadMediaAsync(
        long taskId,
        string url,
        string outputDirectory,
        bool isAudioOnlyMp3,
        bool isPlaylist = false,
        string? customTitle = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureYtDlpAsync(cancellationToken);
        await EnsureFfmpegAsync(cancellationToken);
        var binary = File.Exists(_ytDlpPath) ? _ytDlpPath : "yt-dlp";
        Directory.CreateDirectory(outputDirectory);

        _eventBus.Publish(new TaskStartedEvent(taskId));

        // Format ve argüman belirleme
        var commonArgs = GetCommonArguments();
        string outputTemplate;
        if (!isPlaylist && !string.IsNullOrWhiteSpace(customTitle))
        {
            var sanitizedTitle = FDownloader.Core.Utilities.FileNameSanitizer.Sanitize(customTitle);
            outputTemplate = Path.Combine(outputDirectory, $"{sanitizedTitle}.%(ext)s");
        }
        else
        {
            outputTemplate = Path.Combine(outputDirectory, "%(uploader)s - %(title)s.%(ext)s");
        }

        string args;
        string playlistFlag = isPlaylist ? "--yes-playlist" : "--no-playlist";
        string ffmpegFlag = Directory.Exists(_toolsDir) ? $"--ffmpeg-location \"{_toolsDir}\"" : "";

        if (isAudioOnlyMp3)
        {
            // MP3 Ses dönüştürme
            args = $"{commonArgs} {playlistFlag} {ffmpegFlag} {DefaultAudioFormat} --newline -o \"{outputTemplate}\" \"{url}\"";
        }
        else
        {
            // MP4 Video (en iyi kalite)
            args = $"{commonArgs} {playlistFlag} {ffmpegFlag} {DefaultVideoFormat} --newline -o \"{outputTemplate}\" \"{url}\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = binary,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var proc = new Process { StartInfo = psi };
        _runningProcesses[taskId] = proc;
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch { }
        });

        try
        {
            proc.Start();

            var stopwatch = Stopwatch.StartNew();

            string? downloadedFilePath = null;

            // Canlı çıktı dinleme
            while (!proc.StandardOutput.EndOfStream)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                    _eventBus.Publish(new TaskCancelledEvent(taskId));
                    return;
                }

                var line = await proc.StandardOutput.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                // İlerleme satırlarını ayrıştır
                var match = ProgressRegex.Match(line);
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var percent))
                    {
                        double speedBps = match.Groups.Count > 3 ? ParseSpeed(match.Groups[3].Value) : 0;
                        long totalBytes = match.Groups.Count > 2 ? ParseBytes(match.Groups[2].Value) : 0;
                        long downloadedBytes = totalBytes > 0 ? (long)(totalBytes * (percent / 100.0)) : 0;
                        TimeSpan? eta = match.Groups.Count > 4 ? ParseEta(match.Groups[4].Value) : null;

                        _eventBus.Publish(new ProgressUpdatedEvent(
                            taskId,
                            downloadedBytes,
                            totalBytes,
                            percent,
                            speedBps,
                            eta,
                            Array.Empty<SegmentProgressInfo>()
                        ));
                    }
                }
                else if (line.StartsWith("[download] Destination:", StringComparison.OrdinalIgnoreCase))
                {
                    downloadedFilePath = line.Substring("[download] Destination:".Length).Trim().Trim('\"');
                }
                else if (line.StartsWith("[ExtractAudio] Destination:", StringComparison.OrdinalIgnoreCase))
                {
                    downloadedFilePath = line.Substring("[ExtractAudio] Destination:".Length).Trim().Trim('\"');
                }
                else if (line.Contains("Merging formats into", StringComparison.OrdinalIgnoreCase))
                {
                    const string prefix = "Merging formats into";
                    var pIdx = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                    if (pIdx >= 0)
                    {
                        downloadedFilePath = line.Substring(pIdx + prefix.Length).Trim().Trim('\"');
                    }
                }
            }

            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode == 0)
            {
                stopwatch.Stop();
                long finalBytes = 0;
                if (!string.IsNullOrEmpty(downloadedFilePath) && File.Exists(downloadedFilePath))
                {
                    finalBytes = new FileInfo(downloadedFilePath).Length;
                }
                else if (!string.IsNullOrEmpty(outputDirectory) && Directory.Exists(outputDirectory))
                {
                    var candidate = new DirectoryInfo(outputDirectory)
                        .GetFiles()
                        .Where(f => !f.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !f.Name.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();

                    if (candidate != null && (DateTime.UtcNow - candidate.LastWriteTimeUtc).TotalMinutes < 5)
                    {
                        downloadedFilePath = candidate.FullName;
                        finalBytes = candidate.Length;
                    }
                }

                _eventBus.Publish(new TaskCompletedEvent(
                    taskId,
                    downloadedFilePath ?? outputDirectory,
                    finalBytes,
                    stopwatch.Elapsed
                ));
            }
            else
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _eventBus.Publish(new TaskCancelledEvent(taskId));
                    return;
                }
                var err = await proc.StandardError.ReadToEndAsync(cancellationToken);
                _eventBus.Publish(new TaskFailedEvent(taskId, $"yt-dlp hata kodu: {proc.ExitCode}. {err}", false));
            }
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            _eventBus.Publish(new TaskCancelledEvent(taskId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Medya indirme sırasında hata alındı (#{TaskId})", taskId);
            _eventBus.Publish(new TaskFailedEvent(taskId, ex.Message, false));
        }
        finally
        {
            _runningProcesses.TryRemove(taskId, out _);
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch { }
            try { proc.Dispose(); } catch { }
        }
    }

    public static bool IsMediaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("tiktok.com", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("twitter.com", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("x.com", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase);
    }
}

public class MediaMetadata
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Uploader { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool IsPlaylist { get; set; }
}
