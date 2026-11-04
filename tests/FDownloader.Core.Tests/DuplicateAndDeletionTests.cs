using FDownloader.Core.Domain;
using FDownloader.Core.Events;
using FDownloader.Core.Manager;
using FDownloader.Core.Storage;
using FDownloader.Core.Torrent;
using FDownloader.Core.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class DuplicateAndDeletionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Database _database;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly HttpClient _httpClient;
    private readonly TorrentService _torrentService;
    private readonly YtDlpMediaService _mediaService;

    public DuplicateAndDeletionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_DupDelTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var dbPath = Path.Combine(_tempDir, "dup_del_test.db");
        _database = new Database(dbPath, NullLogger<Database>.Instance);
        _database.Initialize();

        _repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);
        _eventBus = new DownloadEventBus();
        _httpClient = new HttpClient();
        _torrentService = new TorrentService(_eventBus, NullLogger<TorrentService>.Instance);
        _mediaService = new YtDlpMediaService(_eventBus, NullLogger<YtDlpMediaService>.Instance, _httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _database.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task AddHttpDownloadAsync_WhenAllowDuplicateIsFalse_ReturnsExistingTask()
    {
        await using var manager = new DownloadManager(
            _repository,
            _eventBus,
            _torrentService,
            _mediaService,
            _httpClient,
            NullLoggerFactory.Instance);

        const string testUrl = "http://example.com/unique_file.zip";

        // İlk ekleme
        var task1 = await manager.AddHttpDownloadAsync(testUrl, _tempDir, startImmediately: false);
        Assert.NotNull(task1);

        // İkinci ekleme allowDuplicate: false ile
        var task2 = await manager.AddHttpDownloadAsync(testUrl, _tempDir, startImmediately: false, allowDuplicate: false);
        Assert.NotNull(task2);

        // Aynı görev referansı / Id dönmeli
        Assert.Equal(task1.Id, task2.Id);
        Assert.Single(manager.Tasks);
    }

    [Fact]
    public async Task AddHttpDownloadAsync_WhenAllowDuplicateIsTrue_CreatesNewTask()
    {
        await using var manager = new DownloadManager(
            _repository,
            _eventBus,
            _torrentService,
            _mediaService,
            _httpClient,
            NullLoggerFactory.Instance);

        const string testUrl = "http://example.com/duplicate_allowed.zip";

        var task1 = await manager.AddHttpDownloadAsync(testUrl, _tempDir, startImmediately: false);
        var task2 = await manager.AddHttpDownloadAsync(testUrl, _tempDir, startImmediately: false, allowDuplicate: true);

        Assert.NotEqual(task1.Id, task2.Id);
        Assert.Equal(2, manager.Tasks.Count);
    }

    [Fact]
    public async Task RemoveTaskAsync_WithoutDeleteFile_KeepsFilesOnDisk()
    {
        await using var manager = new DownloadManager(
            _repository,
            _eventBus,
            _torrentService,
            _mediaService,
            _httpClient,
            NullLoggerFactory.Instance);

        var task = await manager.AddHttpDownloadAsync("http://example.com/keep_file.zip", _tempDir, fileName: "keep_file.zip", startImmediately: false);

        var targetFile = Path.Combine(_tempDir, "keep_file.zip");
        await File.WriteAllTextAsync(targetFile, "content to preserve");

        // deleteFile: false ile kaldır
        await manager.RemoveTaskAsync(task.Id, deleteFile: false);

        // Dosya diskte kalmalı
        Assert.True(File.Exists(targetFile));

        // DB'den ve listeden silinmiş olmalı
        Assert.DoesNotContain(manager.Tasks, t => t.Id == task.Id);
        Assert.Null(_repository.GetTask(task.Id));
    }

    [Fact]
    public async Task RemoveTaskAsync_WithDeleteFile_DeletesFilesAndTempFilesFromDisk()
    {
        await using var manager = new DownloadManager(
            _repository,
            _eventBus,
            _torrentService,
            _mediaService,
            _httpClient,
            NullLoggerFactory.Instance);

        var task = await manager.AddHttpDownloadAsync("http://example.com/delete_file.zip", _tempDir, fileName: "delete_file.zip", startImmediately: false);
        task.TempFilePath = Path.Combine(_tempDir, "delete_file.zip.fdown");

        var targetFile = Path.Combine(_tempDir, "delete_file.zip");
        var tempFile = Path.Combine(_tempDir, "delete_file.zip.fdown");
        var partFile = Path.Combine(_tempDir, "delete_file.zip.part");

        await File.WriteAllTextAsync(targetFile, "target content");
        await File.WriteAllTextAsync(tempFile, "temp content");
        await File.WriteAllTextAsync(partFile, "part content");

        // deleteFile: true ile kaldır
        await manager.RemoveTaskAsync(task.Id, deleteFile: true);

        // Dosyalar diskten silinmiş olmalı
        Assert.False(File.Exists(targetFile));
        Assert.False(File.Exists(tempFile));
        Assert.False(File.Exists(partFile));

        // DB'den ve listeden silinmiş olmalı
        Assert.DoesNotContain(manager.Tasks, t => t.Id == task.Id);
        Assert.Null(_repository.GetTask(task.Id));
    }
}
