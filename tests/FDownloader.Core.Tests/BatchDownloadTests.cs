using FDownloader.Core.Batch;
using FDownloader.Core.Domain;
using FDownloader.Core.Events;
using FDownloader.Core.Manager;
using FDownloader.Core.Network;
using FDownloader.Core.Storage;
using FDownloader.Core.Torrent;
using FDownloader.Core.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class BatchDownloadTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempDir;
    private readonly DownloadRepository _repository;
    private readonly DownloadEventBus _eventBus;
    private readonly DownloadManager _downloadManager;

    public BatchDownloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_BatchTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _testDbPath = Path.Combine(_tempDir, "test_batch.db");

        var db = new Database(_testDbPath, NullLogger<Database>.Instance);
        db.Initialize();
        _repository = new DownloadRepository(db, NullLogger<DownloadRepository>.Instance);
        _eventBus = new DownloadEventBus();
        var httpClient = new HttpClient();
        var torrentService = new TorrentService(_eventBus, NullLogger<TorrentService>.Instance);
        var mediaService = new YtDlpMediaService(_eventBus, NullLogger<YtDlpMediaService>.Instance, httpClient);
        var loggerFactory = NullLoggerFactory.Instance;
        var credManager = new SiteCredentialManager(NullLogger<SiteCredentialManager>.Instance);

        _downloadManager = new DownloadManager(
            _repository,
            _eventBus,
            torrentService,
            mediaService,
            httpClient,
            loggerFactory,
            credManager);
    }

    public void Dispose()
    {
        _downloadManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void BatchRangePattern_HasPattern_DetectsValidPatterns()
    {
        Assert.True(BatchRangePattern.HasPattern("https://example.com/file_[01-20].zip"));
        Assert.True(BatchRangePattern.HasPattern("https://example.com/item_[1-100:2].mp4"));
        Assert.True(BatchRangePattern.HasPattern("https://example.com/page_[a-z].html"));
        Assert.False(BatchRangePattern.HasPattern("https://example.com/plain_url.zip"));
        Assert.False(BatchRangePattern.HasPattern(""));
        Assert.False(BatchRangePattern.HasPattern(null!));
    }

    [Fact]
    public void BatchRangePattern_NumericPadding_ExpandsCorrectly()
    {
        var template = "https://cdn.site.com/episodes/ep_[01-05].mp4";
        var result = BatchRangePattern.ExpandPattern(template);

        Assert.Equal(5, result.Count);
        Assert.Equal("https://cdn.site.com/episodes/ep_01.mp4", result[0]);
        Assert.Equal("https://cdn.site.com/episodes/ep_02.mp4", result[1]);
        Assert.Equal("https://cdn.site.com/episodes/ep_03.mp4", result[2]);
        Assert.Equal("https://cdn.site.com/episodes/ep_04.mp4", result[3]);
        Assert.Equal("https://cdn.site.com/episodes/ep_05.mp4", result[4]);
    }

    [Fact]
    public void BatchRangePattern_StepAndDescending_ExpandsCorrectly()
    {
        // Adım miktarlı
        var stepTemplate = "https://cdn.site.com/data_[1-9:2].csv";
        var stepResult = BatchRangePattern.ExpandPattern(stepTemplate);
        Assert.Equal(new[]
        {
            "https://cdn.site.com/data_1.csv",
            "https://cdn.site.com/data_3.csv",
            "https://cdn.site.com/data_5.csv",
            "https://cdn.site.com/data_7.csv",
            "https://cdn.site.com/data_9.csv"
        }, stepResult);

        // Azalan aralık
        var descTemplate = "https://cdn.site.com/countdown_[03-01].bin";
        var descResult = BatchRangePattern.ExpandPattern(descTemplate);
        Assert.Equal(new[]
        {
            "https://cdn.site.com/countdown_03.bin",
            "https://cdn.site.com/countdown_02.bin",
            "https://cdn.site.com/countdown_01.bin"
        }, descResult);
    }

    [Fact]
    public void BatchRangePattern_Alphabetic_ExpandsLetters()
    {
        var template = "https://cdn.site.com/part_[a-d].tar";
        var result = BatchRangePattern.ExpandPattern(template);

        Assert.Equal(new[]
        {
            "https://cdn.site.com/part_a.tar",
            "https://cdn.site.com/part_b.tar",
            "https://cdn.site.com/part_c.tar",
            "https://cdn.site.com/part_d.tar"
        }, result);
    }

    [Fact]
    public void BatchDownloadParser_MultilineText_ParsesAndDeduplicates()
    {
        var input = @"
            # Bu bir yorum satırıdır
            https://example.com/file1.zip
            https://example.com/file2.zip
            https://example.com/file1.zip

            Geçersiz Satır
            https://example.com/file3.rar
        ";

        var options = new BatchDownloadOptions { DeduplicateUrls = true };
        var items = BatchDownloadParser.Parse(input, options);

        // 3 geçerli URL (file1, file2, file3) ve 1 geçersiz satır
        var validItems = items.Where(i => i.IsValid).ToList();
        Assert.Equal(3, validItems.Count);
        Assert.Equal("https://example.com/file1.zip", validItems[0].Url);
        Assert.Equal("file1.zip", validItems[0].SuggestedFileName);
        Assert.Equal("https://example.com/file2.zip", validItems[1].Url);
        Assert.Equal("https://example.com/file3.rar", validItems[2].Url);

        var invalidItems = items.Where(i => !i.IsValid).ToList();
        Assert.Single(invalidItems);
        Assert.Contains("Geçersiz Satır", invalidItems[0].Url);
    }

    [Fact]
    public void BatchDownloadParser_WithCustomFileNamePattern_AppliesTemplate()
    {
        var input = @"
            https://site.org/doc/report.pdf
            https://site.org/doc/summary.pdf
        ";

        var options = new BatchDownloadOptions
        {
            FileNamePattern = "Ders_{index:D2}_{name}.{ext}"
        };

        var items = BatchDownloadParser.Parse(input, options);

        Assert.Equal(2, items.Count);
        Assert.Equal("Ders_01_report.pdf", items[0].SuggestedFileName);
        Assert.Equal("Ders_02_summary.pdf", items[1].SuggestedFileName);
    }

    [Fact]
    public async Task DownloadManager_AddBatchDownloadsAsync_CreatesAndQueuesTasks()
    {
        var template = "https://test.local/archive_[01-04].zip";
        var options = new BatchDownloadOptions
        {
            DestinationDirectory = _tempDir,
            Segments = 4
        };

        var tasks = await _downloadManager.AddBatchDownloadsAsync(template, options, startImmediately: false);

        Assert.Equal(4, tasks.Count);
        Assert.All(tasks, t => Assert.Equal(DownloadStatus.Queued, t.Status));
        Assert.All(tasks, t => Assert.Equal(_tempDir, t.SaveDirectory));
        Assert.Equal("https://test.local/archive_01.zip", tasks[0].OriginalUrl);
        Assert.Equal("archive_01.zip", tasks[0].FileName);
        Assert.Equal("https://test.local/archive_04.zip", tasks[3].OriginalUrl);
        Assert.Equal("archive_04.zip", tasks[3].FileName);

        // DownloadManager listesinde mevcut olduğunu doğrula
        var managerTasks = _downloadManager.Tasks;
        Assert.Contains(managerTasks, t => t.OriginalUrl == "https://test.local/archive_01.zip");
        Assert.Contains(managerTasks, t => t.OriginalUrl == "https://test.local/archive_04.zip");
    }
}
