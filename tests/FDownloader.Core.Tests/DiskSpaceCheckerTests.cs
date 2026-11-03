using FDownloader.Core.Utilities;
using Xunit;

namespace FDownloader.Core.Tests;

public class DiskSpaceCheckerTests
{
    [Fact]
    public void GetAvailableSpace_GecerliDizindeSifirdanBuyukAlanDoner()
    {
        var tempPath = Path.GetTempPath();
        var freeBytes = DiskSpaceChecker.GetAvailableSpace(tempPath);
        Assert.True(freeBytes > 0);
    }

    [Fact]
    public void HasEnoughSpace_KucukBoyutIcinTrueDoner()
    {
        var tempPath = Path.GetTempPath();
        // 1024 byte boş alan her zaman olmalıdır
        var hasSpace = DiskSpaceChecker.HasEnoughSpace(tempPath, 1024);
        Assert.True(hasSpace);
    }

    [Fact]
    public void HasEnoughSpace_AsiriBuyukBoyutIcinFalseDoner()
    {
        var tempPath = Path.GetTempPath();
        // 500 Terabyte boş alan olamaz
        long hugeSize = 500L * 1024 * 1024 * 1024 * 1024;
        var hasSpace = DiskSpaceChecker.HasEnoughSpace(tempPath, hugeSize);
        Assert.False(hasSpace);
    }

    [Fact]
    public void GetDriveInfoSummary_BicimlendirilmisMetinUretir()
    {
        var tempPath = Path.GetTempPath();
        var summary = DiskSpaceChecker.GetDriveInfoSummary(tempPath);
        Assert.NotNull(summary);
        Assert.StartsWith("Disk:", summary);
    }
}
