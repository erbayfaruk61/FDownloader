using System.Net;
using FDownloader.Core.Network;
using FDownloader.Core.Utilities;
using Microsoft.Extensions.Logging;
using MonoTorrent.Client;

namespace FDownloader.Core.Torrent;

/// <summary>
/// BitTorrent güvenliği, path traversal denetimleri ve VPN Kill-Switch kurallarını sağlayan yardımcı servis.
/// </summary>
public static class TorrentSecurityValidator
{
    /// <summary>
    /// Bir torrent içindeki dosya yollarının güvenli olup olmadığını doğrular (Path Traversal ve mutlak yol koruması).
    /// </summary>
    /// <param name="saveDirectory">Hedef indirme kök dizini</param>
    /// <param name="torrentFiles">Torrent dosya yolları</param>
    /// <param name="unsafePath">Hata tespit edilirse güvensiz yol bilgisi</param>
    /// <returns>Tüm yollar güvenli ve hedef kök dizin altındaysa true, aksi halde false.</returns>
    public static bool ValidateTorrentFilePaths(string saveDirectory, IEnumerable<string> torrentFiles, out string? unsafePath)
    {
        unsafePath = null;
        if (string.IsNullOrWhiteSpace(saveDirectory)) return false;

        var fullSaveDir = Path.GetFullPath(saveDirectory);
        if (!fullSaveDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            fullSaveDir += Path.DirectorySeparatorChar;
        }

        foreach (var relativePath in torrentFiles)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) continue;

            // 1. Path Traversal veya Mutlak Yol Tespiti
            if (relativePath.Contains("..", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativePath))
            {
                unsafePath = relativePath;
                return false;
            }

            // 2. Birleştirilmiş tam yolun hedef klasör sınırları içinde olduğunu garanti et
            try
            {
                var combinedPath = Path.GetFullPath(Path.Combine(saveDirectory, relativePath));
                if (!combinedPath.StartsWith(fullSaveDir, StringComparison.OrdinalIgnoreCase))
                {
                    unsafePath = relativePath;
                    return false;
                }
            }
            catch
            {
                unsafePath = relativePath;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// VPN Kill-Switch durumunu değerlendirir ve motorun bağlanması gereken IP adresini döner.
    /// Seçili bir ağ arayüzü varsa ve bu arayüz kapalıysa, gerçek IP sızıntısını önlemek için Loopback (127.0.0.1) döndürür ve kill-switch'i aktif kılar.
    /// </summary>
    public static (IPAddress BindIp, bool IsKillSwitchActive) EvaluateNetworkBinding(string? networkInterfaceId, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(networkInterfaceId))
        {
            return (IPAddress.Any, false);
        }

        var ifaceIp = NetworkInterfaceHelper.GetInterfaceIPv4(networkInterfaceId);
        if (ifaceIp != null)
        {
            logger?.LogInformation("VPN Adaptörü Bağlandı: BitTorrent trafiği {Ip} adresine kilitlendi.", ifaceIp);
            return (ifaceIp, false);
        }

        // Kill-Switch DEVREDE: Seçilen VPN adaptörü kapalı veya yok.
        // Gerçek yerel IP sızıntısını engellemek için trafiği loopback'e kilitliyoruz.
        logger?.LogWarning("VPN Kill-Switch AKTİF: Seçili VPN adaptörü ({Id}) kapalı veya bulunamadı! Gerçek IP sızıntısını önlemek için trafik durduruldu.", networkInterfaceId);
        return (IPAddress.Loopback, true);
    }
}
