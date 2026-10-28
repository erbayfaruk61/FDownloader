namespace FDownloader.Core.Utilities;

/// <summary>
/// Disk alanı kontrolü yardımcısı.
/// </summary>
public static class DiskSpaceChecker
{
    /// <summary>
    /// Belirtilen dizinde en az requiredBytes boş alan var mı?
    /// </summary>
    public static bool HasEnoughSpace(string directory, long requiredBytes)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(directory) ?? directory);
            return driveInfo.AvailableFreeSpace >= requiredBytes;
        }
        catch
        {
            return true; // Kontrol yapılamazsa geçmesine izin ver
        }
    }

    /// <summary>
    /// Belirtilen dizindeki sürücünün mevcut boş alanını döner.
    /// </summary>
    public static long GetAvailableSpace(string directory)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(directory) ?? directory);
            return driveInfo.AvailableFreeSpace;
        }
        catch
        {
            return long.MaxValue;
        }
    }

    /// <summary>
    /// Hedef klasörün dosya sistemini kontrol eder (FAT32 → 4GB sınırı).
    /// </summary>
    public static bool IsFat32(string directory)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(directory) ?? directory);
            return driveInfo.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private const long Fat32MaxFileSize = 4L * 1024 * 1024 * 1024 - 1; // 4 GB - 1 byte

    /// <summary>
    /// FAT32 sınırını aşan dosya boyutu var mı?
    /// </summary>
    public static bool ExceedsFat32Limit(string directory, long fileSize) =>
        IsFat32(directory) && fileSize > Fat32MaxFileSize;

    /// <summary>
    /// Kullanıcı dostu disk boş alan özeti döner (Örn: "Disk: C:\ -> 75,4 GB").
    /// </summary>
    public static string GetDriveInfoSummary(string? directory = null)
    {
        try
        {
            directory ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var root = Path.GetPathRoot(directory) ?? "C:\\";
            var drive = new DriveInfo(root);
            return $"Disk: {drive.Name} -> {SpeedCalculator.FormatBytes(drive.AvailableFreeSpace)}";
        }
        catch
        {
            return "Disk: Bilinmiyor";
        }
    }
}
