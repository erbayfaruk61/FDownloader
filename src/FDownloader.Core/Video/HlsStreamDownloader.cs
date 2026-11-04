using System.Diagnostics;
using System.Text;
using FDownloader.Core.Engine.Exceptions;
using FDownloader.Core.Events;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Video;

public class HlsPlaylistInfo
{
    public string PlaylistUrl { get; set; } = string.Empty;
    public List<string> SegmentUrls { get; set; } = new();
    public double TotalDurationSeconds { get; set; }
    public bool IsMasterPlaylist { get; set; }
    public bool IsDrmProtected { get; set; }
}

public sealed class HlsStreamDownloader
{
    private readonly HttpClient _httpClient;
    private readonly DownloadEventBus _eventBus;
    private readonly ILogger<HlsStreamDownloader> _logger;
    private readonly string? _ffmpegPath;

    public HlsStreamDownloader(
        HttpClient httpClient,
        DownloadEventBus eventBus,
        ILogger<HlsStreamDownloader> logger,
        string? ffmpegPath = null)
    {
        _httpClient = httpClient;
        _eventBus = eventBus;
        _logger = logger;
        _ffmpegPath = ffmpegPath;
    }

    public static bool IsHlsUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var clean = url.Split('?')[0].Split('#')[0];
        return clean.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<HlsPlaylistInfo> ParsePlaylistAsync(string playlistUrl, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(playlistUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var baseUri = new Uri(playlistUrl);

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // DRM kontrolü
        foreach (var line in lines)
        {
            if (line.StartsWith("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase))
            {
                if (line.Contains("SAMPLE-AES", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("KEYFORMAT=\"com.widevine", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("KEYFORMAT=\"com.microsoft.playready", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DownloadException(
                        DownloadErrorKind.DrmProtected,
                        "Bu HLS akışı DRM (Dijital Hak Yönetimi) ile şifrelenmiştir ve indirilemez.");
                }
            }
        }

        // Master Playlist mi? (EXT-X-STREAM-INF)
        var streamInfIndex = Array.FindIndex(lines, l => l.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase));
        if (streamInfIndex >= 0)
        {
            // En yüksek bant genişliği veya ilk alt playlist URL'sini al
            string? childUrl = null;
            for (int i = streamInfIndex + 1; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmed))
                {
                    childUrl = new Uri(baseUri, trimmed).ToString();
                    break;
                }
            }

            if (!string.IsNullOrEmpty(childUrl))
            {
                var childPlaylist = await ParsePlaylistAsync(childUrl, cancellationToken);
                childPlaylist.IsMasterPlaylist = true;
                return childPlaylist;
            }
        }

        // Media Playlist parçalarını ayıkla
        var segmentUrls = new List<string>();
        double totalDuration = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var durStr = line.Substring("#EXTINF:".Length).Split(',')[0].Trim();
                if (double.TryParse(durStr, System.Globalization.CultureInfo.InvariantCulture, out var dur))
                {
                    totalDuration += dur;
                }

                // Parça URL'si bir sonraki yorum olmayan satırdadır
                if (i + 1 < lines.Length)
                {
                    var nextLine = lines[i + 1].Trim();
                    if (!nextLine.StartsWith("#") && !string.IsNullOrWhiteSpace(nextLine))
                    {
                        var fullSegmentUrl = new Uri(baseUri, nextLine).ToString();
                        segmentUrls.Add(fullSegmentUrl);
                    }
                }
            }
            else if (!line.StartsWith("#") && (line.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) || line.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase)))
            {
                var fullSegmentUrl = new Uri(baseUri, line).ToString();
                if (!segmentUrls.Contains(fullSegmentUrl))
                {
                    segmentUrls.Add(fullSegmentUrl);
                }
            }
        }

