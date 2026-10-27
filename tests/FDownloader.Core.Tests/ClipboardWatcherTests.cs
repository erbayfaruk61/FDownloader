using FDownloader.Core.Events;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class ClipboardWatcherTests
{
    [Fact]
    public void NormalWebSayfalariPanodanYakalanmaz()
    {
        var eventBus = new DownloadEventBus();
        var capturedEvents = new List<ClipboardUrlDetectedEvent>();
        eventBus.Subscribe<ClipboardUrlDetectedEvent>(e => capturedEvents.Add(e));

        using var watcher = new ClipboardWatcher(eventBus, NullLogger<ClipboardWatcher>.Instance);

        // Kullanıcının belirttiği site ve genel web sayfaları
        watcher.ProcessText("https://testfile.org/");
        watcher.ProcessText("https://testfile.org");
        watcher.ProcessText("https://www.google.com/");
        watcher.ProcessText("https://github.com/erbayfaruk61/FDownloader");
        watcher.ProcessText("https://example.com/index.html");
        watcher.ProcessText("https://example.com/article.php?id=123");

        Assert.Empty(capturedEvents);
    }

    [Fact]
    public void GecerliIndirmeDosyalariVeMedyalarYakalanir()
    {
        var eventBus = new DownloadEventBus();
        var capturedEvents = new List<ClipboardUrlDetectedEvent>();
        eventBus.Subscribe<ClipboardUrlDetectedEvent>(e => capturedEvents.Add(e));

        using var watcher = new ClipboardWatcher(eventBus, NullLogger<ClipboardWatcher>.Instance);

        watcher.ProcessText("https://testfile.org/files/100MB.zip");
        watcher.ProcessText("https://example.com/setup.exe");
        watcher.ProcessText("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        watcher.ProcessText("magnet:?xt=urn:btih:3b245504cf5f11bbdbe1201cea6a6bf45a0b77bc");

        Assert.Equal(4, capturedEvents.Count);
        Assert.Equal(ClipboardUrlType.DirectFile, capturedEvents[0].UrlType);
        Assert.Equal(ClipboardUrlType.DirectFile, capturedEvents[1].UrlType);
        Assert.Equal(ClipboardUrlType.MediaVideo, capturedEvents[2].UrlType);
        Assert.Equal(ClipboardUrlType.TorrentMagnet, capturedEvents[3].UrlType);
    }
}
