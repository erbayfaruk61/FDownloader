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

        if (!bridge.IsRunning)
        {
            // Port yetki veya dinleme engeli varsa testi atla
            return;
        }

        using var client = new HttpClient();
        var content = JsonContent.Create(new { version = "1.2.3", name = "TestExtension" });
        var response = await client.PostAsync("http://127.0.0.1:9666/ping", content);

        Assert.True(response.IsSuccessStatusCode);
        await Task.Delay(100);

        Assert.NotEmpty(heartbeatEvents);
        Assert.Equal("1.2.3", heartbeatEvents[0].Version);
    }
}

