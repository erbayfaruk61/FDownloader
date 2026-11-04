using FDownloader.Core.Domain;
using FDownloader.Core.Security;
using FDownloader.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class SensitiveDataStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Database _database;
    private readonly string _dbPath;

    public SensitiveDataStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fdownloader_sec_store_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "security_test.db");

        _database = new Database(_dbPath, NullLogger<Database>.Instance);
        _database.Initialize();
    }

    public void Dispose()
    {
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
    public void SecureDataProtector_EncryptsAndDecryptsAccurately()
    {
        var protector = new SecureDataProtector();
        const string secret = "session_token_12345!@#$SpecialChars";

        string protectedText = protector.Protect(secret);
        Assert.NotEqual(secret, protectedText);
        Assert.True(protector.IsProtected(protectedText));
        Assert.StartsWith("ENC::v1::", protectedText);

        string restored = protector.Unprotect(protectedText);
        Assert.Equal(secret, restored);
    }

    [Fact]
    public void SecureDataProtector_Unprotect_ReturnsPlainText_WhenNotEncrypted()
    {
        var protector = new SecureDataProtector();
        const string legacyPlainText = "old_unencrypted_cookie_value";

        Assert.False(protector.IsProtected(legacyPlainText));
        string result = protector.Unprotect(legacyPlainText);
        Assert.Equal(legacyPlainText, result);
    }

    [Fact]
    public void DownloadRepository_CookiesAreEncryptedInSqliteDatabase()
    {
        var repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);

        const string sensitiveCookie = "sessionid=secret_cookie_value_98765; auth=token_abcdef";
        var task = new DownloadTask
        {
            OriginalUrl = "https://example.com/file.zip",
            FileName = "file.zip",
            SaveDirectory = _tempDir,
            Cookies = sensitiveCookie,
            Status = DownloadStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        long taskId = repository.AddTask(task);

        // 1. Repository üzerinden okunduğunda otomatik çözülmeli
        var loadedTask = repository.GetTask(taskId);
        Assert.NotNull(loadedTask);
        Assert.Equal(sensitiveCookie, loadedTask.Cookies);

        // 2. Ham SQL ile doğrudan SQLite tablosundan okunduğunda şifrelenmiş olmalı, düz metin olmamalı!
        using var cmd = _database.CreateCommand();
        cmd.CommandText = "SELECT Cookies FROM DownloadTasks WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", taskId);
        string? rawDbCookie = cmd.ExecuteScalar()?.ToString();

        Assert.NotNull(rawDbCookie);
        Assert.StartsWith("ENC::v1::", rawDbCookie);
        Assert.DoesNotContain("secret_cookie_value_98765", rawDbCookie);
        Assert.DoesNotContain("token_abcdef", rawDbCookie);
    }

    [Fact]
    public void DownloadRepository_SensitiveSettingsAreEncryptedInSqliteDatabase()
    {
        var repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);

        const string proxyPassword = "SuperSecretProxyPassword#2026";
        repository.SetSetting("Proxy.Password", proxyPassword);

        // Normal okuma şifreyi çözer
        string? retrieved = repository.GetSetting("Proxy.Password");
        Assert.Equal(proxyPassword, retrieved);

        // Ham SQL ile okuma düz metin olmamalı, ENC::v1:: ön eki ile şifreli saklanmalı
        using var cmd = _database.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = 'Proxy.Password';";
        string? rawDbValue = cmd.ExecuteScalar()?.ToString();

        Assert.NotNull(rawDbValue);
        Assert.StartsWith("ENC::v1::", rawDbValue);
        Assert.DoesNotContain("SuperSecretProxyPassword#2026", rawDbValue);
    }
}
