using FDownloader.Core.Engine.Probe;
using FDownloader.Core.Network;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class NetworkSecurityTests
{
    [Theory]
    [InlineData("https://example.com/login", "https://example.com/download.zip", true)]
    [InlineData("https://example.com/login", "https://cdn.example.com/file.zip", true)] // Subdomain aynı kök alan adı
    [InlineData("https://cold-1.gofile.io/download", "https://gofile.io/login", true)]
    [InlineData("https://example.com/login", "https://other.com/file.zip", false)]
    [InlineData("https://auth.bank.com", "https://malicious.org/phish", false)]
    [InlineData("https://example.com/file", "http://example.com/file", false)] // HTTPS -> HTTP downgrade engeli
    [InlineData("http://example.com/file", "https://example.com/file", true)]  // HTTP -> HTTPS upgrade izinli
    public void NetworkSecurityPolicy_ShouldPreserveSensitiveHeaders_EnforcesDomainAndSchemeRules(
        string originalUrl, string targetUrl, bool expected)
    {
        var original = new Uri(originalUrl);
        var target = new Uri(targetUrl);

        bool result = NetworkSecurityPolicy.ShouldPreserveSensitiveHeaders(original, target);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://example.com", "http://example.com", true)]
    [InlineData("http://example.com", "https://example.com", false)]
    [InlineData("https://example.com", "https://other.com", false)]
    public void NetworkSecurityPolicy_IsHttpsDowngrade_DetectsDowngrades(
        string originalUrl, string targetUrl, bool expected)
    {
        var original = new Uri(originalUrl);
        var target = new Uri(targetUrl);

        bool result = NetworkSecurityPolicy.IsHttpsDowngrade(original, target);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://example.com/file.zip", true)]
    [InlineData("http://example.com/file.zip", true)]
    [InlineData("ftp://ftp.example.com/file.zip", true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/plain;base64,SGVsbG8=", false)]
    [InlineData("", false)]
    public void NetworkSecurityPolicy_IsSafeUrlScheme_BlocksMaliciousSchemes(string url, bool expected)
    {
        bool result = NetworkSecurityPolicy.IsSafeUrlScheme(url);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DownloadProber_CrossDomainRedirect_StripsSensitiveCookies()
    {
        string? receivedCookieOnTarget = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "origin.com")
            {
                var redirectResponse = new HttpResponseMessage(System.Net.HttpStatusCode.Found);
                redirectResponse.Headers.Location = new Uri("https://cross-cdn.com/target_file.zip");
                return redirectResponse;
            }

            if (request.RequestUri?.Host == "cross-cdn.com")
            {
                if (request.Headers.TryGetValues("Cookie", out var cookieValues))
                {
                    receivedCookieOnTarget = string.Join(";", cookieValues);
                }

                var successResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                successResponse.Content = new ByteArrayContent(new byte[100]);
                successResponse.Content.Headers.ContentLength = 100;
                return successResponse;
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var prober = new DownloadProber(client, NullLogger<DownloadProber>.Instance);

        var result = await prober.ProbeAsync(
            "https://origin.com/download",
            cookies: "secret_auth_token=super_secret_cookie_123");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://cross-cdn.com/target_file.zip", result.FinalUrl);

        // Hedef domain'e (cross-cdn.com) origin.com çerezi sızmamış olmalı!
        Assert.Null(receivedCookieOnTarget);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}

