using System.Net;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine.Exceptions;
using FDownloader.Core.Engine.Worker;
using FDownloader.Core.Engine.Writer;
using FDownloader.Core.Tests.MockServer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class ErrorClassificationTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly HttpClient _client;
    private readonly string _tempDir;

    public ErrorClassificationTests()
    {
        _server = new TestHttpServer();
        _server.Start();
        _client = new HttpClient();
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_ErrorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public void ClassifyResponse_429WithRetryAfter_ReturnsTooManyRequestsWithDelta()
    {
        using var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.Add("Retry-After", "12");

        var ex = DownloadErrorClassifier.ClassifyResponse(response);

        Assert.Equal(DownloadErrorKind.TooManyRequests, ex.Kind);
        Assert.NotNull(ex.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(12), ex.RetryAfter.Value);
    }

    [Fact]
    public void ClassifyResponse_404_ReturnsPermanentNotFound()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var ex = DownloadErrorClassifier.ClassifyResponse(response);

        Assert.Equal(DownloadErrorKind.PermanentNotFound, ex.Kind);
        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public void ClassifyResponse_403_ReturnsPermanentAuth()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        var ex = DownloadErrorClassifier.ClassifyResponse(response);

        Assert.Equal(DownloadErrorKind.PermanentAuth, ex.Kind);
        Assert.Contains("403", ex.Message);
    }

    [Fact]
    public void ClassifyResponse_500And503_ReturnsTransient()
    {
        using var res500 = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var ex500 = DownloadErrorClassifier.ClassifyResponse(res500);
        Assert.Equal(DownloadErrorKind.Transient, ex500.Kind);

        using var res503 = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var ex503 = DownloadErrorClassifier.ClassifyResponse(res503);
        Assert.Equal(DownloadErrorKind.Transient, ex503.Kind);
    }

    [Fact]
    public void ClassifyException_DiskFullIOException_ReturnsDiskFullOrPermission()
    {
        const int hrDiskFull = unchecked((int)0x80070070);
        var ioEx = new IOException("There is not enough space on the disk.", hrDiskFull);

        var ex = DownloadErrorClassifier.ClassifyException(ioEx);

        Assert.Equal(DownloadErrorKind.DiskFullOrPermission, ex.Kind);
        Assert.Contains("disk", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SegmentWorker_Permanent404_FailsImmediatelyWithoutRetryingMaxTimes()
    {
        var tempFile = Path.Combine(_tempDir, "test_404.tmp");
        var segment = new Segment
        {
            TaskId = 1,
            Index = 0,
            StartByte = 0,
            EndByte = 100,
            DownloadedBytes = 0
        };

        await using var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance);
        await writer.InitializeAsync(101, CancellationToken.None);

        var worker = new SegmentWorker(
            segment,
            $"{_server.BaseUrl}error-404",
            writer,
            _client,
            NullLogger<SegmentWorker>.Instance,
            maxRetries: 5);

        var ex = await Assert.ThrowsAsync<DownloadException>(() => worker.RunAsync(CancellationToken.None));

        Assert.Equal(DownloadErrorKind.PermanentNotFound, ex.Kind);
        Assert.Equal(0, segment.RetryCount); // Kalıcı hatada boşuna retry yapmamalı!
        Assert.Equal(SegmentStatus.Failed, segment.Status);
    }

    [Fact]
    public async Task SegmentWorker_Permanent403_FailsImmediatelyWithoutRetryingMaxTimes()
    {
        var tempFile = Path.Combine(_tempDir, "test_403.tmp");
        var segment = new Segment
        {
            TaskId = 2,
            Index = 0,
            StartByte = 0,
            EndByte = 100,
            DownloadedBytes = 0
        };

        await using var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance);
        await writer.InitializeAsync(101, CancellationToken.None);

        var worker = new SegmentWorker(
            segment,
            $"{_server.BaseUrl}error-403",
            writer,
            _client,
            NullLogger<SegmentWorker>.Instance,
            maxRetries: 5);

        var ex = await Assert.ThrowsAsync<DownloadException>(() => worker.RunAsync(CancellationToken.None));

        Assert.Equal(DownloadErrorKind.PermanentAuth, ex.Kind);
        Assert.Equal(0, segment.RetryCount);
        Assert.Equal(SegmentStatus.Failed, segment.Status);
    }

    [Fact]
    public async Task Scenario05_Abort_TransientErrorTriggersRetry()
    {
        var tempFile = Path.Combine(_tempDir, "test_abort.tmp");
        var segment = new Segment
        {
            TaskId = 3,
            Index = 0,
            StartByte = 0,
            EndByte = TestHttpServer.NormalContent.Length - 1,
            DownloadedBytes = 0
        };

        await using var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance);
        await writer.InitializeAsync(TestHttpServer.NormalContent.Length, CancellationToken.None);

        // Abort uç noktası bağlantıyı zorla keser; SegmentWorker bunu geçici hata görüp retry yapmalı
        var worker = new SegmentWorker(
            segment,
            $"{_server.BaseUrl}abort",
            writer,
            _client,
            NullLogger<SegmentWorker>.Instance,
            maxRetries: 1, // Hızlı test için 1 retry
            stallTimeout: TimeSpan.FromSeconds(2));

        var ex = await Assert.ThrowsAsync<DownloadException>(() => worker.RunAsync(CancellationToken.None));

        Assert.Equal(DownloadErrorKind.Transient, ex.Kind);
        Assert.True(segment.RetryCount >= 1); // En az 1 kez yeniden denemiş olmalı!
    }
}
