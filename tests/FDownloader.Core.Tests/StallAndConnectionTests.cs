using System.Security.Cryptography;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine.Exceptions;
using FDownloader.Core.Engine.Worker;
using FDownloader.Core.Engine.Writer;
using FDownloader.Core.Tests.MockServer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class StallAndConnectionTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly HttpClient _client;
    private readonly string _tempDir;

    public StallAndConnectionTests()
    {
        _server = new TestHttpServer();
        _server.Start();
        _client = new HttpClient();
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_StallTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task Scenario05_Abort_RecoversSeamlesslyAndMatchesHash()
    {
        // 1 kez abort etsin, 2. denemede Range ile kaldığı yerden devam etsin
        _server.AbortRemainingCount = 1;

        var tempFile = Path.Combine(_tempDir, "recover_abort.tmp");
        var segment = new Segment
        {
            TaskId = 100,
            Index = 0,
            StartByte = 0,
            EndByte = TestHttpServer.NormalContent.Length - 1,
            DownloadedBytes = 0
        };

        await using (var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance))
        {
            await writer.InitializeAsync(TestHttpServer.NormalContent.Length, CancellationToken.None);

            var worker = new SegmentWorker(
                segment,
                $"{_server.BaseUrl}abort",
                writer,
                _client,
                NullLogger<SegmentWorker>.Instance,
                maxRetries: 3,
                connectTimeout: TimeSpan.FromSeconds(5),
                stallTimeout: TimeSpan.FromSeconds(5));

            bool success = await worker.RunAsync(CancellationToken.None);
            Assert.True(success, "Worker kopan bağlantı sonrası toparlanıp indirmeyi tamamlamalı");
            Assert.Equal(SegmentStatus.Completed, segment.Status);
            Assert.Equal(1, segment.RetryCount); // Tam olarak 1 retry ile toparlanmış olmalı
        }

        // Dosya bütünlüğü SHA-256 doğrulaması
        var fileBytes = await File.ReadAllBytesAsync(tempFile);
        var actualHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        Assert.Equal(TestHttpServer.NormalContentSha256, actualHash);
    }

    [Fact]
    public async Task Scenario04_SlowEndpoint_CompletesWhenStallTimeoutIsGenerous()
    {
        var tempFile = Path.Combine(_tempDir, "slow_complete.tmp");
        var segment = new Segment
        {
            TaskId = 101,
            Index = 0,
            StartByte = 0,
            EndByte = TestHttpServer.NormalContent.Length - 1,
            DownloadedBytes = 0
        };

        await using (var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance))
        {
            await writer.InitializeAsync(TestHttpServer.NormalContent.Length, CancellationToken.None);

            // /slow endpoint'i 20ms'lik parçalarla gönderiyor; 3 saniyelik stall timeout ile başarıyla tamamlanmalı
            var worker = new SegmentWorker(
                segment,
                $"{_server.BaseUrl}slow",
                writer,
                _client,
                NullLogger<SegmentWorker>.Instance,
                maxRetries: 2,
                connectTimeout: TimeSpan.FromSeconds(5),
                stallTimeout: TimeSpan.FromSeconds(3));

            bool success = await worker.RunAsync(CancellationToken.None);
            Assert.True(success);
            Assert.Equal(SegmentStatus.Completed, segment.Status);
        }

        var fileBytes = await File.ReadAllBytesAsync(tempFile);
        var actualHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        Assert.Equal(TestHttpServer.NormalContentSha256, actualHash);
    }

    [Fact]
    public async Task StallTimeout_WhenStallDurationExceeded_TriggersRetry()
    {
        var tempFile = Path.Combine(_tempDir, "stall_retry.tmp");
        var segment = new Segment
        {
            TaskId = 102,
            Index = 0,
            StartByte = 0,
            EndByte = TestHttpServer.NormalContent.Length - 1,
            DownloadedBytes = 0
        };

        await using (var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance))
        {
            await writer.InitializeAsync(TestHttpServer.NormalContent.Length, CancellationToken.None);

            // /slow endpoint'i 20ms gecikmeli; stall timeout'ı aşırı küçük (1ms) vererek stall tetikliyoruz
            var worker = new SegmentWorker(
                segment,
                $"{_server.BaseUrl}slow",
                writer,
                _client,
                NullLogger<SegmentWorker>.Instance,
                maxRetries: 1,
                connectTimeout: TimeSpan.FromSeconds(5),
                stallTimeout: TimeSpan.FromMilliseconds(1)); // 1ms'de stall olmalı

            var ex = await Assert.ThrowsAsync<DownloadException>(() => worker.RunAsync(CancellationToken.None));
            Assert.Equal(DownloadErrorKind.Transient, ex.Kind);
            Assert.True(segment.RetryCount >= 1, "Stall sonrası retry sayacı artırılmış olmalı");
        }
    }
}

