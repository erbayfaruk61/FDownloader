using System.Security.Cryptography;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine;
using FDownloader.Core.Events;
using FDownloader.Core.Storage;
using FDownloader.Core.Tests.MockServer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class DataIntegrityHashTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly string _testDir;
    private readonly Database _database;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly HttpClient _httpClient;

    public DataIntegrityHashTests()
    {
        _server = new TestHttpServer();
        _server.Start();

        _testDir = Path.Combine(Path.GetTempPath(), "FDownloader_HashTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        var dbPath = Path.Combine(_testDir, "test_tasks.db");
        _database = new Database(dbPath, NullLogger<Database>.Instance);
        _database.Initialize();

        _repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);
        _eventBus = new DownloadEventBus();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _database.Dispose();
        _server.Dispose();

        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task HttpDownloader_MultiSegment_MatchesExpectedHash(int segmentCount)
    {
        var task = new DownloadTask
        {
            OriginalUrl = $"{_server.BaseUrl}large-stream",
            FileName = $"large_test_{segmentCount}.bin",
            SaveDirectory = _testDir,
            SegmentCount = segmentCount,
            SupportsRange = true
        };

        _repository.AddTask(task);

        var downloader = new HttpDownloader(
            task,
            _repository,
            _eventBus,
            _httpClient,
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

        // İndirilen dosyanın SHA-256 hash'ini hesapla
        var finalPath = task.FinalFilePath;
        Assert.True(File.Exists(finalPath), $"Final dosyası bulunamadı: {finalPath}");

        var fileBytes = await File.ReadAllBytesAsync(finalPath);
        Assert.Equal(1024 * 1024, fileBytes.Length);

        // Beklenen hash: Deterministik akış için yerinde hash
        using var sha256 = SHA256.Create();
        var actualHash = Convert.ToHexString(sha256.ComputeHash(fileBytes));

        // Deterministik referans veri üret
        var expectedBytes = new byte[1024 * 1024];
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            expectedBytes[i] = TestHttpServer.LargeChunkPattern[i % TestHttpServer.LargeChunkPattern.Length];
        }
        var expectedHash = Convert.ToHexString(sha256.ComputeHash(expectedBytes));

        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public async Task HttpDownloader_NonDivisibleSize_MatchesHash()
    {
        // 97 baytlık (asallardan oluşan) tam bölünmeyen dosya indirmesi
        var task = new DownloadTask
        {
            OriginalUrl = $"{_server.BaseUrl}normal",
            FileName = "normal_test.bin",
            SaveDirectory = _testDir,
            SegmentCount = 4,
            SupportsRange = true
        };

        _repository.AddTask(task);

        var downloader = new HttpDownloader(
            task,
            _repository,
            _eventBus,
            _httpClient,
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

        var completed = await System.Threading.Tasks.Task.WhenAny(tcs.Task, System.Threading.Tasks.Task.Delay(10000));
        Assert.True(completed == tcs.Task, "İndirme zaman aşımına uğradı!");
        Assert.True(await tcs.Task);

        var fileBytes = await File.ReadAllBytesAsync(task.FinalFilePath);
        Assert.Equal(TestHttpServer.NormalContent.Length, fileBytes.Length);

        using var sha256 = SHA256.Create();
        var actualHash = Convert.ToHexString(sha256.ComputeHash(fileBytes)).ToLowerInvariant();
        Assert.Equal(TestHttpServer.NormalContentSha256, actualHash);
    }
}
