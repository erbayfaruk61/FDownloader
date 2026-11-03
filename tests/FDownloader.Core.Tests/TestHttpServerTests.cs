using System.Net;
using System.Security.Cryptography;
using FDownloader.Core.Tests.MockServer;
using Xunit;

namespace FDownloader.Core.Tests;

public class TestHttpServerTests : IDisposable
{
    private readonly TestHttpServer _server;
    private readonly HttpClient _client;

    public TestHttpServerTests()
    {
        _server = new TestHttpServer();
        _server.Start();
        _client = new HttpClient { BaseAddress = new Uri(_server.BaseUrl) };
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task Scenario01_NormalWithRange_ReturnsPartialContent()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "normal");
        req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 9);
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.PartialContent, res.StatusCode);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal(10, bytes.Length);
    }

    [Fact]
    public async Task Scenario02_NoRange_Returns200Ok()
    {
        var res = await _client.GetAsync("no-range");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("none", res.Headers.AcceptRanges.ToString());
    }

    [Fact]
    public async Task Scenario03_ChunkedStream_ReadsChunks()
    {
        var res = await _client.GetAsync("chunked");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var content = await res.Content.ReadAsStringAsync();
        Assert.Contains("chunk_0_data", content);
    }

    [Fact]
    public async Task Scenario04_SlowResponse_DeliversContent()
    {
        var res = await _client.GetAsync("slow");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal(TestHttpServer.NormalContent.Length, bytes.Length);
    }

    [Fact]
    public async Task Scenario05_AbortedConnection_ThrowsOrFails()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var res = await _client.GetAsync("abort");
            await res.Content.ReadAsByteArrayAsync();
        });
    }

    [Fact]
    public async Task Scenario06_WrongLength_ReturnsSpecifiedHeader()
    {
        // HeadersRead kullanarak soketin eksik bayt beklemesini önlüyoruz
        var res = await _client.GetAsync("wrong-length", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(TestHttpServer.NormalContent.Length + 500, res.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Scenario07_RedirectChain_FollowsSuccessfully()
    {
        var res = await _client.GetAsync("redirect-1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var content = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal(TestHttpServer.NormalContent.Length, content.Length);
    }

    [Fact]
    public async Task Scenario08_ErrorInjection_ReturnsExpectedStatus()
    {
        var res429 = await _client.GetAsync("error-429");
        Assert.Equal((HttpStatusCode)429, res429.StatusCode);
        Assert.NotNull(res429.Headers.RetryAfter);

        var res500 = await _client.GetAsync("error-500");
        Assert.Equal(HttpStatusCode.InternalServerError, res500.StatusCode);
    }

    [Fact]
    public async Task Scenario09_ContentDisposition_ReturnsHeaders()
    {
        var resSimple = await _client.GetAsync("disposition-simple");
        Assert.Contains("setup_simple.exe", resSimple.Content.Headers.ContentDisposition?.FileName);

        var resUtf8 = await _client.GetAsync("disposition-utf8");
        Assert.NotNull(resUtf8.Content.Headers.ContentDisposition?.FileNameStar);

        var resMalicious = await _client.GetAsync("disposition-malicious");
        Assert.Contains("evil.exe", resMalicious.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task Scenario10_BasicAuth_RequiresCredentials()
    {
        var resUnauthorized = await _client.GetAsync("auth-basic");
        Assert.Equal(HttpStatusCode.Unauthorized, resUnauthorized.StatusCode);

        var reqAuthorized = new HttpRequestMessage(HttpMethod.Get, "auth-basic");
        reqAuthorized.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "YWRtaW46c2VjcmV0");
        var resAuthorized = await _client.SendAsync(reqAuthorized);
        Assert.Equal(HttpStatusCode.OK, resAuthorized.StatusCode);
    }

    [Fact]
    public async Task Scenario11_CookieAndReferer_EnforcesSecurity()
    {
        var resDenied = await _client.GetAsync("require-cookie-referer");
        Assert.Equal(HttpStatusCode.Forbidden, resDenied.StatusCode);

        var reqValid = new HttpRequestMessage(HttpMethod.Get, "require-cookie-referer");
        reqValid.Headers.Add("Cookie", "session_token=valid123");
        reqValid.Headers.Referrer = new Uri("http://example.com");
        var resValid = await _client.SendAsync(reqValid);
        Assert.Equal(HttpStatusCode.OK, resValid.StatusCode);
    }

    [Fact]
    public async Task Scenario12_DynamicFile_ReportsEtagChanges()
    {
        _server.DynamicEtag = "\"version-1\"";
        _server.DynamicContent = "data1";
        var res1 = await _client.GetAsync("dynamic-file");
        Assert.Equal("\"version-1\"", res1.Headers.ETag?.Tag);

        _server.DynamicEtag = "\"version-2\"";
        _server.DynamicContent = "data2";
        var res2 = await _client.GetAsync("dynamic-file");
        Assert.Equal("\"version-2\"", res2.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Scenario13_LargeDeterministicStream_VerifiesCorrectLength()
    {
        var res = await _client.GetAsync("large-stream", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(1024 * 1024, res.Content.Headers.ContentLength);

        await using var stream = await res.Content.ReadAsStreamAsync();
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);
    }
}

