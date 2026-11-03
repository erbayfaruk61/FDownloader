using System.Security.Cryptography;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine;
using FDownloader.Core.Engine.Segmenter;
using FDownloader.Core.Events;
using FDownloader.Core.Storage;
using FDownloader.Core.Tests.MockServer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class DynamicSegmentSplitTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly HttpClient _client;
    private readonly string _tempDir;
    private readonly Database _database;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;

    public DynamicSegmentSplitTests()
    {
        _server = new TestHttpServer();
        _server.Start();
        _client = new HttpClient();
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_SplitTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var dbPath = Path.Combine(_tempDir, "test.db");
        _database = new Database(dbPath, NullLogger<Database>.Instance);
        _database.Initialize();
        _repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);
        _eventBus = new DownloadEventBus();
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        _database.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public void SplitLargestActiveSegment_SplitsCorrectlyAndCreatesNonOverlappingRanges()
    {
        long totalBytes = 10 * 1024 * 1024; // 10 MB
        var segments = new List<Segment>
        {
            new Segment
            {
                TaskId = 1,
                Index = 0,
                StartByte = 0,
                EndByte = totalBytes - 1,
                DownloadedBytes = 2 * 1024 * 1024, // 2 MB indirildi, 8 MB kaldı
                Status = SegmentStatus.Downloading
            }
        };

        var newSegment = SegmentPlanner.SplitLargestActiveSegment(segments, minSplitSizeBytes: 1024 * 1024);

        Assert.NotNull(newSegment);
        Assert.Equal(2, segments.Count);

        var original = segments[0];
        // Orijinal segment ve yeni segment sınırları
        Assert.Equal(0, original.StartByte);
        Assert.Equal(original.EndByte + 1, newSegment.StartByte); // Aralıklar tam bitişik ve çakışmasız!
        Assert.Equal(totalBytes - 1, newSegment.EndByte);

        // Toplam bayt sayısı korunmalı
        long totalCovered = original.TotalBytes + newSegment.TotalBytes;
        Assert.Equal(totalBytes, totalCovered);
    }

    [Fact]
    public void SplitLargestActiveSegment_WhenRemainingBelowThreshold_ReturnsNull()
    {
        var segments = new List<Segment>
        {
            new Segment
            {
                TaskId = 1,
                Index = 0,
                StartByte = 0,
                EndByte = 1024 * 1024,
                DownloadedBytes = 800 * 1024, // Kalan az
                Status = SegmentStatus.Downloading
            }
        };

        // 1 MB eşik için kalan 224 KB bölünemez
        var newSegment = SegmentPlanner.SplitLargestActiveSegment(segments, minSplitSizeBytes: 512 * 1024);
        Assert.Null(newSegment);
    }

    [Fact]
    public async Task DynamicSplitting_InHttpDownloader_CompletesWithValidHash()
    {
        // 1 MB'lık deterministik akış
        var task = new DownloadTask
        {
            OriginalUrl = $"{_server.BaseUrl}large-stream",
            FinalUrl = $"{_server.BaseUrl}large-stream",
            FileName = "large_split.bin",
            SaveDirectory = _tempDir,
            TotalBytes = 1024 * 1024,
            SupportsRange = true,
            SegmentCount = 4 // 4 worker concurrency
        };

        _repository.AddTask(task);

        // Başlangıçta sadece 1 büyük segment planlayalım ki diğer 3 worker dinamik olarak bölüp iş çalsın!
        task.Segments.Add(new Segment
        {
            TaskId = task.Id,
            Index = 0,
            StartByte = 0,
            EndByte = task.TotalBytes - 1,
            DownloadedBytes = 0,
            Status = SegmentStatus.Pending
        });
        _repository.SaveSegmentProgress(task.Segments);

        using var downloader = new HttpDownloader(
            task,
            _repository,
            _eventBus,
            _client,
            NullLoggerFactory.Instance);

        var tcs = new TaskCompletionSource<bool>();
        _eventBus.Subscribe<TaskCompletedEvent>(e =>
        {
            if (e.TaskId == task.Id) tcs.TrySetResult(true);
        });
        _eventBus.Subscribe<TaskFailedEvent>(e =>
        {
            if (e.TaskId == task.Id) tcs.TrySetException(new Exception($"İndirme başarısız: {e.Reason}"));
        });

        downloader.Start();

        var completed = await System.Threading.Tasks.Task.WhenAny(tcs.Task, System.Threading.Tasks.Task.Delay(15000));
        Assert.True(completed == tcs.Task, "İndirme zaman aşımına uğradı!");
        Assert.True(await tcs.Task);

        string targetFile = task.FinalFilePath;
        Assert.True(File.Exists(targetFile));

        // Deterministik dosyanın SHA-256 doğrulaması
        var fileBytes = await File.ReadAllBytesAsync(targetFile);
        var actualHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();

        // TestHttpServer large-stream içeriğinin beklenen hash'i
        var expectedBytes = new byte[1024 * 1024];
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            expectedBytes[i] = TestHttpServer.LargeChunkPattern[i % TestHttpServer.LargeChunkPattern.Length];
        }
        var expectedHash = Convert.ToHexString(SHA256.HashData(expectedBytes)).ToLowerInvariant();

        Assert.Equal(expectedHash, actualHash);
    }
}
