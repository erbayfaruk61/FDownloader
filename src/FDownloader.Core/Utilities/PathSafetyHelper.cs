namespace FDownloader.Core.Utilities;

/// <summary>
/// Dosya ve dizin silme işlemlerinde veri güvenliğini sağlayan yardımcı sınıf.
/// İndirilenler (Downloads), Masaüstü veya Kök dizinlerin yanlışlıkla silinmesini kesin olarak engeller.
/// </summary>
public static class PathSafetyHelper
{
    private static readonly HashSet<string> ProtectedDirectories = new(StringComparer.OrdinalIgnoreCase);

    static PathSafetyHelper()
    {
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.System));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        AddProtectedPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AddProtectedPath(Path.Combine(userProfile, "Downloads"));
            AddProtectedPath(Path.Combine(userProfile, "İndirilenler"));
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                AddProtectedPath(drive.RootDirectory.FullName);
            }
            catch { }
        }
    }

    private static void AddProtectedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var normalized = NormalizePath(path);
            ProtectedDirectories.Add(normalized);
        }
        catch { }
    }

    public static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Bir dizinin silinmesinin güvenli olup olmadığını kontrol eder.
    /// Kök dizinler, sistem dizinleri, kullanıcı profil ve İndirilenler ana klasörü ASLA silinemez.
    /// Silinecek klasörün, temel klasörün (baseDirectory) ALTINDA gerçek bir alt klasör olması zorunludur.
    /// </summary>
    public static bool IsSafeToDeleteDirectory(string? candidateDir, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(candidateDir)) return false;
        if (!Directory.Exists(candidateDir)) return false;

        try
        {
            var normCandidate = NormalizePath(candidateDir);

            // Korunan sistem ve kullanıcı dizinlerinden biri mi?
            if (ProtectedDirectories.Contains(normCandidate))
            {
                return false;
            }

            // Herhangi bir sürücünün kök dizini mi? (C:\, D:\ vb.)
            var root = Path.GetPathRoot(normCandidate);
            if (!string.IsNullOrEmpty(root) && string.Equals(NormalizePath(root), normCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Eğer temel klasör verilmişse, hedef klasör kesinlikle onun bir alt klasörü olmalıdır (kendisi olamaz)
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                var normBase = NormalizePath(baseDirectory);

                // Temel klasör ile hedef aynı ise ASLA silinemez! (Örn: Downloads klasörünün kendisi)
                if (string.Equals(normCandidate, normBase, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var relative = Path.GetRelativePath(normBase, normCandidate);
                if (string.IsNullOrWhiteSpace(relative) ||
                    relative == "." ||
                    relative == ".." ||
                    relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ||
                    Path.IsPathRooted(relative))
                {
                    // Temel klasörün dışına taşıyor veya temel klasörün kendisi
                    return false;
                }
            }
            else
            {
                // Temel klasör yoksa, en az 3 derinlik seviyesinde olmalıdır (Örn: C:\Downloads\MyTorrent)
                var segments = normCandidate.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length <= 2)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bir dosyanın silinmesinin güvenli olup olmadığını kontrol eder.
    /// </summary>
    public static bool IsSafeToDeleteFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        if (Directory.Exists(filePath)) return false; // Dizinler dosya olarak silinemez!
        if (!File.Exists(filePath)) return false;

        try
        {
            var normFile = NormalizePath(filePath);
            var parentDir = Path.GetDirectoryName(normFile);
            if (string.IsNullOrWhiteSpace(parentDir)) return false;

            // Dosya korunan bir sistem kök klasörünün içindeki doğrudan bir sistem dosyası mı?
            var root = Path.GetPathRoot(normFile);
            if (!string.IsNullOrEmpty(root) && string.Equals(NormalizePath(root), NormalizePath(parentDir), StringComparison.OrdinalIgnoreCase))
            {
                // Sürücü kökünde doğrudan yer alan dosyaları silme
                return false;
            }

            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(winDir) && normFile.StartsWith(NormalizePath(winDir), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Dosyayı güvenli bir şekilde siler. Hata durumunda istisnayı yutar.
    /// </summary>
    public static bool SafeDeleteFile(string? filePath)
    {
        if (!IsSafeToDeleteFile(filePath)) return false;

        try
        {
            File.Delete(filePath!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Klasörü sadece güvenliyse ve ana dizin değilse siler.
    /// </summary>
    public static bool SafeDeleteDirectory(string? candidateDir, string? baseDirectory)
    {
        if (!IsSafeToDeleteDirectory(candidateDir, baseDirectory)) return false;

        try
        {
            Directory.Delete(candidateDir!, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
