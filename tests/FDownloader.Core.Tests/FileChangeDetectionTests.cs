using FDownloader.Core.Domain;
using FDownloader.Core.Engine;
using FDownloader.Core.Events;
using FDownloader.Core.Storage;
using FDownloader.Core.Tests.MockServer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class FileChangeDetectionTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly string _testDir;
    private readonly Database _database;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly HttpClient _httpClient;

    public FileChangeDetectionTests()
    {
        _server = new TestHttpServer();
        _server.Start();

        _testDir = Path.Combine(Path.GetTempPath(), "FDownloader_ChangeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        var dbPath = Path.Combine(_testDir, "test_change.db");
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

    [Fact]
    public async Task Resume_WhenFileChangedOnServer_AbortsAndPublishesEvent()
    {
        // 1. Dinamik dosya sürüm 1 olarak başla
        _server.DynamicEtag = "\"etag-version-1\"";
        _server.DynamicContent = "Content Version 1 - Baseline";

        var task = new DownloadTask
        {
            OriginalUrl = $"{_server.BaseUrl}dynamic-file",
            FileName = "dynamic.txt",
            SaveDirectory = _testDir,
            SegmentCount = 1,
            SupportsRange = false,
            ETag = "\"etag-version-1\"",
            TotalBytes = _server.DynamicContent.Length
        };

        _repository.AddTask(task);

        task.Segments.Add(new Segment
        {
            TaskId = task.Id,
            Index = 0,
            StartByte = 0,
            EndByte = task.TotalBytes - 1,
            DownloadedBytes = 10, // Kısmen indirilmiş simülasyonu
            Status = SegmentStatus.Pending
        });

        _repository.AddSegments(task.Segments);

        // 2. Sunucudaki dosyayı değiştir (ETag değişir)
        _server.DynamicEtag = "\"etag-version-2\"";
        _server.DynamicContent = "Content Version 2 - Completely Changed Content";

        // 3. Resume denemesi yap
        var downloader = new HttpDownloader(
            task,
            _repository,
            _eventBus,
            _httpClient,
            NullLoggerFactory.Instance);

        FileChangedOnServerEvent? receivedChangeEvent = null;
        var tcs = new TaskCompletionSource<bool>();

        _eventBus.Subscribe<FileChangedOnServerEvent>(e =>
        {
            if (e.TaskId == task.Id) receivedChangeEvent = e;
        });

        _eventBus.Subscribe<TaskFailedEvent>(e =>
        {
            if (e.TaskId == task.Id) tcs.TrySetResult(true);
        });

        downloader.Start();

        var completed = await System.Threading.Tasks.Task.WhenAny(tcs.Task, System.Threading.Tasks.Task.Delay(10000));
        Assert.True(completed == tcs.Task, "Dosya değişim tespiti zaman aşımına uğradı!");

        // 4. Doğrulamalar
        Assert.NotNull(receivedChangeEvent);
        Assert.Equal(task.Id, receivedChangeEvent.TaskId);
        Assert.Contains("etag-version-2", receivedChangeEvent.NewEtag);
        Assert.Equal(DownloadStatus.Failed, task.Status);
        Assert.Contains("Dosya sunucuda değişmiş", task.LastErrorMessage);
    }
}
