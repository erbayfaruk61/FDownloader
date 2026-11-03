using System.Text;
using FDownloader.Core.Utilities;
using Xunit;

namespace FDownloader.Core.Tests;

public class HashCalculatorTests
{
    [Fact]
    public async Task ComputeHashesAsync_ValidFile_ReturnsCorrectHashes()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = "FDownloader Unit Test Content 12345";
            await File.WriteAllTextAsync(tempFile, content, Encoding.UTF8);

            var result = await HashCalculator.ComputeHashesAsync(tempFile);

            Assert.False(string.IsNullOrWhiteSpace(result.Md5));
            Assert.False(string.IsNullOrWhiteSpace(result.Sha1));
            Assert.False(string.IsNullOrWhiteSpace(result.Sha256));

            // MD5 length is 32 hex chars, SHA1 is 40, SHA256 is 64
            Assert.Equal(32, result.Md5.Length);
            Assert.Equal(40, result.Sha1.Length);
            Assert.Equal(64, result.Sha256.Length);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ComputeHashesAsync_EmptyFile_ReturnsKnownEmptyHashes()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, Array.Empty<byte>());

            var result = await HashCalculator.ComputeHashesAsync(tempFile);

            // Empty string MD5: d41d8cd98f00b204e9800998ecf8427e
            Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result.Md5.ToLowerInvariant());
            // Empty string SHA-1: da39a3ee5e6b4b0d3255bfef95601890afd80709
            Assert.Equal("da39a3ee5e6b4b0d3255bfef95601890afd80709", result.Sha1.ToLowerInvariant());
            // Empty string SHA-256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result.Sha256.ToLowerInvariant());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

