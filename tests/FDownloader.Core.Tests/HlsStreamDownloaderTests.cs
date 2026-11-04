using FDownloader.Core.Engine.Exceptions;
using FDownloader.Core.Events;
using FDownloader.Core.Tests.MockServer;
using FDownloader.Core.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class HlsStreamDownloaderTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly HttpClient _httpClient;
    private readonly DownloadEventBus _eventBus;
    private readonly HlsStreamDownloader _downloader;
    private readonly string _tempDir;

    public HlsStreamDownloaderTests()
    {
        _server = new TestHttpServer();
        _server.Start();
        _httpClient = new HttpClient();
        _eventBus = new DownloadEventBus();
        _downloader = new HlsStreamDownloader(_httpClient, _eventBus, NullLogger<HlsStreamDownloader>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"FDownloader_HlsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _server.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Theory]
    [InlineData("https://example.com/stream.m3u8", true)]
    [InlineData("https://example.com/playlist.m3u8?token=xyz123", true)]
    [InlineData("https://example.com/live/master.m3u8#track1", true)]
    [InlineData("https://example.com/video.mp4", false)]
    [InlineData("https://example.com/archive.zip", false)]
    [InlineData("", false)]
    public void IsHlsUrl_IdentifiesM3u8Extension(string url, bool expectedIsHls)
    {
        var result = HlsStreamDownloader.IsHlsUrl(url);
        Assert.Equal(expectedIsHls, result);
    }

    [Fact]
    public async Task ParsePlaylistAsync_MediaPlaylist_ParsesSegmentsAndDurationCorrectly()
    {
        var url = $"{_server.BaseUrl}hls/stream.m3u8";
        var info = await _downloader.ParsePlaylistAsync(url);

        Assert.NotNull(info);
        Assert.Equal(3, info.SegmentUrls.Count);
        Assert.Equal(27.0, info.TotalDurationSeconds);
        Assert.False(info.IsMasterPlaylist);
        Assert.EndsWith("/hls/seg0.ts", info.SegmentUrls[0]);
        Assert.EndsWith("/hls/seg1.ts", info.SegmentUrls[1]);
        Assert.EndsWith("/hls/seg2.ts", info.SegmentUrls[2]);
    }

    [Fact]
    public async Task ParsePlaylistAsync_MasterPlaylist_FollowsChildStreamAutomatically()
    {
        var url = $"{_server.BaseUrl}hls/master.m3u8";
        var info = await _downloader.ParsePlaylistAsync(url);

        Assert.NotNull(info);
        Assert.True(info.IsMasterPlaylist);
        Assert.Equal(3, info.SegmentUrls.Count);
        Assert.EndsWith("/hls/seg0.ts", info.SegmentUrls[0]);
    }

    [Fact]
    public async Task ParsePlaylistAsync_DrmProtectedStream_ThrowsDrmProtectedDownloadException()
    {
        var url = $"{_server.BaseUrl}hls/drm.m3u8";

        var ex = await Assert.ThrowsAsync<DownloadException>(() => _downloader.ParsePlaylistAsync(url));

        Assert.Equal(DownloadErrorKind.DrmProtected, ex.Kind);
        Assert.Contains("DRM", ex.Message);
    }

    [Fact]
    public async Task DownloadHlsAsync_DownloadsSegmentsInParallelAndMergesSequentially()
    {
        var url = $"{_server.BaseUrl}hls/stream.m3u8";
        var outputFile = Path.Combine(_tempDir, "merged_video.ts");

        var completedEvents = new List<TaskCompletedEvent>();
        var progressEvents = new List<ProgressUpdatedEvent>();
        _eventBus.Subscribe<TaskCompletedEvent>(e => completedEvents.Add(e));
        _eventBus.Subscribe<ProgressUpdatedEvent>(e => progressEvents.Add(e));

        var resultPath = await _downloader.DownloadHlsAsync(
            taskId: 42,
            playlistUrl: url,
            outputFilePath: outputFile,
            maxConcurrency: 2);

        Assert.Equal(outputFile, resultPath);
        Assert.True(File.Exists(outputFile));

        var mergedBytes = await File.ReadAllBytesAsync(outputFile);
        Assert.Equal(TestHttpServer.HlsExpectedCombined, mergedBytes);

        Assert.Single(completedEvents);
        Assert.Equal(42, completedEvents[0].TaskId);
        Assert.Equal(TestHttpServer.HlsExpectedCombined.Length, completedEvents[0].TotalBytes);
        Assert.NotEmpty(progressEvents);
    }

    [Fact]
    public async Task DownloadHlsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var url = $"{_server.BaseUrl}hls/stream.m3u8";
        var outputFile = Path.Combine(_tempDir, "cancelled_video.ts");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // İptal edilmiş token

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _downloader.DownloadHlsAsync(taskId: 99, playlistUrl: url, outputFilePath: outputFile, cancellationToken: cts.Token));
    }
}
