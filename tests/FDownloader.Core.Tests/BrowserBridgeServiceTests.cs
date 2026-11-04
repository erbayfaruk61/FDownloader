using System.Net;
using System.Net.Http.Json;
using FDownloader.Core.Events;
using FDownloader.Core.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class BrowserBridgeServiceTests
{
    [Fact]
    public async Task PingIstegiExtensionHeartbeatEventYayinlar()
    {
        var eventBus = new DownloadEventBus();
        var heartbeatEvents = new List<BrowserExtensionHeartbeatEvent>();
        eventBus.Subscribe<BrowserExtensionHeartbeatEvent>(e => heartbeatEvents.Add(e));

        using var bridge = new BrowserBridgeService(eventBus, NullLogger<BrowserBridgeService>.Instance);
        bridge.Start();

        if (!bridge.IsRunning) return;

        using var client = new HttpClient();
        var content = JsonContent.Create(new { version = "1.2.3", name = "TestExtension" });
        var response = await client.PostAsync("http://127.0.0.1:9666/ping", content);

        Assert.True(response.IsSuccessStatusCode);
        await Task.Delay(100);

        Assert.NotEmpty(heartbeatEvents);
        Assert.Equal("1.2.3", heartbeatEvents[0].Version);
    }

    [Fact]
    public async Task IndirmeIstegiTumParametrelerleYakalanir()
    {
        var eventBus = new DownloadEventBus();
        var capturedEvents = new List<BrowserDownloadCapturedEvent>();
        eventBus.Subscribe<BrowserDownloadCapturedEvent>(e => capturedEvents.Add(e));

        using var bridge = new BrowserBridgeService(eventBus, NullLogger<BrowserBridgeService>.Instance);
        bridge.Start();

        if (!bridge.IsRunning) return;

        using var client = new HttpClient();
        var payload = new BrowserDownloadRequest
        {
            Url = "https://example.com/downloads/archive.7z",
            FileName = "archive.7z",
            Referrer = "https://example.com/download-page",
            Cookies = "sessionid=xyz123; auth=token_abc",
            UserAgent = "Mozilla/5.0 FDownloaderTest",
            TotalBytes = 10485760
        };

        var response = await client.PostAsync("http://127.0.0.1:9666/download", JsonContent.Create(payload));
        Assert.True(response.IsSuccessStatusCode);
        await Task.Delay(100);

        Assert.Single(capturedEvents);
        var captured = capturedEvents[0];
        Assert.Equal("https://example.com/downloads/archive.7z", captured.Url);
        Assert.Equal("archive.7z", captured.FileName);
        Assert.Equal("https://example.com/download-page", captured.Referrer);
        Assert.Equal("sessionid=xyz123; auth=token_abc", captured.Cookies);
        Assert.Equal("Mozilla/5.0 FDownloaderTest", captured.UserAgent);
        Assert.Equal(10485760, captured.TotalBytes);
    }

    [Fact]
    public async Task FirefoxVeEdgeOriginIstekleriKabulEdilir()
    {
        var eventBus = new DownloadEventBus();
        using var bridge = new BrowserBridgeService(eventBus, NullLogger<BrowserBridgeService>.Instance);
        bridge.Start();

        if (!bridge.IsRunning) return;

        using var client = new HttpClient();

        // Firefox moz-extension://
        using var mozRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:9666/health");
        mozRequest.Headers.Add("Origin", "moz-extension://a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var mozResponse = await client.SendAsync(mozRequest);
        Assert.True(mozResponse.IsSuccessStatusCode);
        Assert.Equal("moz-extension://a1b2c3d4-e5f6-7890-abcd-ef1234567890", mozResponse.Headers.GetValues("Access-Control-Allow-Origin").First());

        // Chromium chrome-extension://
        using var chromeRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:9666/health");
        chromeRequest.Headers.Add("Origin", "chrome-extension://abcdefghijklmnopabcdefghijklmnop");
        var chromeResponse = await client.SendAsync(chromeRequest);
        Assert.True(chromeResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task YetkisizWebSiteOriginReddedilir()
    {
        var eventBus = new DownloadEventBus();
        using var bridge = new BrowserBridgeService(eventBus, NullLogger<BrowserBridgeService>.Instance);
        bridge.Start();

        if (!bridge.IsRunning) return;

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:9666/download");
        request.Headers.Add("Origin", "https://malicious-website.com");
        request.Content = JsonContent.Create(new { url = "https://example.com/test.zip" });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GecersizUrlSemasiReddedilir()
    {
        var eventBus = new DownloadEventBus();
        using var bridge = new BrowserBridgeService(eventBus, NullLogger<BrowserBridgeService>.Instance);
        bridge.Start();

        if (!bridge.IsRunning) return;

        using var client = new HttpClient();
        var payload = new BrowserDownloadRequest
        {
            Url = "javascript:alert(1)"
        };

        var response = await client.PostAsync("http://127.0.0.1:9666/download", JsonContent.Create(payload));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
