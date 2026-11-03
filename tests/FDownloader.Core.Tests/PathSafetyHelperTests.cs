using FDownloader.Core.Utilities;
using Xunit;

namespace FDownloader.Core.Tests;

public class PathSafetyHelperTests
{
    [Fact]
    public void DownloadsKlasorununKendisiAslaSilinemez()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");

        // Downloads klasörü, baseDirectory olarak Downloads verildiğinde bile ASLA silinemez
        var isSafe = PathSafetyHelper.IsSafeToDeleteDirectory(downloads, downloads);
        Assert.False(isSafe);

        // baseDirectory verilmeden de korunan klasör olduğu için ASLA silinemez
        var isSafeWithoutBase = PathSafetyHelper.IsSafeToDeleteDirectory(downloads, null);
        Assert.False(isSafeWithoutBase);
    }

    [Fact]
    public void KullaniciProfiliVeKokDizinlerAslaSilinemez()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(userProfile, null));

        var driveRoot = Path.GetPathRoot(userProfile);
        Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(driveRoot, null));

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(desktop, null));
    }

    [Fact]
    public void DownloadsAltindakiTorrentOzelKlasoruSilinebilir()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "FDownloader_Safety_Test_" + Guid.NewGuid().ToString("N"));
        var baseDir = Path.Combine(tempRoot, "Downloads");
        var torrentSubDir = Path.Combine(baseDir, "Ubuntu_24_04_ISO_Torrent");

        try
        {
            Directory.CreateDirectory(torrentSubDir);
            var testFile = Path.Combine(torrentSubDir, "ubuntu.iso");
            File.WriteAllText(testFile, "test data");

            // Alt klasörün silinmesi güvenli olmalıdır
            Assert.True(PathSafetyHelper.IsSafeToDeleteDirectory(torrentSubDir, baseDir));

            // Klasörün kendisi (baseDir) silinemez olmalıdır
            Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(baseDir, baseDir));

            // Güvenli silme işlemi çalışmalıdır
            var deleted = PathSafetyHelper.SafeDeleteDirectory(torrentSubDir, baseDir);
            Assert.True(deleted);
            Assert.False(Directory.Exists(torrentSubDir));
            Assert.True(Directory.Exists(baseDir));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void PathTraversalGirisimleriEngellenir()
    {
        var baseDir = @"C:\Users\Faruk\Downloads";
        var maliciousDir1 = @"C:\Users\Faruk\Downloads\..\Desktop";
        var maliciousDir2 = @"C:\Users\Faruk\Downloads\..\..\Windows";

        Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(maliciousDir1, baseDir));
        Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(maliciousDir2, baseDir));
    }

    [Fact]
    public void KaraListedekiSistemVeIndirilenlerKlasorAdlariAslaSilinemez()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "FDownloader_Blacklist_Test_" + Guid.NewGuid().ToString("N"));
        var downloadsDir = Path.Combine(tempRoot, "İndirilenler");
        var englishDownloadsDir = Path.Combine(tempRoot, "Downloads");
        var desktopDir = Path.Combine(tempRoot, "Masaüstü");

        try
        {
            Directory.CreateDirectory(downloadsDir);
            Directory.CreateDirectory(englishDownloadsDir);
            Directory.CreateDirectory(desktopDir);

            // Alt klasör olarak bile olsa, adı "İndirilenler" veya "Downloads" ise ASLA silinemez
            Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(downloadsDir, tempRoot));
            Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(englishDownloadsDir, tempRoot));
            Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(desktopDir, tempRoot));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void BaseDirectoryVerilmedigindeHicbirKlasorSilinemez()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "FDownloader_NoBase_Test_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempRoot, "HerhangiBirKlasor");

        try
        {
            Directory.CreateDirectory(subDir);
            // baseDirectory olmadan kesinlikle silinmemeli
            Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(subDir, null));
            Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(subDir, ""));
            Assert.False(PathSafetyHelper.IsSafeToDeleteDirectory(subDir, "   "));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
