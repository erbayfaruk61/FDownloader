using System.Net;
using System.Text;
using FDownloader.Core.Domain;
using FDownloader.Core.Events;
using FDownloader.Core.Network;
using FDownloader.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class NetworkProxyAndCredentialTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Database _database;
    private readonly DownloadRepository _repository;
    private readonly MockServer.TestHttpServer _server;

    public NetworkProxyAndCredentialTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fdownloader_proxy_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var dbPath = Path.Combine(_tempDir, "test.db");
        _database = new Database(dbPath, NullLogger<Database>.Instance);
        _database.Initialize();
        _repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);

        _server = new MockServer.TestHttpServer();
        _server.Start();
    }

    public void Dispose()
    {
        _server.Dispose();
        _database.Dispose();
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
    public void ProxyConfig_CreateWebProxy_WhenNone_ReturnsNull()
    {
        var config = new ProxyConfig { Mode = ProxyMode.None };
        Assert.Null(config.CreateWebProxy());
    }

    [Fact]
    public void ProxyConfig_CreateWebProxy_WhenSystem_ReturnsSystemWebProxy()
    {
        var config = new ProxyConfig { Mode = ProxyMode.System };
        var proxy = config.CreateWebProxy();
        Assert.NotNull(proxy);
    }

    [Fact]
    public void ProxyConfig_CreateWebProxy_WhenManualHttp_SetsAddressAndCredentials()
    {
        var config = new ProxyConfig
        {
            Mode = ProxyMode.Manual,
            Protocol = ProxyProtocol.Http,
            Server = "127.0.0.1",
            Port = 8888,
            Username = "proxyUser",
            Password = "proxyPassword",
            BypassLocal = true,
            BypassList = new List<string> { "localhost", "192.168.*" }
        };

        var webProxy = config.CreateWebProxy() as WebProxy;
        Assert.NotNull(webProxy);
        Assert.Equal(new Uri("http://127.0.0.1:8888"), webProxy.Address);
        Assert.NotNull(webProxy.Credentials);

        var netCred = webProxy.Credentials.GetCredential(new Uri("http://127.0.0.1:8888"), "Basic");
        Assert.NotNull(netCred);
        Assert.Equal("proxyUser", netCred.UserName);
        Assert.Equal("proxyPassword", netCred.Password);
    }

    [Fact]
    public void ProxyConfig_CreateWebProxy_WhenManualSocks5_SetsSocks5Scheme()
    {
        var config = new ProxyConfig
        {
            Mode = ProxyMode.Manual,
            Protocol = ProxyProtocol.Socks5,
            Server = "10.0.0.1",
            Port = 1080
        };

        var webProxy = config.CreateWebProxy() as WebProxy;
        Assert.NotNull(webProxy);
        Assert.Equal(new Uri("socks5://10.0.0.1:1080"), webProxy.Address);
    }

    [Fact]
    public void SiteCredentialManager_SetAndGetCredential_SupportsExactAndSubdomainMatch()
    {
        var manager = new SiteCredentialManager(NullLogger<SiteCredentialManager>.Instance);
        manager.SetCredential("example.com", "user1", "pass1", authType: "Basic");

        // Tam eşleşme
        var cred1 = manager.GetCredential("example.com");
        Assert.NotNull(cred1);
        Assert.Equal("user1", cred1.Username);
        Assert.Equal("pass1", cred1.Password);

        // Alt alan adı eşleşmesi
        var credSub = manager.GetCredential("download.example.com");
        Assert.NotNull(credSub);
        Assert.Equal("user1", credSub.Username);

        // URL formatından normalize etme
        manager.SetCredential("https://secure.site.org/downloads", "user2", "pass2", authType: "Digest");
        var cred2 = manager.GetCredential("secure.site.org");
        Assert.NotNull(cred2);
        Assert.Equal("user2", cred2.Username);
        Assert.Equal("Digest", cred2.AuthType);
    }

    [Fact]
    public void SiteCredentialManager_GetBasicAuthHeaderValue_EncodesBase64Correctly()
    {
        var manager = new SiteCredentialManager(NullLogger<SiteCredentialManager>.Instance);
        manager.SetCredential("myapi.com", "aladdin", "opensesame");

        var header = manager.GetBasicAuthHeaderValue(new Uri("https://myapi.com/v1/data"));

        // "aladdin:opensesame" -> "YWxhZGRpbjpvcGVuc2VzYW1l"
        Assert.Equal("Basic YWxhZGRpbjpvcGVuc2VzYW1l", header);
    }

    [Fact]
    public void SiteCredentialManager_SaveAndLoad_PreservesCredentialsEncryptedInRepository()
    {
        var manager1 = new SiteCredentialManager(NullLogger<SiteCredentialManager>.Instance);
        manager1.SetCredential("vault.example.com", "vaultUser", "superSecretP@ssword123", domain: "CORP", authType: "NTLM");
        manager1.SaveToRepository(_repository);

        var manager2 = new SiteCredentialManager(NullLogger<SiteCredentialManager>.Instance);
        manager2.LoadFromRepository(_repository);

        var loaded = manager2.GetCredential("vault.example.com");
        Assert.NotNull(loaded);
        Assert.Equal("vaultUser", loaded.Username);
        Assert.Equal("superSecretP@ssword123", loaded.Password);
        Assert.Equal("CORP", loaded.Domain);
        Assert.Equal("NTLM", loaded.AuthType);
    }

    [Fact]
    public void NetworkHandlerFactory_CreateHandler_ConfiguresProxyAndCredentials()
    {
        var proxyConfig = new ProxyConfig
        {
            Mode = ProxyMode.Manual,
            Protocol = ProxyProtocol.Http,
            Server = "127.0.0.1",
            Port = 8080
        };

        var credManager = new SiteCredentialManager(NullLogger<SiteCredentialManager>.Instance);
        credManager.SetCredential("target.com", "bob", "12345");

        using var handler = NetworkHandlerFactory.CreateHandler(proxyConfig, credManager);

        Assert.True(handler.UseProxy);
        Assert.NotNull(handler.Proxy);
        Assert.NotNull(handler.Credentials);
    }

    [Fact]
    public async Task SiteCredentials_AuthenticatesAgainstProtectedServerSuccessfully()
    {
        var expectedContent = "Gizli ve korumalı dosya içeriği 1234567890";
        var expectedBytes = Encoding.UTF8.GetBytes(expectedContent);

        // Korumalı uç nokta: Basic Auth (faruk:gizli123)
        _server.CustomHandlers["/protected/data.txt"] = async (req, res) =>
        {
            var authHeader = req.Headers["Authorization"];
            var validToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("faruk:gizli123"));
            var expectedHeader = $"Basic {validToken}";

            if (authHeader == null || !authHeader.Equals(expectedHeader, StringComparison.OrdinalIgnoreCase))
            {
                res.StatusCode = 401;
                res.Headers["WWW-Authenticate"] = "Basic realm=\"SecureArea\"";
                return;
            }

            res.StatusCode = 200;
            res.ContentType = "text/plain";
            res.ContentLength64 = expectedBytes.Length;
            await res.OutputStream.WriteAsync(expectedBytes);
        };

        var protectedUrl = $"{_server.BaseUrl}protected/data.txt";
        var serverUri = new Uri(protectedUrl);

        var credManager = new SiteCredentialManager(NullLogger<SiteCredentialManager>.Instance);
        credManager.SetCredential(serverUri.Host, "faruk", "gizli123");

        using var client = NetworkHandlerFactory.CreateClient(
            proxyConfig: new ProxyConfig { Mode = ProxyMode.None },
            credentialManager: credManager);

        // 1. Yetkisiz doğrudan GET denemesi (boş client ile 401 almalı)
        using var unauthClient = new HttpClient();
        var unauthRes = await unauthClient.GetAsync(protectedUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthRes.StatusCode);

        // 2. Yetkili client ile GET denemesi (SocketsHttpHandler CredentialCache ile otomatik 200 dönmeli)
        var authRes = await client.GetAsync(protectedUrl);
        Assert.Equal(HttpStatusCode.OK, authRes.StatusCode);

        var receivedString = await authRes.Content.ReadAsStringAsync();
        Assert.Equal(expectedContent, receivedString);
    }
}
