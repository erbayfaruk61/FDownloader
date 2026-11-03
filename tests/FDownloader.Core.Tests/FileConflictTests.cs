using System.IO;
using FDownloader.Core.Domain;
using FDownloader.Core.Utilities;
using Xunit;

namespace FDownloader.Core.Tests;

public class FileConflictTests
{
    [Fact]
    public void ResolveConflict_WhenFileDoesNotExist_ReturnsSameName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fdown_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var fileName = "testfile.zip";
            var result = FileNameSanitizer.ResolveConflict(tempDir, fileName);
            Assert.Equal("testfile.zip", result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveConflict_WhenFileExists_AddsNumberedSuffix()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fdown_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var fileName = "archive.zip";
            var existingFile = Path.Combine(tempDir, fileName);
            File.WriteAllText(existingFile, "content");

            var result = FileNameSanitizer.ResolveConflict(tempDir, fileName);
            Assert.Equal("archive (1).zip", result);

            // 1 numaralı dosya da varsa (2) üretmeli
            var secondFile = Path.Combine(tempDir, result);
            File.WriteAllText(secondFile, "content 2");

            var result2 = FileNameSanitizer.ResolveConflict(tempDir, fileName);
            Assert.Equal("archive (2).zip", result2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DownloadCategoryExtensions_ToCategoryIcon_ReturnsCorrectIcons()
    {
        Assert.Equal("🎬", DownloadCategory.Video.ToCategoryIcon());
        Assert.Equal("🎵", DownloadCategory.Music.ToCategoryIcon());
        Assert.Equal("📦", DownloadCategory.Archive.ToCategoryIcon());
        Assert.Equal("📄", DownloadCategory.Document.ToCategoryIcon());
        Assert.Equal("⚙️", DownloadCategory.Program.ToCategoryIcon());
    }
}