        return new HlsPlaylistInfo
        {
            PlaylistUrl = playlistUrl,
            SegmentUrls = segmentUrls,
            TotalDurationSeconds = totalDuration,
            IsMasterPlaylist = false
        };
    }

    public async Task<string> DownloadHlsAsync(
        long taskId,
        string playlistUrl,
        string outputFilePath,
        int maxConcurrency = 4,
        CancellationToken cancellationToken = default)
    {
        _eventBus.Publish(new TaskStartedEvent(taskId));

        var stopwatch = Stopwatch.StartNew();
        var playlist = await ParsePlaylistAsync(playlistUrl, cancellationToken);

        if (playlist.SegmentUrls.Count == 0)
        {
            throw new DownloadException(DownloadErrorKind.PermanentNotFound, "HLS akışında indirilecek medya parçası bulunamadı.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"FDownloader_HLS_{taskId}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            int totalSegments = playlist.SegmentUrls.Count;
            int completedSegments = 0;
            long totalDownloadedBytes = 0;

            var segmentFiles = new string[totalSegments];
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var tasks = playlist.SegmentUrls.Select(async (segUrl, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var segPath = Path.Combine(tempDir, $"segment_{index:D6}.ts");
                    using var res = await _httpClient.GetAsync(segUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    res.EnsureSuccessStatusCode();

                    await using (var fs = new FileStream(segPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await res.Content.CopyToAsync(fs, cancellationToken);
                    }

                    segmentFiles[index] = segPath;
                    var segBytes = new FileInfo(segPath).Length;
                    Interlocked.Add(ref totalDownloadedBytes, segBytes);
                    var currentCompleted = Interlocked.Increment(ref completedSegments);

                    double percent = (double)currentCompleted / totalSegments * 100.0;
                    double speedBps = stopwatch.Elapsed.TotalSeconds > 0
                        ? totalDownloadedBytes / stopwatch.Elapsed.TotalSeconds
                        : 0;

                    _eventBus.Publish(new ProgressUpdatedEvent(
                        taskId,
                        totalDownloadedBytes,
                        totalDownloadedBytes, // HLS için toplam boyut dinamik
                        percent,
                        speedBps,
                        null,
                        Array.Empty<SegmentProgressInfo>()
                    ));
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // ─── Sıralı Birleştirme (MPEG-TS Concat) ───────────────────────────
            var outDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            var finalExt = Path.GetExtension(outputFilePath).ToLowerInvariant();
            var shouldConvertToMp4 = (finalExt == ".mp4") && CanUseFfmpeg();

            if (shouldConvertToMp4)
            {
                // ffmpeg ile kayıpsız MP4 konteynerine aktar (-c copy)
                await MergeToMp4WithFfmpegAsync(segmentFiles, outputFilePath, tempDir, cancellationToken);
            }
            else
            {
                // Doğrudan TS parça birleştirme
                await using var outStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                byte[] buffer = new byte[65536];
                foreach (var segFile in segmentFiles)
                {
                    if (File.Exists(segFile))
                    {
                        await using var inStream = new FileStream(segFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        int read;
                        while ((read = await inStream.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            await outStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        }
                    }
                }
            }

            stopwatch.Stop();
            var finalLength = File.Exists(outputFilePath) ? new FileInfo(outputFilePath).Length : totalDownloadedBytes;

            _eventBus.Publish(new TaskCompletedEvent(
                taskId,
                outputFilePath,
                finalLength,
                stopwatch.Elapsed
            ));

            return outputFilePath;
        }
        catch (OperationCanceledException)
        {
            _eventBus.Publish(new TaskCancelledEvent(taskId));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HLS akış indirme başarısız (#{TaskId})", taskId);
            _eventBus.Publish(new TaskFailedEvent(taskId, ex.Message, false));
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch { }
        }
    }

    private bool CanUseFfmpeg()
    {
        if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath)) return true;
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(2000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task MergeToMp4WithFfmpegAsync(
        string[] segmentFiles,
        string outputFilePath,
        string tempDir,
        CancellationToken cancellationToken)
    {
        var listFile = Path.Combine(tempDir, "segments.txt");
        var sb = new StringBuilder();
        foreach (var seg in segmentFiles)
        {
            sb.AppendLine($"file '{seg.Replace("\\", "/")}'");
        }
        await File.WriteAllTextAsync(listFile, sb.ToString(), Encoding.UTF8, cancellationToken);

        var ffmpegExe = (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath)) ? _ffmpegPath : "ffmpeg";
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            ArgumentList =
            {
                "-f", "concat",
                "-safe", "0",
                "-i", listFile,
                "-c", "copy",
                "-y",
                outputFilePath
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        await proc.WaitForExitAsync(cancellationToken);

        if (proc.ExitCode != 0 || !File.Exists(outputFilePath))
        {
            var err = await proc.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogWarning("ffmpeg ile MP4 dönüştürme başarısız ({Code}): {Error}, doğrudan ikili birleştirme uygulanıyor.", proc.ExitCode, err);

            // Geri dönüş: Doğrudan ikili birleştirme
            await using var outStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[65536];
            foreach (var segFile in segmentFiles)
            {
                if (File.Exists(segFile))
                {
                    await using var inStream = new FileStream(segFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    int read;
                    while ((read = await inStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await outStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }
            }
        }
    }
}
