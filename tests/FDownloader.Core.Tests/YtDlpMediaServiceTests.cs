using FDownloader.Core.Events;
using FDownloader.Core.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class YtDlpMediaServiceTests : IDisposable
{
    private readonly DownloadEventBus _eventBus;
    private readonly HttpClient _httpClient;
    private readonly YtDlpMediaService _service;

    public YtDlpMediaServiceTests()
    {
        _eventBus = new DownloadEventBus();
        _httpClient = new HttpClient();
        _service = new YtDlpMediaService(_eventBus, NullLogger<YtDlpMediaService>.Instance, _httpClient);
    }

    public void Dispose()
    {
        _service.Dispose();
        _httpClient.Dispose();
    }

    [Theory]
    [InlineData("ERROR: [youtube] xyz: HTTP Error 403: Forbidden", 1, "Sunucu erişimi engelledi (HTTP 403)")]
    [InlineData("ERROR: [generic] abc: HTTP Error 404: Not Found", 1, "sunucuda bulunamadı (HTTP 404)")]
    [InlineData("ERROR: Sign in to confirm your age", 1, "yaş kısıtlamalıdır")]
    [InlineData("ERROR: Sign in to confirm you’re not a bot", 1, "bot doğrulaması")]
    [InlineData("ERROR: Video unavailable. This video is private.", 1, "yayından kaldırılmış")]
    [InlineData("ERROR: The uploader has not made this video available in your country", 1, "bulunduğunuz coğrafi bölgede")]
    [InlineData("WARNING: ffmpeg not found! Streams cannot be merged.", 1, "ffmpeg aracı bulunamadı")]
    [InlineData("ERROR: [download] No space left on device", 1, "Yetersiz disk alanı")]
    [InlineData("ERROR: Unsupported URL: https://invalid-site.com", 1, "Desteklenmeyen medya URL'si")]
    [InlineData("ERROR: This video is DRM protected (Widevine)", 1, "Bu içerik DRM (Dijital Hak Yönetimi) ile korunmaktadır ve indirilemez.")]
    public void FormatTurkishErrorMessage_TranslatesCommonErrorsAccurately(string rawError, int exitCode, string expectedSnippet)
    {
        var message = YtDlpMediaService.FormatTurkishErrorMessage(rawError, exitCode);
        Assert.Contains(expectedSnippet, message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatTurkishErrorMessage_WhenEmptyError_ReturnsGenericFailure()
    {
        var message = YtDlpMediaService.FormatTurkishErrorMessage(null, 2);
        Assert.Contains("Medya indirme işlemi başarısız oldu", message);
        Assert.Contains("2", message);
    }

    [Theory]
    [InlineData("Adele", "Hello", "Adele - Hello")]
    [InlineData("Adele", "Adele - Rolling in the Deep", "Adele - Rolling in the Deep")]
    [InlineData("Queen - Topic", "Bohemian Rhapsody", "Queen - Bohemian Rhapsody")]
    [InlineData(null, "Just Title", "Just Title")]
    [InlineData("Author Only", null, "Author Only")]
    public void FormatMediaTitle_FormatsAuthorAndTitleCorrectly(string? author, string? title, string expected)
    {
        var result = YtDlpMediaService.FormatMediaTitle(author, title);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", true)]
    [InlineData("https://www.tiktok.com/@user/video/1234567890", true)]
    [InlineData("https://www.instagram.com/reel/C123456/", true)]
    [InlineData("https://x.com/user/status/123456789", true)]
    [InlineData("https://twitter.com/user/status/123456789", true)]
    [InlineData("https://soundcloud.com/artist/track", true)]
    [InlineData("https://example.com/files/document.pdf", false)]
    [InlineData("https://archive.org/download/item.zip", false)]
    [InlineData("", false)]
    public void IsMediaUrl_RecognizesMediaPlatforms(string url, bool expectedIsMedia)
    {
        var isMedia = YtDlpMediaService.IsMediaUrl(url);
        Assert.Equal(expectedIsMedia, isMedia);
    }

    [Fact]
    public void BuildCommonArgumentList_ContainsSecurityAndRuntimeArguments()
    {
        var args = YtDlpMediaService.BuildCommonArgumentList();

        Assert.NotEmpty(args);
        Assert.Contains("--extractor-args", args);
        Assert.Contains("youtube:player_client=web,default,android", args);
        Assert.Contains("--js-runtimes", args);
    }

    [Fact]
    public void StopDownload_WhenTaskNotRunning_DoesNotThrow()
    {
        var ex = Record.Exception(() => _service.StopDownload(999999));
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_CleansUpSafely()
    {
        var ex = Record.Exception(() =>
        {
            _service.Dispose();
            _service.Dispose();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void MediaMetadata_Formats_CanBeAddedAndOrderedByHeight()
    {
        var metadata = new MediaMetadata
        {
            Url = "https://youtube.com/watch?v=123",
            Title = "Test Video",
            Formats =
            {
                new MediaFormatInfo { FormatId = "137", Resolution = "1080p", Height = 1080 },
                new MediaFormatInfo { FormatId = "22", Resolution = "720p", Height = 720 },
                new MediaFormatInfo { FormatId = "18", Resolution = "360p", Height = 360 }
            }
        };

        Assert.Equal(3, metadata.Formats.Count);
        Assert.Equal("1080p", metadata.Formats[0].Resolution);
        Assert.Equal(1080, metadata.Formats[0].Height);
    }
}
