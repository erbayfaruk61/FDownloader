using System.Security.Cryptography;
using FDownloader.Core.Domain;
using FDownloader.Core.Engine;
using FDownloader.Core.Events;
using FDownloader.Core.Storage;
using FDownloader.Core.Tests.MockServer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class LosslessResumeTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly string _testDir;
    private readonly Database _database;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly HttpClient _httpClient;

    public LosslessResumeTests()
    {
        _server = new TestHttpServer();
        _server.Start();

        _testDir = Path.Combine(Path.GetTempPath(), "FDownloader_ResumeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        var dbPath = Path.Combine(_testDir, "test_resume.db");
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
    public async Task HttpDownloader_PauseAndResume_MatchesExpectedHash()
    {
        // 1 MB deterministik veri akışı
        var task = new DownloadTask
        {
            OriginalUrl = $"{_server.BaseUrl}large-stream",
            FileName = "resume_test.bin",
            SaveDirectory = _testDir,
            SegmentCount = 4,
            SupportsRange = true
        };

        _repository.AddTask(task);

        // 1. İndirmeyi başlat
        var downloader1 = new HttpDownloader(
            task,
            _repository,
            _eventBus,
            _httpClient,
            NullLoggerFactory.Instance);

        downloader1.Start();

        // Bir miktar veri inene kadar bekle (örn. 250ms)
        await System.Threading.Tasks.Task.Delay(250);

        // 2. İndirmeyi duraklat (Pause)
        await downloader1.PauseAsync();
        downloader1.Dispose();

        // Duraklatıldıktan sonra durumun Paused olduğunu ve geçici dosyanın durduğunu doğrula
        Assert.Equal(DownloadStatus.Paused, task.Status);
        Assert.True(File.Exists(task.TempFilePath), "Geçici .fdown dosyası silinmemeli");

        // Kaydedilen checkpoint durumunu DB'den çek
        var savedTask = _repository.GetTask(task.Id);
        Assert.NotNull(savedTask);
        Assert.NotEmpty(savedTask.Segments);

        // En azından bazı parçalarda ilerleme kaydedilmiş olmalı
        long downloadedSoFar = savedTask.Segments.Sum(s => s.DownloadedBytes);

        // 3. İkinci bir Downloader instance'ı ile devam ettir (Resume)
        var downloader2 = new HttpDownloader(
            savedTask,
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
            if (e.TaskId == task.Id) tcs.TrySetException(new Exception($"Resume sonrası indirme başarısız: {e.Reason}"));
        });

        downloader2.Start();

        var completed = await System.Threading.Tasks.Task.WhenAny(tcs.Task, System.Threading.Tasks.Task.Delay(15000));
        Assert.True(completed == tcs.Task, "Resume indirmesi zaman aşımına uğradı!");
        Assert.True(await tcs.Task);

        // 4. Nihai dosyanın SHA-256 bütünlüğünü doğrula
        var finalPath = savedTask.FinalFilePath;
        Assert.True(File.Exists(finalPath));

        var fileBytes = await File.ReadAllBytesAsync(finalPath);
        Assert.Equal(1024 * 1024, fileBytes.Length);

        using var sha256 = SHA256.Create();
        var actualHash = Convert.ToHexString(sha256.ComputeHash(fileBytes));

        var expectedBytes = new byte[1024 * 1024];
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            expectedBytes[i] = TestHttpServer.LargeChunkPattern[i % TestHttpServer.LargeChunkPattern.Length];
        }
        var expectedHash = Convert.ToHexString(sha256.ComputeHash(expectedBytes));

        Assert.Equal(expectedHash, actualHash);
    }
}

