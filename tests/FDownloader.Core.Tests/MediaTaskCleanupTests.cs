using System.IO;
using FDownloader.Core.Domain;
using FDownloader.Core.Utilities;
using Xunit;

namespace FDownloader.Core.Tests;

public class MediaTaskCleanupTests
{
    [Fact]
    public void MediaCleanup_ShouldIdentifyAndRemoveAllYtDlpStreamFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_Test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var baseTitle = "Elraenn - GÜVEN BİTTİ, ENTRİKALAR BAŞLADI!_GOOSE GOOSE DUCK_BÖLÜM 20";
            var targetFileName = $"{baseTitle}.mp4";

            // Simüle edilen yt-dlp ve ffmpeg akış dosyaları
            var filesToCreate = new[]
            {
                Path.Combine(tempDir, $"{baseTitle}.f400.mp4"),
                Path.Combine(tempDir, $"{baseTitle}.f400.mp4.part"),
                Path.Combine(tempDir, $"{baseTitle}.f140.m4a"),
                Path.Combine(tempDir, $"{baseTitle}.f140.m4a.part"),
                Path.Combine(tempDir, $"{baseTitle}.temp.mp4"),
                Path.Combine(tempDir, $"{baseTitle}.mp4.part"),
                Path.Combine(tempDir, $"{baseTitle}.mp4")
            };

            // Başka bir bölüme ait dosya (yanlışlıkla silinmemeli!)
            var unrelatedFile = Path.Combine(tempDir, $"{baseTitle}0.mp4");

            foreach (var f in filesToCreate)
            {
                File.WriteAllText(f, "test stream content");
            }
            File.WriteAllText(unrelatedFile, "unrelated content");

            // Act: Simüle edilen temizleme algoritması
            var baseName = Path.GetFileNameWithoutExtension(targetFileName);
            var candidateFiles = Directory.GetFiles(tempDir, $"{baseName}*");
            foreach (var f in candidateFiles)
            {
                var fName = Path.GetFileName(f);
                if (fName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase) ||
                    fName.StartsWith(baseName + "-", StringComparison.OrdinalIgnoreCase) ||
                    fName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    PathSafetyHelper.SafeDeleteFile(f);
                }
            }

            // Assert
            foreach (var f in filesToCreate)
            {
                Assert.False(File.Exists(f), $"Dosya silinmiş olmalıydı: {Path.GetFileName(f)}");
            }

            // İlişkisiz dosya korunmalı
            Assert.True(File.Exists(unrelatedFile), "Farklı bölüme ait dosya yanlışlıkla silinmemeli");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
