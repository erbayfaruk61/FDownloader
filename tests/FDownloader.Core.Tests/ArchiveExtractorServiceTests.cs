using System.IO.Compression;
using FDownloader.Core.Utilities;
using SharpCompress.Archives;
using Xunit;

namespace FDownloader.Core.Tests;

public class ArchiveExtractorServiceTests : IDisposable
{
    private readonly string _testDir;

    public ArchiveExtractorServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "fdownloader_archive_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Theory]
    [InlineData("test.zip", true)]
    [InlineData("test.rar", true)]
    [InlineData("test.7z", true)]
    [InlineData("test.tar", true)]
    [InlineData("test.tar.gz", true)]
    [InlineData("game.part01.rar", true)]
    [InlineData("game.7z.001", true)]
    [InlineData("video.mp4", false)]
    [InlineData("document.pdf", false)]
    [InlineData("app.exe", false)]
    public void IsArchive_CorrectlyIdentifiesArchiveFiles(string fileName, bool expected)
    {
        var result = ArchiveExtractorService.IsArchive(fileName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("game.part01.rar", true)]
    [InlineData("game.part02.rar", false)]
    [InlineData("game.part10.rar", false)]
    [InlineData("backup.7z.001", true)]
    [InlineData("backup.7z.002", false)]
    [InlineData("standalone.zip", true)]
    [InlineData("standalone.rar", true)]
    public void IsFirstVolumeOrStandalone_HandlesMultiPartVolumesCorrectly(string fileName, bool expected)
    {
        var result = ArchiveExtractorService.IsFirstVolumeOrStandalone(fileName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("game.part01.rar", "game")]
    [InlineData("backup.7z.001", "backup")]
    [InlineData("my_photos.zip", "my_photos")]
    public void GetCleanArchiveFolderName_StripsMultiPartSuffixes(string fileName, string expected)
    {
        var result = ArchiveExtractorService.GetCleanArchiveFolderName(fileName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsZipArchiveSuccessfully()
    {
        // 1. Create a dummy test zip
        var sourceFolder = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllText(Path.Combine(sourceFolder, "file1.txt"), "Hello FDownloader");
        File.WriteAllText(Path.Combine(sourceFolder, "file2.txt"), "Second content");

        var zipPath = Path.Combine(_testDir, "test_archive.zip");
        ZipFile.CreateFromDirectory(sourceFolder, zipPath);

        // 2. Extract using ArchiveExtractorService
        var extractor = new ArchiveExtractorService();
        var destFolder = Path.Combine(_testDir, "extracted");
        var result = await extractor.ExtractAsync(zipPath, destFolder, createSubfolder: true);

        // 3. Verify
        Assert.True(result.Success);
        Assert.Equal(2, result.ExtractedFilesCount);
        Assert.NotNull(result.ExtractedDirectory);
        Assert.True(Directory.Exists(result.ExtractedDirectory));
        Assert.True(File.Exists(Path.Combine(result.ExtractedDirectory, "file1.txt")));
        Assert.Equal("Hello FDownloader", File.ReadAllText(Path.Combine(result.ExtractedDirectory, "file1.txt")));
    }

    [Fact]
    public async Task ExtractAsync_NonExistentFile_ReturnsTurkishError()
    {
        var extractor = new ArchiveExtractorService();
        var result = await extractor.ExtractAsync(Path.Combine(_testDir, "does_not_exist.zip"));
        Assert.False(result.Success);
        Assert.Contains("Arşiv dosyası bulunamadı", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractAsync_CorruptedArchive_ReturnsTurkishError()
    {
        var corruptedFile = Path.Combine(_testDir, "corrupt.zip");
        await File.WriteAllBytesAsync(corruptedFile, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 });

        var extractor = new ArchiveExtractorService();
        var result = await extractor.ExtractAsync(corruptedFile);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.DoesNotContain("Winzip AES authentication failed", result.ErrorMessage);
    }
}
