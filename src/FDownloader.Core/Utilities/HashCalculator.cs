using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FDownloader.Core.Utilities;

public record FileHashes(string Md5, string Sha1, string Sha256);

public static class HashCalculator
{
    /// <summary>
    /// Büyük dosyaları bellek taşırmadan (stream ile) okuyarak MD5, SHA-1 ve SHA-256 hash'lerini hesaplar.
    /// </summary>
    public static async Task<FileHashes> ComputeHashesAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Doğrulanacak dosya bulunamadı", filePath);

        using var md5 = MD5.Create();
        using var sha1 = SHA1.Create();
        using var sha256 = SHA256.Create();

        const int bufferSize = 256 * 1024; // 256 KB tampon
        var buffer = new byte[bufferSize];

        var fileInfo = new FileInfo(filePath);
        long totalBytes = fileInfo.Length;
        long bytesReadTotal = 0;

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)) > 0)
        {
            bytesReadTotal += bytesRead;
            cancellationToken.ThrowIfCancellationRequested();

            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
            sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);

            if (totalBytes > 0 && progress != null)
            {
                progress.Report((double)bytesReadTotal / totalBytes * 100.0);
            }
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        var md5Str = Convert.ToHexString(md5.Hash!).ToLowerInvariant();
        var sha1Str = Convert.ToHexString(sha1.Hash!).ToLowerInvariant();
        var sha256Str = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

        return new FileHashes(md5Str, sha1Str, sha256Str);
    }
}

