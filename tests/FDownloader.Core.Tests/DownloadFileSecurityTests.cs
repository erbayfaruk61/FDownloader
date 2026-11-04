using System.Runtime.InteropServices;
using FDownloader.Core.Security;
using Xunit;

namespace FDownloader.Core.Tests;

public class DownloadFileSecurityTests : IDisposable
{
    private readonly string _tempDir;

    public DownloadFileSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fdownloader_filesec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Theory]
    [InlineData("setup.exe", true)]
    [InlineData("script.bat", true)]
    [InlineData("payload.cmd", true)]
    [InlineData("screen.scr", true)]
    [InlineData("code.js", true)]
    [InlineData("runner.vbs", true)]
    [InlineData("automate.ps1", true)]
    [InlineData("installer.msi", true)]
    [InlineData("archive.zip", false)]
    [InlineData("video.mp4", false)]
    [InlineData("music.mp3", false)]
    [InlineData("document.pdf", false)]
    [InlineData("image.png", false)]
    [InlineData("text.txt", false)]
    public void IsDangerousFileType_CorrectlyIdentifiesDangerousAndSafeExtensions(string fileName, bool expected)
    {
        bool result = DownloadFileSecurity.IsDangerousFileType(fileName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyZoneIdentifier_WritesZoneId3ToNtfsAlternateDataStream()
    {
        var filePath = Path.Combine(_tempDir, "test_download.pdf");
        File.WriteAllText(filePath, "%PDF-1.4 test dummy content");

        bool applied = DownloadFileSecurity.ApplyZoneIdentifier(
            filePath,
            referrerUrl: "https://example.com/page",
            hostUrl: "https://example.com/files/test_download.pdf");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(applied);
            Assert.True(DownloadFileSecurity.HasZoneIdentifier(filePath));

            // Doğrudan ADS akışını oku ve doğrula
            string adsPath = filePath + ":Zone.Identifier";
            Assert.True(File.Exists(adsPath));
            string content = File.ReadAllText(adsPath);
            Assert.Contains("[ZoneTransfer]", content);
            Assert.Contains("ZoneId=3", content);
            Assert.Contains("ReferrerUrl=https://example.com/page", content);
            Assert.Contains("HostUrl=https://example.com/files/test_download.pdf", content);
        }
    }
}

