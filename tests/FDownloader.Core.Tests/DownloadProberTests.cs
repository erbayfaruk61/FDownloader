using System.Net;
using FDownloader.Core.Engine.Probe;
using FDownloader.Core.Tests.MockServer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class DownloadProberTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly HttpClient _client;
    private readonly DownloadProber _prober;

    public DownloadProberTests()
    {
        _server = new TestHttpServer();
        _server.Start();
        _client = new HttpClient();
        _prober = new DownloadProber(_client, NullLogger<DownloadProber>.Instance);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task Scenario02_NoRange_ProbeIdentifiesNoRangeSupport()
    {
        string url = $"{_server.BaseUrl}no-range";
        var result = await _prober.ProbeAsync(url);

        Assert.True(result.IsSuccess);
        Assert.False(result.SupportsRange);
        Assert.Equal(TestHttpServer.NormalContent.Length, result.ContentLength);
    }

    [Fact]
    public async Task Scenario03_Chunked_ProbeIdentifiesUnknownLength()
    {
        string url = $"{_server.BaseUrl}chunked";
        var result = await _prober.ProbeAsync(url);

        Assert.True(result.IsSuccess);
        Assert.False(result.SupportsRange);
        Assert.Equal(-1, result.ContentLength);
    }

    [Fact]
    public async Task Scenario07_RedirectChain_FollowsAndUpdatesFinalUrl()
    {
        string url = $"{_server.BaseUrl}redirect-1";
        var result = await _prober.ProbeAsync(url);

        Assert.True(result.IsSuccess);
        Assert.Contains("normal", result.FinalUrl);
        Assert.True(result.SupportsRange);
        Assert.Equal(TestHttpServer.NormalContent.Length, result.ContentLength);
    }

    [Fact]
    public async Task Scenario07_RedirectLoop_FailsGracefully()
    {
        string url = $"{_server.BaseUrl}redirect-loop";
        var result = await _prober.ProbeAsync(url);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Yönlendirme döngüsü", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario09_ContentDisposition_Simple_ExtractsFileName()
    {
        string url = $"{_server.BaseUrl}disposition-simple";
        var result = await _prober.ProbeAsync(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("setup_simple.exe", result.FileName);
    }

    [Fact]
    public async Task Scenario09_ContentDisposition_Utf8_ExtractsFileName()
    {
        string url = $"{_server.BaseUrl}disposition-utf8";
        var result = await _prober.ProbeAsync(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("türkçe_şarkı.mp3", result.FileName);
    }

    [Fact]
    public async Task Scenario09_ContentDisposition_Malicious_SanitizesFileName()
    {
        string url = $"{_server.BaseUrl}disposition-malicious";
        var result = await _prober.ProbeAsync(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("evil.exe", result.FileName);
    }

    [Fact]
    public async Task QueryParameter_FileName_ExtractedWhenPathLacksExtension()
    {
        string url = $"{_server.BaseUrl}download?file=belge.pdf";
        var result = await _prober.ProbeAsync(url);

        Assert.Equal("belge.pdf", result.FileName);
    }
}
