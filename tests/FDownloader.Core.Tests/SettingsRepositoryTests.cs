using System.Text.Json;
using FDownloader.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class SettingsRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly Database _database;
    private readonly DownloadRepository _repository;

    public SettingsRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"fdownloader_settings_test_{Guid.NewGuid():N}.db");
        _database = new Database(_testDbPath, NullLogger<Database>.Instance);
        _database.Initialize();
        _repository = new DownloadRepository(_database, NullLogger<DownloadRepository>.Instance);
    }

    public void Dispose()
    {
        _database.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public void SetAndGetSetting_StandardAndSensitiveValues_WorkCorrectly()
    {
        // Standart ayar
        _repository.SetSetting("Ui.ThemeMode", "2");
        Assert.Equal("2", _repository.GetSetting("Ui.ThemeMode"));

        // Hassas ayar (DPAPI ile korunmalı, GetSetting ile çözülmeli)
        _repository.SetSetting("Proxy.Password", "SuperSecret123!");
        Assert.Equal("SuperSecret123!", _repository.GetSetting("Proxy.Password"));
    }

    [Fact]
    public void GetAllSettings_ReturnsAllStoredKeys()
    {
        _repository.SetSetting("Test.A", "ValueA");
        _repository.SetSetting("Test.B", "ValueB");
        _repository.SetSetting("Proxy.Password", "MyP@ss");

        var all = _repository.GetAllSettings();

        Assert.True(all.ContainsKey("Test.A"));
        Assert.True(all.ContainsKey("Test.B"));
        Assert.True(all.ContainsKey("Proxy.Password"));
        Assert.Equal("ValueA", all["Test.A"]);
        Assert.Equal("ValueB", all["Test.B"]);
        Assert.Equal("MyP@ss", all["Proxy.Password"]);
    }

    [Fact]
    public void ClearAllSettings_RemovesAllKeys()
    {
        _repository.SetSetting("Test.X", "100");
        _repository.SetSetting("Test.Y", "200");

        Assert.NotEmpty(_repository.GetAllSettings());

        _repository.ClearAllSettings();

        Assert.Empty(_repository.GetAllSettings());
        Assert.Null(_repository.GetSetting("Test.X"));
    }

    [Fact]
    public void ImportSettings_RestoresConfigurationCorrectly()
    {
        var backup = new Dictionary<string, string>
        {
            ["Download.DefaultSaveDirectory"] = @"C:\TestDownloads",
            ["Ui.ThemeMode"] = "1",
            ["Network.GlobalDownloadLimitKiB"] = "512"
        };

        var json = JsonSerializer.Serialize(backup);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;

        _repository.ImportSettings(deserialized);

        Assert.Equal(@"C:\TestDownloads", _repository.GetSetting("Download.DefaultSaveDirectory"));
        Assert.Equal("1", _repository.GetSetting("Ui.ThemeMode"));
        Assert.Equal("512", _repository.GetSetting("Network.GlobalDownloadLimitKiB"));
    }
}

