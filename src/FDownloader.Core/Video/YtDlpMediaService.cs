using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine.Exceptions;
using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Video;

/// <summary>
/// YouTube, Twitter, Instagram, TikTok vb. platformlardan video/ses indirmek için yt-dlp sarmalayıcısı.
/// Video meta verisini alır, MP4/MP3 format seçimini destekler ve canlı ilerleme yayını yapar.
/// </summary>
public sealed class YtDlpMediaService : IDisposable
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
                    proc.WaitForExit(3000);
                }
            }
            catch { }
            finally
            {
                try { proc.Dispose(); } catch { }
            }
        }
    }

    /// <summary>
    /// Servis sonlandırıldığında veya uygulama kapatıldığında çalışan tüm alt süreçleri (yt-dlp ve ffmpeg) temizler.
    /// </summary>
    public void Dispose()
    {
        foreach (var kvp in _runningProcesses)
        {
            try
            {
                if (!kvp.Value.HasExited)
                {
                    kvp.Value.Kill(entireProcessTree: true);
                    kvp.Value.WaitForExit(2000);
                }
            }
            catch { }
            finally
            {
                try { kvp.Value.Dispose(); } catch { }
            }
        }
        _runningProcesses.Clear();
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

    /// <summary>
    /// ProcessStartInfo.ArgumentList ile kullanılacak güvenli ortak argüman listesini üretir (komut enjeksiyonunu önler).
    /// </summary>
    public static List<string> BuildCommonArgumentList()
    {
        var list = new List<string>();
        var nodePath = DetectNodeJsPath();
        if (!string.IsNullOrEmpty(nodePath))
        {
            list.Add("--js-runtimes");
            list.Add($"node:{nodePath}");
        }
        else
        {
            list.Add("--js-runtimes");
            list.Add("node");
        }

        list.Add("--js-runtimes");
        list.Add("quickjs");
        list.Add("--js-runtimes");
        list.Add("bun");
        list.Add("--js-runtimes");
        list.Add("deno");

        list.Add("--extractor-args");
        list.Add("youtube:player_client=web,default,android");

        return list;
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
    /// yt-dlp hata çıktısını analiz ederek kullanıcı dostu Türkçe hata mesajına dönüştürür.
    /// </summary>
    public static string FormatTurkishErrorMessage(string? rawError, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return $"Medya indirme işlemi başarısız oldu (Hata kodu: {exitCode}).";

        var err = rawError.Trim();

        if (DownloadErrorClassifier.IsDrmProtectedMessage(err))
            return "Bu içerik DRM (Dijital Hak Yönetimi) ile korunmaktadır ve indirilemez.";

        if (err.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("403: Forbidden", StringComparison.OrdinalIgnoreCase))
            return "Sunucu erişimi engelledi (HTTP 403). Bağlantının süresi dolmuş veya oturum/çerez gerekiyor olabilir.";

        if (err.Contains("HTTP Error 404", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("404: Not Found", StringComparison.OrdinalIgnoreCase))
            return "İstenen medya dosyası sunucuda bulunamadı (HTTP 404).";

        if (err.Contains("Sign in to confirm your age", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("confirm your age", StringComparison.OrdinalIgnoreCase))
            return "Bu video yaş kısıtlamalıdır ve indirilmesi için oturum açılması gerekmektedir.";

        if (err.Contains("Sign in to confirm you’re not a bot", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("bot confirmation", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("bot detection", StringComparison.OrdinalIgnoreCase))
            return "Sunucu bot doğrulaması talep etti. Lütfen tarayıcıdan giriş yapıp çerezleri aktararak deneyin.";

        if (err.Contains("Video unavailable", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("Private video", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("This video is private", StringComparison.OrdinalIgnoreCase))
            return "Video yayından kaldırılmış, silinmiş veya gizli olarak ayarlanmış.";

        if (err.Contains("not available in your country", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("The uploader has not made this video available in your country", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("geo-restricted", StringComparison.OrdinalIgnoreCase))
            return "Bu içerik bulunduğunuz coğrafi bölgede/ülkede kullanıma kapalıdır.";

        if (err.Contains("ffmpeg not found", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("ffprobe not found", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("ffmpeg is not installed", StringComparison.OrdinalIgnoreCase))
            return "ffmpeg aracı bulunamadı; ses ve video akışları birleştirilemiyor.";

        if (err.Contains("No space left on device", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("disk full", StringComparison.OrdinalIgnoreCase))
            return "Yetersiz disk alanı. İndirme sırasında disk doldu.";

        if (err.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase))
            return "Desteklenmeyen medya URL'si veya bu siteden video indirme desteklenmiyor.";

        var errorLines = err.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var firstErrorLine = errorLines.FirstOrDefault(l => l.Contains("ERROR:", StringComparison.OrdinalIgnoreCase)) ?? errorLines.FirstOrDefault() ?? err;

        return $"Medya indirme hatası: {firstErrorLine.Trim()}";
    }

    /// <summary>
    /// Kurulu olan yt-dlp binary'sinin sürüm numarasını sorgular (yt-dlp --version).
    /// </summary>
    public async Task<string?> GetInstalledVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable()) return null;

        var binary = File.Exists(_ytDlpPath) ? _ytDlpPath : "yt-dlp";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binary,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "yt-dlp sürüm bilgisi alınamadı");
        }

        return null;
    }

    /// <summary>
    /// yt-dlp binary'sini en son resmi kararlı sürüme günceller.
    /// Önce 'yt-dlp -U' komutunu dener; başarısız olursa GitHub releases üzerinden doğrudan indirip atomik yeniler.
    /// </summary>
    public async Task<YtDlpUpdateResult> UpdateYtDlpAsync(CancellationToken cancellationToken = default)
    {
        var oldVersion = await GetInstalledVersionAsync(cancellationToken);

        // 1. Önce yt-dlp -U komutunu dene (mevcut binary çalışabiliyorsa)
        if (IsAvailable())
        {
            var binary = File.Exists(_ytDlpPath) ? _ytDlpPath : "yt-dlp";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = binary,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-U");

                using var proc = new Process { StartInfo = psi };
                proc.Start();
                var outputTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = proc.StandardError.ReadToEndAsync(cancellationToken);
                await proc.WaitForExitAsync(cancellationToken);

                var output = await outputTask;
                var newVersion = await GetInstalledVersionAsync(cancellationToken);

                if (proc.ExitCode == 0)
                {
                    _logger.LogInformation("yt-dlp başarıyla güncellendi (-U): {Output}", output);
                    return new YtDlpUpdateResult
                    {
                        Success = true,
                        OldVersion = oldVersion,
                        NewVersion = newVersion ?? oldVersion,
                        Message = string.IsNullOrWhiteSpace(output) ? "yt-dlp başarıyla güncellendi." : output.Trim()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "yt-dlp -U çalıştırma hatası, doğrudan GitHub indirmesi deneniyor...");
            }
        }

        // 2. yt-dlp -U başarısız olduysa veya binary mevcut değilse doğrudan GitHub'dan indir
        try
        {
            Directory.CreateDirectory(_toolsDir);
            var tempDownloadPath = _ytDlpPath + ".tmp";

            using var response = await _httpClient.GetAsync(YtDlpDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var fs = new FileStream(tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fs, cancellationToken);
            }

            var fi = new FileInfo(tempDownloadPath);
            if (fi.Length < 1_000_000)
            {
                try { File.Delete(tempDownloadPath); } catch { }
                return new YtDlpUpdateResult
                {
                    Success = false,
                    OldVersion = oldVersion,
                    Message = "İndirilen yt-dlp binary boyutu geçersiz (1 MB altı)."
                };
            }

            File.Move(tempDownloadPath, _ytDlpPath, overwrite: true);

            var updatedVersion = await GetInstalledVersionAsync(cancellationToken);
            return new YtDlpUpdateResult
            {
                Success = true,
                OldVersion = oldVersion,
                NewVersion = updatedVersion,
                Message = "yt-dlp en güncel sürüme başarıyla güncellendi."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp doğrudan indirme hatası");
            return new YtDlpUpdateResult
            {
                Success = false,
                OldVersion = oldVersion,
                Message = $"yt-dlp güncellenemedi: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// URL'nin yalnızca izin verilen şemalardan (http, https, ftp) birini kullandığını doğrular.
    /// Komut enjeksiyonu ve yerel dosya erişimini önler.
    /// </summary>
    private static bool IsUrlSchemeAllowed(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verilen medya URL'sinin (YouTube vb.) bilgilerini JSON olarak çeker.
    /// </summary>
    public async Task<MediaMetadata?> GetMediaInfoAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsUrlSchemeAllowed(url))
        {
            _logger.LogWarning("GetMediaInfoAsync: izin verilmeyen URL şeması: {Url}", url?[..Math.Min(url?.Length ?? 0, 100)] ?? "");
            return null;
        }

        await EnsureYtDlpAsync(cancellationToken);
        await EnsureFfmpegAsync(cancellationToken);
        var binary = File.Exists(_ytDlpPath) ? _ytDlpPath : "yt-dlp";
        var commonArgs = BuildCommonArgumentList();

        // ArgumentList kullanarak her argümanı ayrı iletir → shell enjeksiyonu imkânsız
        var psi = new ProcessStartInfo
        {
            FileName = binary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in commonArgs)
            psi.ArgumentList.Add(arg);

        psi.ArgumentList.Add("--dump-single-json");
        psi.ArgumentList.Add("--no-warnings");
        psi.ArgumentList.Add("--no-playlist");
        psi.ArgumentList.Add(url); // URL argüman listesinin son elemanı; shell yorumlaması yok

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

            var formatsList = new List<MediaFormatInfo>();
            if (root.TryGetProperty("formats", out var formatsArr) && formatsArr.ValueKind == JsonValueKind.Array)
            {
                var seenHeights = new HashSet<int>();
                foreach (var fmt in formatsArr.EnumerateArray())
                {
                    var formatId = fmt.TryGetProperty("format_id", out var fid) ? fid.GetString() ?? "" : "";
                    var ext = fmt.TryGetProperty("ext", out var fext) ? fext.GetString() ?? "" : "";
                    var vcodec = fmt.TryGetProperty("vcodec", out var fvc) ? fvc.GetString() : null;
                    var acodec = fmt.TryGetProperty("acodec", out var fac) ? fac.GetString() : null;
                    var height = fmt.TryGetProperty("height", out var fh) && fh.ValueKind == JsonValueKind.Number ? fh.GetInt32() : (int?)null;
                    var fps = fmt.TryGetProperty("fps", out var ffps) && ffps.ValueKind == JsonValueKind.Number ? ffps.GetInt32() : (int?)null;
                    var fileSize = fmt.TryGetProperty("filesize", out var fsz) && fsz.ValueKind == JsonValueKind.Number ? fsz.GetInt64() : (long?)null;

                    bool isAudioOnly = vcodec == "none" || (string.IsNullOrEmpty(vcodec) && !string.IsNullOrEmpty(acodec) && acodec != "none");

                    if (height.HasValue && height.Value > 0 && !isAudioOnly)
                    {
                        if (seenHeights.Add(height.Value))
                        {
                            formatsList.Add(new MediaFormatInfo
                            {
                                FormatId = formatId,
                                Extension = ext,
                                Height = height,
                                Resolution = $"{height}p",
                                Fps = fps,
                                FileSize = fileSize,
                                VCodec = vcodec,
                                ACodec = acodec,
                                IsAudioOnly = false
                            });
                        }
                    }
                }
            }

            return new MediaMetadata
            {
                Url = url,
                Title = title,
                Uploader = author,
                ThumbnailUrl = thumbnail,
                Duration = duration,
                IsPlaylist = isPlaylist,
                Formats = formatsList.OrderByDescending(f => f.Height ?? 0).ToList()
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
    public Task DownloadMediaAsync(
        long taskId,
        string url,
        string outputDirectory,
        bool isAudioOnlyMp3,
        bool isPlaylist = false,
        string? customTitle = null,
        CancellationToken cancellationToken = default)
    {
        return DownloadMediaAsync(taskId, url, outputDirectory, isAudioOnlyMp3, isPlaylist, customTitle, null, cancellationToken);
    }

    /// <summary>
    /// Medyayı seçilen özel format veya kalite belirteciyle indirir.
    /// </summary>
    public async Task DownloadMediaAsync(
        long taskId,
        string url,
        string outputDirectory,
        bool isAudioOnlyMp3,
        bool isPlaylist,
        string? customTitle,
        string? formatSpecifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsUrlSchemeAllowed(url))
        {
            _logger.LogWarning("DownloadMediaAsync: izin verilmeyen URL şeması: {Url}", url?[..Math.Min(url?.Length ?? 0, 100)] ?? "");
            _eventBus.Publish(new TaskFailedEvent(taskId, "Geçersiz veya güvenli olmayan medya URL'si.", false));
            return;
        }

        await EnsureYtDlpAsync(cancellationToken);
        await EnsureFfmpegAsync(cancellationToken);
        var binary = File.Exists(_ytDlpPath) ? _ytDlpPath : "yt-dlp";
        Directory.CreateDirectory(outputDirectory);

        _eventBus.Publish(new TaskStartedEvent(taskId));

        // Format ve argüman belirleme
        var commonArgs = BuildCommonArgumentList();
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

        var psi = new ProcessStartInfo
        {
            FileName = binary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in commonArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.ArgumentList.Add(isPlaylist ? "--yes-playlist" : "--no-playlist");

        if (Directory.Exists(_toolsDir))
        {
            psi.ArgumentList.Add("--ffmpeg-location");
            psi.ArgumentList.Add(_toolsDir);
        }

        if (!string.IsNullOrWhiteSpace(formatSpecifier))
        {
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(formatSpecifier);
            if (!isAudioOnlyMp3)
            {
                psi.ArgumentList.Add("--merge-output-format");
                psi.ArgumentList.Add("mp4");
            }
        }
        else if (isAudioOnlyMp3)
        {
            // MP3 Ses dönüştürme: -x --audio-format mp3 --audio-quality 0 --embed-metadata
            psi.ArgumentList.Add("-x");
            psi.ArgumentList.Add("--audio-format");
            psi.ArgumentList.Add("mp3");
            psi.ArgumentList.Add("--audio-quality");
            psi.ArgumentList.Add("0");
            psi.ArgumentList.Add("--embed-metadata");
        }
        else
        {
            // MP4 Video (en iyi kalite): -f "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best[ext=mp4]/best" --merge-output-format mp4
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best[ext=mp4]/best");
            psi.ArgumentList.Add("--merge-output-format");
            psi.ArgumentList.Add("mp4");
        }

        psi.ArgumentList.Add("--newline");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outputTemplate);
        psi.ArgumentList.Add(url);

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
                var userMessage = FormatTurkishErrorMessage(err, proc.ExitCode);
                _eventBus.Publish(new TaskFailedEvent(taskId, userMessage, false));
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
    public List<MediaFormatInfo> Formats { get; set; } = new();
}

public class MediaFormatInfo
{
    public string FormatId { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public int? Height { get; set; }
    public int? Fps { get; set; }
    public long? FileSize { get; set; }
    public string? VCodec { get; set; }
    public string? ACodec { get; set; }
    public bool IsAudioOnly { get; set; }
}

public class YtDlpUpdateResult
{
    public bool Success { get; set; }
    public string? OldVersion { get; set; }
    public string? NewVersion { get; set; }
    public string Message { get; set; } = string.Empty;
}
