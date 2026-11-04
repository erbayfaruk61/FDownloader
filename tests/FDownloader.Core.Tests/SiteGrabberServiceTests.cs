using System.Net;
using System.Text;
using FDownloader.Core.Domain;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class SiteGrabberServiceTests : IDisposable
{
    private readonly MockServer.TestHttpServer _server;
    private readonly HttpClient _httpClient;
    private readonly SiteGrabberService _grabberService;

    public SiteGrabberServiceTests()
    {
        _server = new MockServer.TestHttpServer();
        _server.Start();

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _grabberService = new SiteGrabberService(_httpClient, NullLogger<SiteGrabberService>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task AnalyzePageAsync_InvalidUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _grabberService.AnalyzePageAsync("not-a-valid-url");
        });
    }

    [Fact]
    public async Task AnalyzePageAsync_SinglePage_ExtractsMediaFilesAndCategorizesCorrectly()
    {
        var htmlContent = """
            <!DOCTYPE html>
            <html>
            <head><title>Test Gallery</title></head>
            <body>
                <h1>Galeri</h1>
                <img src="/media/photo1.jpg" alt="Fotoğraf 1" />
                <img src="/media/banner.png" alt="Banner" />
                <video src="/media/clip.mp4" controls></video>
                <a href="/downloads/archive.zip">Arşiv İndir</a>
                <a href="/docs/guide.pdf">Rehber</a>
                <a href="javascript:void(0)">Tıklanmaz</a>
                <a href="mailto:info@example.com">E-posta</a>
            </body>
            </html>
            """;

        _server.CustomHandlers["/gallery.html"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "text/html; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(htmlContent);
            await res.OutputStream.WriteAsync(bytes);
        };

        var pageUrl = $"{_server.BaseUrl}gallery.html";
        var results = await _grabberService.AnalyzePageAsync(pageUrl, new SiteGrabberOptions
        {
            MaxDepth = 1,
            RespectRobotsTxt = false
        });

        Assert.NotNull(results);
        Assert.Equal(5, results.Count);

        Assert.Contains(results, r => r.FileName == "photo1.jpg" && r.Category == DownloadCategory.Image);
        Assert.Contains(results, r => r.FileName == "banner.png" && r.Category == DownloadCategory.Image);
        Assert.Contains(results, r => r.FileName == "clip.mp4" && r.Category == DownloadCategory.Video);
        Assert.Contains(results, r => r.FileName == "archive.zip" && r.Category == DownloadCategory.Archive);
        Assert.Contains(results, r => r.FileName == "guide.pdf" && r.Category == DownloadCategory.Document);
    }

    [Fact]
    public async Task AnalyzePageAsync_Depth2_CrawlsInternalLinksAndCollectsNestedFiles()
    {
        var indexHtml = """
            <html>
            <body>
                <img src="/img/pic1.jpg" />
                <a href="/sub/page2.html">İkinci Sayfa</a>
            </body>
            </html>
            """;

        var page2Html = """
            <html>
            <body>
                <a href="/files/presentation.pptx">Sunum</a>
                <video src="/videos/nested.mkv"></video>
            </body>
            </html>
            """;

        _server.CustomHandlers["/sub/index.html"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "text/html";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(indexHtml));
        };

        _server.CustomHandlers["/sub/page2.html"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "text/html";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(page2Html));
        };

        var pageUrl = $"{_server.BaseUrl}sub/index.html";

        // Derinlik 1: Sadece index.html taranmalı (1 resim)
        var depth1Results = await _grabberService.AnalyzePageAsync(pageUrl, new SiteGrabberOptions
        {
            MaxDepth = 1,
            RespectRobotsTxt = false,
            DelayBetweenRequestsMs = 0
        });
        Assert.Single(depth1Results);
        Assert.Equal("pic1.jpg", depth1Results[0].FileName);

        // Derinlik 2: page2.html de taranmalı (pic1 + presentation + nested)
        var depth2Results = await _grabberService.AnalyzePageAsync(pageUrl, new SiteGrabberOptions
        {
            MaxDepth = 2,
            RespectRobotsTxt = false,
            DelayBetweenRequestsMs = 0
        });
        Assert.Equal(3, depth2Results.Count);
        Assert.Contains(depth2Results, r => r.FileName == "pic1.jpg" && r.DepthFound == 1);
        Assert.Contains(depth2Results, r => r.FileName == "presentation.pptx" && r.DepthFound == 2);
        Assert.Contains(depth2Results, r => r.FileName == "nested.mkv" && r.DepthFound == 2);
    }

    [Fact]
    public async Task AnalyzePageAsync_RespectsRobotsTxt_SkipsDisallowedPaths()
    {
        var robotsContent = """
            User-agent: *
            Disallow: /admin/
            Disallow: /private.html
            """;

        var indexHtml = """
            <html>
            <body>
                <a href="/public/doc.pdf">Açık Belge</a>
                <a href="/admin/secret.zip">Yasaklı Dosya</a>
                <a href="/private.html">Yasaklı Sayfa</a>
            </body>
            </html>
            """;

        var privateHtml = """
            <html>
            <body>
                <img src="/private/photo.png" />
            </body>
            </html>
            """;

        _server.CustomHandlers["/robots.txt"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "text/plain";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(robotsContent));
        };

        _server.CustomHandlers["/index.html"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "text/html";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(indexHtml));
        };

        _server.CustomHandlers["/private.html"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "text/html";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(privateHtml));
        };

        var pageUrl = $"{_server.BaseUrl}index.html";
        var results = await _grabberService.AnalyzePageAsync(pageUrl, new SiteGrabberOptions
        {
            MaxDepth = 2,
            RespectRobotsTxt = true,
            DelayBetweenRequestsMs = 0
        });

        // /admin/secret.zip ve /private.html engellenmeli, yalnızca /public/doc.pdf gelmeli
        Assert.Single(results);
        Assert.Equal("doc.pdf", results[0].FileName);
    }

    [Fact]
    public async Task AnalyzePageAsync_CategoryFilter_FiltersOutUnwantedCategories()
    {
        var html = """
            <html>
            <body>
                <img src="/img/sample.jpg" />
                <video src="/media/movie.mp4"></video>
                <a href="/pkg/setup.exe">Program</a>
                <a href="/data/bundle.7z">Arşiv</a>
            </body>
            </html>
            """;

        _server.CustomHandlers["/filtered.html"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "text/html";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(html));
        };

        var pageUrl = $"{_server.BaseUrl}filtered.html";
        var results = await _grabberService.AnalyzePageAsync(pageUrl, new SiteGrabberOptions
        {
            AllowedCategories = new HashSet<DownloadCategory> { DownloadCategory.Video, DownloadCategory.Archive },
            RespectRobotsTxt = false
        });

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Category == DownloadCategory.Video);
        Assert.Contains(results, r => r.Category == DownloadCategory.Archive);
        Assert.DoesNotContain(results, r => r.Category == DownloadCategory.Image);
        Assert.DoesNotContain(results, r => r.Category == DownloadCategory.Program);
    }

    [Fact]
    public async Task AnalyzePageAsync_Cancellation_AbortsExecutionImmediately()
    {
        _server.CustomHandlers["/hang.html"] = async (req, res) =>
        {
            await Task.Delay(2000);
            res.StatusCode = 200;
            res.ContentType = "text/html";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("<html><body>OK</body></html>"));
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _grabberService.AnalyzePageAsync($"{_server.BaseUrl}hang.html", cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task ProbeFileSizesAsync_FetchesContentLengthAndTypeViaHeadRequests()
    {
        _server.CustomHandlers["/items/big_archive.zip"] = async (req, res) =>
        {
            res.StatusCode = 200;
            res.ContentType = "application/zip";
            res.ContentLength64 = 10485760; // 10 MB
            if (req.HttpMethod == "GET")
            {
                await res.OutputStream.WriteAsync(new byte[1024]);
            }
        };

        var items = new List<GrabbedFileItem>
        {
            new()
            {
                Url = $"{_server.BaseUrl}items/big_archive.zip",
                FileName = "big_archive.zip",
                Category = DownloadCategory.Archive
            }
        };

        await _grabberService.ProbeFileSizesAsync(items, maxConcurrency: 2);

        Assert.NotNull(items[0].FileSize);
        Assert.Equal(10485760, items[0].FileSize);
        Assert.Equal("application/zip", items[0].ContentType);
    }

    [Theory]
    [InlineData("User-agent: *\nDisallow: /admin\nDisallow: /tmp/", "/admin/dash", false)]
    [InlineData("User-agent: *\nDisallow: /admin\nDisallow: /tmp/", "/public/index.html", true)]
    [InlineData("User-agent: Googlebot\nDisallow: /secret\nUser-agent: *\nDisallow: /only_all/", "/secret", true)]
    [InlineData("User-agent: Googlebot\nDisallow: /secret\nUser-agent: *\nDisallow: /only_all/", "/only_all/test", false)]
    public void RobotsTxtRules_ParseAndValidatePaths(string robotsTxt, string path, bool expectedAllowed)
    {
        var rules = new RobotsTxtRules();
        rules.Parse(robotsTxt);

        var testUri = new Uri($"https://example.com{path}");
        Assert.Equal(expectedAllowed, rules.IsAllowed(testUri));
    }
}

