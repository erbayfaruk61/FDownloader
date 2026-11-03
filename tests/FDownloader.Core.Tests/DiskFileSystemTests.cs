using System.Text;
using FDownloader.Core.Engine.Writer;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class DiskFileSystemTests : IDisposable
{
    private readonly string _tempDir;

    public DiskFileSystemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_DiskTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task FileWriter_Initialize_PreallocatesSpecifiedSize()
    {
        string tempFile = Path.Combine(_tempDir, "prealloc.fdown");
        long expectedSize = 5 * 1024 * 1024; // 5 MB

        await using (var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance))
        {
            await writer.InitializeAsync(expectedSize, CancellationToken.None);
        }

        Assert.True(File.Exists(tempFile));
        var fileInfo = new FileInfo(tempFile);
        Assert.Equal(expectedSize, fileInfo.Length);
    }

    [Fact]
    public async Task FileWriter_CommitAsync_AtomicallyRenamesTempToFinal()
    {
        string tempFile = Path.Combine(_tempDir, "video.mp4.fdown");
        string finalFile = Path.Combine(_tempDir, "video.mp4");
        byte[] testData = Encoding.UTF8.GetBytes("FDownloader Disk Test File Content");

        await using (var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance))
        {
            await writer.InitializeAsync(testData.Length, CancellationToken.None);
            await writer.WriteAtAsync(0, testData, testData.Length, CancellationToken.None);
            await writer.CommitAsync(finalFile, CancellationToken.None);
        }

        Assert.False(File.Exists(tempFile), "Geçici dosya taşınmış olmalı");
        Assert.True(File.Exists(finalFile), "Final dosyası mevcut olmalı");

        byte[] actual = await File.ReadAllBytesAsync(finalFile);
        Assert.Equal(testData, actual);
    }

    [Fact]
    public async Task FileWriter_CommitAsync_WhenDestinationDirectoryMissing_ThrowsDirectoryNotFound()
    {
        string tempFile = Path.Combine(_tempDir, "sample.bin.fdown");
        string nonExistentDir = Path.Combine(_tempDir, "sub_dir_" + Guid.NewGuid().ToString("N"));
        string finalFile = Path.Combine(nonExistentDir, "sample.bin");

        await using var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance);
        await writer.InitializeAsync(100, CancellationToken.None);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            writer.CommitAsync(finalFile, CancellationToken.None));
    }

    [Fact]
    public async Task FileWriter_WriteAtAsync_WritesAtCorrectOffsets()
    {
        string tempFile = Path.Combine(_tempDir, "offset_test.bin");
        byte[] chunk1 = Encoding.ASCII.GetBytes("AAAA");
        byte[] chunk2 = Encoding.ASCII.GetBytes("BBBB");

        await using (var writer = new FileWriter(tempFile, NullLogger<FileWriter>.Instance))
        {
            await writer.InitializeAsync(8, CancellationToken.None);
            // Önce 4. ofsete chunk2 yaz
            await writer.WriteAtAsync(4, chunk2, chunk2.Length, CancellationToken.None);
            // Sonra 0. ofsete chunk1 yaz
            await writer.WriteAtAsync(0, chunk1, chunk1.Length, CancellationToken.None);
        }

        string result = await File.ReadAllTextAsync(tempFile, Encoding.ASCII);
        Assert.Equal("AAAABBBB", result);
    }

    [Fact]
    public void DiskSpaceChecker_Fat32Limit_HandlesSizeThresholds()
    {
        long belowLimit = 4L * 1024 * 1024 * 1024 - 1; // 4 GB - 1

        // ExceedsFat32Limit FAT32 olmayan sürücülerde false dönmeli
        Assert.False(DiskSpaceChecker.ExceedsFat32Limit(_tempDir, belowLimit));
    }
}
