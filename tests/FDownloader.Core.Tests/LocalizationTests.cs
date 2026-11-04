using FDownloader.Core.Localization;
using Xunit;

namespace FDownloader.Core.Tests;

public class LocalizationTests
{
    [Fact]
    public void LocalizationService_DefaultLanguage_IsTurkish()
    {
        var loc = LocalizationService.Instance;
        loc.CurrentLanguage = LocalizationService.DefaultLanguage;

        Assert.Equal("tr-TR", loc.CurrentLanguage);
        Assert.True(loc.IsTurkish);
        Assert.False(loc.IsEnglish);

        // Türkçe dizge kontrolleri
        Assert.Equal("➕ Yeni İndirme", loc["Toolbar.NewDownload"]);
        Assert.Equal("Ayarlar", loc["Common.Settings"]);
        Assert.Equal("Dosya Adı", loc["Grid.FileName"]);
        Assert.Equal("FDownloader Kurulum Sihirbazı", loc["Wizard.Title"]);
    }

    [Fact]
    public void LocalizationService_SwitchToEnglish_SwitchesStringsDynamically()
    {
        var loc = LocalizationService.Instance;
        loc.CurrentLanguage = LocalizationService.EnglishLanguage;

        Assert.Equal("en-US", loc.CurrentLanguage);
        Assert.True(loc.IsEnglish);
        Assert.False(loc.IsTurkish);

        // İngilizce dizge kontrolleri
        Assert.Equal("➕ New Download", loc["Toolbar.NewDownload"]);
        Assert.Equal("Settings", loc["Common.Settings"]);
        Assert.Equal("File Name", loc["Grid.FileName"]);
        Assert.Equal("FDownloader Setup Wizard", loc["Wizard.Title"]);

        // Tekrar Türkçe'ye dön
        loc.CurrentLanguage = LocalizationService.DefaultLanguage;
        Assert.Equal("tr-TR", loc.CurrentLanguage);
        Assert.Equal("➕ Yeni İndirme", loc["Toolbar.NewDownload"]);
    }

    [Fact]
    public void LocalizationService_MissingKey_ReturnsKeyFallback()
    {
        var loc = LocalizationService.Instance;
        var nonExistentKey = "Non.Existent.String.Key";

        var result = loc[nonExistentKey];
        Assert.Equal(nonExistentKey, result);
    }

    [Fact]
    public void LocalizationService_DetectSystemLanguage_ReturnsValidCode()
    {
        var detected = LocalizationService.DetectSystemLanguage();
        Assert.True(detected == LocalizationService.DefaultLanguage || detected == LocalizationService.EnglishLanguage);
    }
}

