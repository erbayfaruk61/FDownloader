using System.Net;
using System.Text;
using FDownloader.Core.Domain;
using FDownloader.Core.Events;
using FDownloader.Core.Torrent;
using Microsoft.Extensions.Logging.Abstractions;
using MonoTorrent;
using MonoTorrent.Client;
using Xunit;
using AppTorrentSettings = FDownloader.Core.Torrent.TorrentSettings;

namespace FDownloader.Core.Tests;

[Collection("TorrentTests")]
public class TorrentServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _downloadDir;
    private readonly DownloadEventBus _eventBus;
    private readonly TorrentService _torrentService;

    public TorrentServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fdownloader_torrent_test_" + Guid.NewGuid().ToString("N"));
        _downloadDir = Path.Combine(_tempDir, "downloads");
        Directory.CreateDirectory(_downloadDir);

        _eventBus = new DownloadEventBus();
        _torrentService = new TorrentService(
            _eventBus,
            NullLogger<TorrentService>.Instance,
            _downloadDir,
            new AppTorrentSettings
            {
                ListeningPort = 0,
                EnableDht = false,
                EnableUpnp = false,
                EnableLsd = false,
                EnableAutoSequentialForVideo = false,
                EnableFallbackTrackers = false
            },
            Path.Combine(_tempDir, "torrent_cache"));
    }

    public void Dispose()
    {
        try
        {
            _torrentService.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch { }

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private async Task<string> CreateSampleTorrentFileAsync(string torrentName, params (string RelPath, string Content)[] files)
    {
        var dataDir = Path.Combine(_tempDir, "torrent_src_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);

        foreach (var (relPath, content) in files)
        {
            var fullPath = Path.Combine(dataDir, relPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            await File.WriteAllTextAsync(fullPath, content);
        }

        var creator = new TorrentCreator();

        var fileSource = new TorrentFileSource(dataDir);
        var bencoded = await creator.CreateAsync(fileSource);

        var torrentFilePath = Path.Combine(_tempDir, $"{torrentName}.torrent");
        await File.WriteAllBytesAsync(torrentFilePath, bencoded.Encode());
        return torrentFilePath;
    }

    [Fact]
    public async Task ApplySettingsAsync_UpdatesConfiguredSettings()
    {
        var newSettings = new AppTorrentSettings
        {
            ListeningPort = 55123,
            EnableDht = false,
            EnableUpnp = false,
            EnableLsd = false,
            EnableFallbackTrackers = false,
            EncryptionMode = 1,
            MaxConnectionsTotal = 150,
            SeedRatioLimit = 2.0,
            StopSeedingWhenRatioReached = true
        };

        await _torrentService.ApplySettingsAsync(newSettings);

        Assert.Equal(55123, _torrentService.Settings.ListeningPort);
        Assert.Equal(1, _torrentService.Settings.EncryptionMode);
        Assert.Equal(2.0, _torrentService.Settings.SeedRatioLimit);
        Assert.True(_torrentService.Settings.StopSeedingWhenRatioReached);
    }

    [Fact]
    public async Task InspectTorrentFilesAsync_FromLocalFile_ReturnsFileListCorrectly()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "sample_pkg",
            ("video.mp4", "fake video data 1234567890"),
            ("notes.txt", "some sample text notes")
        );

        var inspected = await _torrentService.InspectTorrentFilesAsync(torrentPath);

        Assert.NotNull(inspected);
        Assert.Equal(2, inspected.Count);
        Assert.Contains(inspected, f => f.Path.Contains("video.mp4") && f.Length > 0);
        Assert.Contains(inspected, f => f.Path.Contains("notes.txt") && f.Length > 0);
    }

    [Fact]
    public async Task AddAndStartTorrentAsync_WithInvalidSource_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _torrentService.AddAndStartTorrentAsync(999, "invalid_source_format");
        });
    }

    [Fact]
    public async Task AddAndStartTorrentAsync_WithLocalFile_LoadsMetadataAndStarts()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_task_1",
            ("file1.dat", "binary data 1111"),
            ("file2.dat", "binary data 2222")
        );

        var startedTcs = new TaskCompletionSource<bool>();
        var metaTcs = new TaskCompletionSource<bool>();

        _eventBus.Subscribe<TaskStartedEvent>(e =>
        {
            if (e.TaskId == 100) startedTcs.TrySetResult(true);
        });

        _eventBus.Subscribe<TorrentMetadataLoadedEvent>(e =>
        {
            if (e.TaskId == 100) metaTcs.TrySetResult(true);
        });

        var manager = await _torrentService.AddAndStartTorrentAsync(100, torrentPath, _downloadDir);

        Assert.NotNull(manager);
        Assert.NotNull(manager.Torrent);

        var metaFinished = await Task.WhenAny(metaTcs.Task, Task.Delay(3000));
        Assert.Same(metaTcs.Task, metaFinished);

        var startedFinished = await Task.WhenAny(startedTcs.Task, Task.Delay(3000));
        Assert.Same(startedTcs.Task, startedFinished);

        var details = _torrentService.GetTorrentDetails(100);
        Assert.NotNull(details);
        Assert.Equal(2, details.Files.Count);
    }

    [Fact]
    public async Task FilePriorityManagement_AllowsSelectingAndIgnoringSpecificFiles()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_priority",
            ("keep.iso", "important data"),
            ("ignore.tmp", "temp data")
        );

        var manager = await _torrentService.AddAndStartTorrentAsync(200, torrentPath, _downloadDir);
        Assert.NotNull(manager.Files);
        Assert.Equal(2, manager.Files.Count);

        // 1. Dosyayı indir, 2. Dosyayı indirme
        await _torrentService.SetFilePriorityAsync(200, 0, Priority.High);
        await _torrentService.SetFilePriorityAsync(200, 1, Priority.DoNotDownload);

        Assert.Equal(Priority.High, manager.Files[0].Priority);
        Assert.Equal(Priority.DoNotDownload, manager.Files[1].Priority);

        // Topluca tüm dosyaları indir
        await _torrentService.SetAllFilesPriorityAsync(200, downloadAll: true);
        Assert.Equal(Priority.Normal, manager.Files[0].Priority);
        Assert.Equal(Priority.Normal, manager.Files[1].Priority);

        // Topluca tüm dosyaları indirmeyi durdur
        await _torrentService.SetAllFilesPriorityAsync(200, downloadAll: false);
        Assert.Equal(Priority.DoNotDownload, manager.Files[0].Priority);
        Assert.Equal(Priority.DoNotDownload, manager.Files[1].Priority);
    }

    [Fact]
    public async Task ToggleSequentialDownloadAsync_TogglesSequentialStreamingState()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_seq",
            ("movie.mp4", "movie video bitstream payload")
        );

        var manager = await _torrentService.AddAndStartTorrentAsync(300, torrentPath, _downloadDir);
        Assert.False(_torrentService.IsSequentialDownload(300));

        // Sıralı indirmeyi aç
        var seq1 = await _torrentService.ToggleSequentialDownloadAsync(300);
        Assert.True(seq1);
        Assert.True(_torrentService.IsSequentialDownload(300));

        await Task.Delay(100);

        // Sıralı indirmeyi kapat
        var seq2 = await _torrentService.ToggleSequentialDownloadAsync(300);
        Assert.False(seq2);
        Assert.False(_torrentService.IsSequentialDownload(300));

        await _torrentService.RemoveTorrentAsync(300, deleteFiles: false);
    }

    [Fact]
    public async Task AddTrackersAsync_And_ForceReannounce_AddCustomTrackers()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_trackers",
            ("data.bin", "sample payload")
        );

        await _torrentService.AddAndStartTorrentAsync(400, torrentPath, _downloadDir);

        var addedCount = await _torrentService.AddTrackersAsync(400, new[]
        {
            "http://127.0.0.1:8081/announce",
            "udp://127.0.0.1:8082/announce"
        });

        Assert.Equal(2, addedCount);

        // ForceReannounceAsync çağrısı istisna fırlatmamalı
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _torrentService.ForceReannounceAsync(400);
        });
        Assert.Null(exception);
    }

    [Fact]
    public async Task ForceRecheckAsync_InitiatesHashCheckSuccessfully()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_recheck",
            ("check_file.txt", "content for hash verification")
        );

        await _torrentService.AddAndStartTorrentAsync(500, torrentPath, _downloadDir);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await _torrentService.ForceRecheckAsync(500);
        });
        Assert.Null(exception);
    }

    [Fact]
    public async Task PauseResumeAndRemoveLifecycle_TransitionsEventsProperly()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_lifecycle",
            ("item.bin", "sample test data for lifecycle")
        );

        await _torrentService.AddAndStartTorrentAsync(600, torrentPath, _downloadDir);

        var pauseTcs = new TaskCompletionSource<bool>();
        _eventBus.Subscribe<TaskPausedEvent>(e =>
        {
            if (e.TaskId == 600) pauseTcs.TrySetResult(true);
        });

        await _torrentService.PauseTorrentAsync(600);
        var pauseFinished = await Task.WhenAny(pauseTcs.Task, Task.Delay(3000));
        Assert.Same(pauseTcs.Task, pauseFinished);

        var resumeTcs = new TaskCompletionSource<bool>();
        _eventBus.Subscribe<TaskResumedEvent>(e =>
        {
            if (e.TaskId == 600) resumeTcs.TrySetResult(true);
        });

        var resumed = await _torrentService.ResumeTorrentAsync(600);
        Assert.True(resumed);
        var resumeFinished = await Task.WhenAny(resumeTcs.Task, Task.Delay(3000));
        Assert.Same(resumeTcs.Task, resumeFinished);

        var cancelTcs = new TaskCompletionSource<bool>();
        _eventBus.Subscribe<TaskCancelledEvent>(e =>
        {
            if (e.TaskId == 600) cancelTcs.TrySetResult(true);
        });

        await _torrentService.RemoveTorrentAsync(600, deleteFiles: true);
        var cancelFinished = await Task.WhenAny(cancelTcs.Task, Task.Delay(3000));
        Assert.Same(cancelTcs.Task, cancelFinished);

        var details = _torrentService.GetTorrentDetails(600);
        Assert.Null(details);
    }

    [Fact]
    public async Task AddAndStartTorrentAsync_WithMagnetLink_EnrichesWithFallbackTrackers()
    {
        // Standart bir SHA1 info-hash'e sahip magnet linki
        const string rawMagnet = "magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567&dn=UbuntuTest";

        _torrentService.Settings.EnableFallbackTrackers = true;
        var manager = await _torrentService.RestoreTorrentTaskAsync(700, rawMagnet, _downloadDir, startImmediately: false);

        Assert.NotNull(manager);
        Assert.NotNull(manager.TrackerManager);

        // Fallback tracker'ların eklenip eklenmediğini kontrol edelim
        var trackers = manager.TrackerManager.Tiers;
        Assert.NotNull(trackers);
        Assert.NotEmpty(trackers);

        // Temizleyelim
        await _torrentService.RemoveTorrentAsync(700, deleteFiles: false);
    }

    [Fact]
    public async Task RestoreTorrentTaskAsync_WhenCalled_RestoresManagerWithoutStartingUnlessRequested()
    {
        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_restore",
            ("payload.bin", "sample data for task restoration")
        );

        // 1. startImmediately: false olarak restore et
        var managerStopped = await _torrentService.RestoreTorrentTaskAsync(800, torrentPath, _downloadDir, startImmediately: false);
        Assert.NotNull(managerStopped);
        Assert.Equal(TorrentState.Stopped, managerStopped.State);

        await _torrentService.RemoveTorrentAsync(800, deleteFiles: false);

        // 2. startImmediately: true olarak restore et
        var managerStarted = await _torrentService.RestoreTorrentTaskAsync(801, torrentPath, _downloadDir, startImmediately: true);
        Assert.NotNull(managerStarted);
        Assert.NotEqual(TorrentState.Stopped, managerStarted.State);

        await _torrentService.RemoveTorrentAsync(801, deleteFiles: false);
    }

    [Fact]
    public async Task InspectTorrentFilesAsync_WithRemoteHttpTorrent_DownloadsAndListsFiles()
    {
        using var server = new MockServer.TestHttpServer();
        server.Start();

        var torrentPath = await CreateSampleTorrentFileAsync(
            "remote_pkg",
            ("folder/doc.pdf", "pdf header content")
        );
        var torrentBytes = await File.ReadAllBytesAsync(torrentPath);

        server.CustomHandlers["/remote.torrent"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "application/x-bittorrent";
            res.ContentLength64 = torrentBytes.Length;
            await res.OutputStream.WriteAsync(torrentBytes);
        };

        var remoteUrl = $"{server.BaseUrl}remote.torrent";
        var inspected = await _torrentService.InspectTorrentFilesAsync(remoteUrl);

        Assert.NotNull(inspected);
        Assert.Single(inspected);
        Assert.Contains("doc.pdf", inspected[0].Path);
        Assert.True(inspected[0].Length > 0);
    }

    [Fact]
    public void InfoHashes_EqualityCheck()
    {
        var m1 = MagnetLink.Parse("magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c");
        var m2 = MagnetLink.Parse("magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Test");
        Assert.True(m1.InfoHashes.Equals(m2.InfoHashes));
        Assert.True(m1.InfoHashes == m2.InfoHashes);
    }

    [Fact]
    public async Task SeedRatioLimit_WhenConfigured_SettingsAreAppliedAndReported()
    {
        var settings = new AppTorrentSettings
        {
            SeedRatioLimit = 1.5,
            StopSeedingWhenRatioReached = true
        };
        await _torrentService.ApplySettingsAsync(settings);

        var torrentPath = await CreateSampleTorrentFileAsync(
            "test_ratio",
            ("file.bin", "sample content")
        );

        var manager = await _torrentService.AddAndStartTorrentAsync(900, torrentPath, _downloadDir);
        Assert.NotNull(manager);

        var details = _torrentService.GetTorrentDetails(900);
        Assert.NotNull(details);
        Assert.Equal(0, details.ShareRatio);
        Assert.Equal(1.5, _torrentService.Settings.SeedRatioLimit);
        Assert.True(_torrentService.Settings.StopSeedingWhenRatioReached);

        await _torrentService.RemoveTorrentAsync(900, deleteFiles: false);
    }

    [Fact]
    public async Task InspectTorrentFilesAsync_FollowedBy_AddAndStartTorrentAsync_DoesNotThrowAlreadyRegistered()
    {
        var magnet = "magnet:?xt=urn:btih:342992b8d0c3eb1499b9cf9abf284ec0ef23ed95&dn=ubuntu-24.04-desktop-amd64.iso";
        // 1. First inspect the files (this may time out or succeed)
        await _torrentService.InspectTorrentFilesAsync(magnet);

        // 2. Then add and start the torrent
        var manager = await _torrentService.AddAndStartTorrentAsync(950, magnet, _downloadDir);
        Assert.NotNull(manager);

        await _torrentService.RemoveTorrentAsync(950, deleteFiles: false);
    }
}
