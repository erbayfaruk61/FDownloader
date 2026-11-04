using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Security;

/// <summary>
/// İndirilen dosyaların güvenliğini sağlamak için Windows NTFS Zone.Identifier (Mark-of-the-Web)
/// ve potansiyel olarak tehlikeli çalıştırılabilir dosya uzantılarını yöneten yardımcı servis.
/// </summary>
public static class DownloadFileSecurity
{
    /// <summary>
    /// Doğrudan veya komut istemcisi üzerinden çalıştırıldığında sistemde kod yürütebilecek tehlikeli uzantılar.
    /// </summary>
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".scr", ".pif",
        ".js", ".jse", ".vbs", ".vbe", ".wsf", ".wsh",
        ".ps1", ".ps1xml", ".ps2", ".psc1", ".psc2",
        ".msi", ".msp", ".mst", ".hta", ".cpl", ".jar",
        ".reg", ".inf", ".appx", ".msix"
    };

    /// <summary>
    /// Belirtilen dosya uzantısının veya dosya adının tehlikeli çalıştırılabilir bir dosya olup olmadığını kontrol eder.
    /// </summary>
    public static bool IsDangerousFileType(string? filePathOrName)
    {
        if (string.IsNullOrWhiteSpace(filePathOrName)) return false;

        var ext = Path.GetExtension(filePathOrName);
        return !string.IsNullOrEmpty(ext) && DangerousExtensions.Contains(ext);
    }

    /// <summary>
    /// İndirilen dosyaya Windows Mark-of-the-Web (Zone.Identifier) ADS (Alternate Data Stream) bilgisini yazar.
    /// ZoneId=3 (İnternet Bölgesi). Bu sayede Windows Defender ve SmartScreen dosyayı internetten indirilmiş olarak tanır.
    /// Windows dışı işletim sistemlerinde işlem güvenle atlanır.
    /// </summary>
    /// <param name="filePath">Hedef dosya yolu</param>
    /// <param name="referrerUrl">İsteğe bağlı kaynak referans URL</param>
    /// <param name="hostUrl">İsteğe bağlı indirme URL'si</param>
    /// <param name="logger">Loglama arayüzü</param>
    /// <returns>İşlem başarılıysa veya desteklenmeyen platformda atlandıysa true</returns>
    public static bool ApplyZoneIdentifier(
        string filePath,
        string? referrerUrl = null,
        string? hostUrl = null,
        ILogger? logger = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            // NTFS Alternate Data Stream: filePath:Zone.Identifier
            string zonePath = filePath + ":Zone.Identifier";

            var content = new System.Text.StringBuilder();
            content.AppendLine("[ZoneTransfer]");
            content.AppendLine("ZoneId=3"); // 3 = URLZONE_INTERNET

            if (!string.IsNullOrWhiteSpace(referrerUrl))
            {
                content.AppendLine($"ReferrerUrl={referrerUrl.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(hostUrl))
            {
                content.AppendLine($"HostUrl={hostUrl.Trim()}");
            }

            File.WriteAllText(zonePath, content.ToString(), System.Text.Encoding.ASCII);
            logger?.LogDebug("Zone.Identifier başarıyla uygulandı: {Path}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            // FAT32, exFAT gibi ADS desteklemeyen dosya sistemlerinde hata fırlatabilir, işlemi kırmadan logla
            logger?.LogWarning(ex, "Zone.Identifier yazılamadı (dosya sistemi ADS desteklemiyor olabilir): {Path}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Belirtilen dosyada Zone.Identifier akışının bulunup bulunmadığını kontrol eder.
    /// </summary>
    public static bool HasZoneIdentifier(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            string zonePath = filePath + ":Zone.Identifier";
            if (File.Exists(zonePath))
            {
                var content = File.ReadAllText(zonePath);
                return content.Contains("ZoneId=3", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }

        return false;
    }
}
