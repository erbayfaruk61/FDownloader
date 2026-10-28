using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FDownloader.Core.Utilities;

public record ArchiveExtractionResult(
    bool Success,
    string? ExtractedDirectory,
    int ExtractedFilesCount,
    string? ErrorMessage,
    bool IsPasswordProtected = false
);

/// <summary>
/// .zip, .rar, .7z, .tar, .gz gibi arşiv dosyalarını güvenli ve kontrollü bir şekilde çıkaran servis.
/// Çok parçalı arşivleri (multi-volume) algılar ve Zip-Slip güvenlik açıklarını engeller.
/// </summary>
public class ArchiveExtractorService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz"
    };

    private static readonly Regex MultiPartRarRegex = new(@"\.part(\d+)\.rar$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MultiPart7zRegex = new(@"\.7z\.(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OldMultiPartRarRegex = new(@"\.r(\d{2})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static ArchiveExtractorService()
    {
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    private readonly ILogger<ArchiveExtractorService>? _logger;

    public ArchiveExtractorService(ILogger<ArchiveExtractorService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Belirtilen dosya yolunun desteklenen bir arşiv formatı olup olmadığını döner.
    /// </summary>
    public static bool IsArchive(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        if (SupportedExtensions.Contains(ext)) return true;

        if (MultiPart7zRegex.IsMatch(filePath)) return true;
        if (OldMultiPartRarRegex.IsMatch(filePath)) return true;

        return false;
    }

    /// <summary>
    /// Çok parçalı arşivlerde sadece ilk cildin (örn: part01.rar veya .7z.001) ya da tekil arşivin açılmasını sağlar.
    /// Parça 2 ve sonrakiler için false döner; böylece her parça indiğinde tekrar açma tetiklenmez.
    /// </summary>
    public static bool IsFirstVolumeOrStandalone(string filePath)
    {
        if (!IsArchive(filePath)) return false;

        var fileName = Path.GetFileName(filePath);

        var rarMatch = MultiPartRarRegex.Match(fileName);
        if (rarMatch.Success)
        {
            var partNum = int.Parse(rarMatch.Groups[1].Value);
            return partNum == 1;
        }

        var sevenZipMatch = MultiPart7zRegex.Match(fileName);
        if (sevenZipMatch.Success)
        {
            var partNum = int.Parse(sevenZipMatch.Groups[1].Value);
            return partNum == 1;
        }

        if (OldMultiPartRarRegex.IsMatch(fileName))
        {
            return false; // Eski RAR'larda ana dosya .rar'dır, .r00, .r01 ardıl parçalardır.
        }

        return true;
    }

    /// <summary>
    /// Arşiv dosyası için temiz bir hedef klasör adı üretir (.part01 eklerini temizler).
    /// </summary>
    public static string GetCleanArchiveFolderName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        if (MultiPartRarRegex.IsMatch(filePath))
        {
            fileName = MultiPartRarRegex.Replace(Path.GetFileName(filePath), "");
        }
        else if (MultiPart7zRegex.IsMatch(filePath))
        {
            fileName = MultiPart7zRegex.Replace(Path.GetFileName(filePath), "");
        }

        return string.IsNullOrWhiteSpace(fileName) ? "Arsiv_Icerik" : fileName;
    }

    /// <summary>
    /// Arşivi asenkron olarak hedef klasöre çıkarır.
    /// Parola korumalı WinZip AES, RAR ve 7z arşivlerinde parola varyasyonlarını ve kod sayfalarını otomatik dener.
    /// </summary>
    public async Task<ArchiveExtractionResult> ExtractAsync(
        string archivePath,
        string? targetDirectory = null,
        bool createSubfolder = true,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
        {
            return new ArchiveExtractionResult(false, null, 0, $"Arşiv dosyası bulunamadı: {archivePath}");
        }

        return await Task.Run(() =>
        {
            var baseDirectory = targetDirectory ?? Path.GetDirectoryName(archivePath) ?? ".";
            var finalDirectory = baseDirectory;

            if (createSubfolder)
            {
                var folderName = GetCleanArchiveFolderName(archivePath);
                finalDirectory = Path.Combine(baseDirectory, folderName);
            }

            Directory.CreateDirectory(finalDirectory);

            var sevenZipExe = Find7zExecutable();

            // 1. Parola verilmişse veya 7-Zip mevcutsa, WinZip AES Data Descriptor / RAR5 / 7z için önce 7-Zip ile dene
            if (sevenZipExe != null)
            {
                var sevenZipRes = ExtractWith7z(sevenZipExe, archivePath, finalDirectory, password, cancellationToken);
                if (sevenZipRes.Success) return sevenZipRes;

                var trimmed = password?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && trimmed != password)
                {
                    var trimRes = ExtractWith7z(sevenZipExe, archivePath, finalDirectory, trimmed, cancellationToken);
                    if (trimRes.Success) return trimRes;
                }

                if (string.IsNullOrEmpty(password) && sevenZipRes.IsPasswordProtected)
                {
                    _logger?.LogInformation("Arşiv parola korumalı algılandı (7-Zip): {ArchivePath}", archivePath);
                    return sevenZipRes;
                }
            }

            if (string.IsNullOrEmpty(password))
            {
                try
                {
                    return ExtractCore(archivePath, finalDirectory, null, null, cancellationToken);
                }
                catch (Exception ex) when (IsPasswordOrCryptoException(ex))
                {
                    _logger?.LogInformation("Arşiv parola korumalı algılandı: {ArchivePath}", archivePath);
                    return new ArchiveExtractionResult(false, null, 0, "Arşiv parola korumalı.", IsPasswordProtected: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Arşiv çıkartılırken hata oluştu: {ArchivePath}", archivePath);
                    return new ArchiveExtractionResult(false, null, 0, FormatTurkishErrorMessage(ex));
                }
            }

            // Parola verilmişse adayları SharpCompress ile sırayla dene (orijinal, trimmed, Latin1, Windows-1254)
            var candidates = new List<(string pwd, System.Text.Encoding? enc)>
            {
                (password, null)
            };

            var trimmedCandidate = password.Trim();
            if (trimmedCandidate != password && !string.IsNullOrEmpty(trimmedCandidate))
            {
                candidates.Add((trimmedCandidate, null));
            }

            // ISO-8859-1 (Latin1) - WinZip AES resmi şartname standardı
            candidates.Add((trimmedCandidate, System.Text.Encoding.Latin1));

            // Windows-1254 (Türkçe Windows ANSI)
            try
            {
                var trEnc = System.Text.Encoding.GetEncoding(1254);
                candidates.Add((trimmedCandidate, trEnc));
            }
            catch { }

            Exception? lastCryptoEx = null;
            foreach (var (candPwd, candEnc) in candidates)
            {
                try
                {
                    var res = ExtractCore(archivePath, finalDirectory, candPwd, candEnc, cancellationToken);
                    if (res.Success) return res;
                    if (res.IsPasswordProtected)
                    {
                        lastCryptoEx = new UnauthorizedAccessException(res.ErrorMessage);
                    }
                }
                catch (Exception ex) when (IsPasswordOrCryptoException(ex))
                {
                    lastCryptoEx = ex;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Arşiv çıkartılırken hata oluştu: {ArchivePath}", archivePath);
                    return new ArchiveExtractionResult(false, null, 0, FormatTurkishErrorMessage(ex));
                }
            }

            _logger?.LogWarning(lastCryptoEx, "Arşiv şifreli veya parola hatalı: {ArchivePath}", archivePath);
            return new ArchiveExtractionResult(false, null, 0, "Girilen arşiv parolası hatalı veya geçersiz. Lütfen parolayı kontrol edip tekrar deneyin.", IsPasswordProtected: true);
        }, cancellationToken);
    }

    public static string? Find7zExecutable()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "7z", "7z.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "7z", "7za.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "tools", "7z", "7z.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "7z", "7z.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", "7z", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
            "7z.exe"
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return Path.GetFullPath(path);
        }

        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir, "7z.exe");
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        catch { }

        return null;
    }

    private ArchiveExtractionResult ExtractWith7z(
        string sevenZipExe,
        string archivePath,
        string finalDirectory,
        string? password,
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = sevenZipExe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("x");
            psi.ArgumentList.Add(archivePath);
            psi.ArgumentList.Add($"-o{finalDirectory}");
            psi.ArgumentList.Add("-y");

            if (!string.IsNullOrEmpty(password))
            {
                psi.ArgumentList.Add($"-p{password}");
            }

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
            {
                return new ArchiveExtractionResult(false, null, 0, "7-Zip başlatılamadı.");
            }

            using var reg = cancellationToken.Register(() =>
            {
                try { proc.Kill(); } catch { }
            });

            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                int count = 0;
                try
                {
                    count = Directory.GetFiles(finalDirectory, "*", SearchOption.AllDirectories).Length;
                }
                catch { }

                _logger?.LogInformation("7-Zip ile arşiv başarıyla çıkarıldı: {Path} -> {Dir} ({Count} dosya)", archivePath, finalDirectory, count);
                return new ArchiveExtractionResult(true, finalDirectory, count, null);
            }

            var combinedOut = (output + "\n" + error);
            if (combinedOut.Contains("Wrong password", StringComparison.OrdinalIgnoreCase) ||
                combinedOut.Contains("Cannot open encrypted", StringComparison.OrdinalIgnoreCase) ||
                combinedOut.Contains("Can not open encrypted", StringComparison.OrdinalIgnoreCase) ||
                combinedOut.Contains("Enter password", StringComparison.OrdinalIgnoreCase))
            {
                return new ArchiveExtractionResult(false, null, 0, "Girilen arşiv parolası hatalı veya geçersiz. Lütfen parolayı kontrol edip tekrar deneyin.", IsPasswordProtected: true);
            }

            return new ArchiveExtractionResult(false, null, 0, "7-Zip çıkarma hatası: " + (string.IsNullOrWhiteSpace(error) ? output : error));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "7-Zip çalıştırma hatası");
            return new ArchiveExtractionResult(false, null, 0, ex.Message);
        }
    }

    private ArchiveExtractionResult ExtractCore(
        string archivePath,
        string finalDirectory,
        string? password,
        System.Text.Encoding? passwordEncoding,
        CancellationToken cancellationToken)
    {
        var readerOptions = new ReaderOptions();
        if (!string.IsNullOrEmpty(password))
        {
            readerOptions.Password = password;
            if (passwordEncoding != null)
            {
                readerOptions.ArchiveEncoding.Password = passwordEncoding;
            }
        }

        using var archive = ArchiveFactory.OpenArchive(archivePath, readerOptions);

        // Şifreli arşiv kontrolü: Parola verilmemişse ve şifreli dosya varsa çıkarmaya kalkışmadan dur
        if (string.IsNullOrEmpty(password))
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.IsEncrypted)
                {
                    _logger?.LogInformation("Arşiv parola korumalı algılandı: {ArchivePath}", archivePath);
                    return new ArchiveExtractionResult(false, null, 0, "Arşiv parola korumalı.", IsPasswordProtected: true);
                }
            }
        }

        int extractedCount = 0;
        var extractionOptions = new ExtractionOptions
        {
            ExtractFullPath = true,
            Overwrite = true
        };

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.IsDirectory) continue;

            // Zip-Slip zafiyet koruması: Çıkartılacak dosyanın hedef klasör dışına taşmasını engelle
            var relativePath = entry.Key?.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar) ?? "";
            var destinationPath = Path.GetFullPath(Path.Combine(finalDirectory, relativePath));

            if (!destinationPath.StartsWith(Path.GetFullPath(finalDirectory), StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning("Zip-Slip koruması: Şüpheli arşiv dosyası engellendi: {Key}", entry.Key);
                continue;
            }

            entry.WriteToDirectory(finalDirectory, extractionOptions);
            extractedCount++;
        }

        _logger?.LogInformation("Arşiv başarıyla çıkarıldı: {ArchivePath} -> {FinalDir} ({Count} dosya)",
            archivePath, finalDirectory, extractedCount);

        return new ArchiveExtractionResult(true, finalDirectory, extractedCount, null);
    }

    private static bool IsPasswordOrCryptoException(Exception ex)
    {
        if (ex is SharpCompress.Common.CryptographicException) return true;
        if (ex is System.Security.Cryptography.CryptographicException) return true;

        var msg = ex.Message ?? "";
        return msg.Contains("password", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("authentication failed", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("winzip aes", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("aes", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("şifre", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("parola", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("bad key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("bad password", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("crc", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("checksum", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatTurkishErrorMessage(Exception ex)
    {
        var msg = ex.Message ?? "";
        if (msg.Contains("not a valid", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("unknown format", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("header", StringComparison.OrdinalIgnoreCase))
        {
            return "Arşiv formatı desteklenmiyor veya dosya başlığı bozuk.";
        }
        if (msg.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("damaged", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("truncated", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("unexpected end", StringComparison.OrdinalIgnoreCase))
        {
            return "Arşiv dosyası bozuk veya eksik indirilmiş.";
        }
        return $"Arşiv açılırken bir hata oluştu ({msg}).";
    }
}
