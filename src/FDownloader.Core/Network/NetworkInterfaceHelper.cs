using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FDownloader.Core.Network;

public record NetworkInterfaceInfo(
    string Id,
    string Name,
    string Description,
    string DisplayName,
    string? IPv4Address,
    bool IsUp,
    bool IsVpnOrVirtual
);

/// <summary>
/// Sistemdeki ağ adaptörlerini akıllı filtreleyerek listeler.
/// Antivirüs (Kaspersky vb.) NDIS filtrelerini, QoS ve WFP sanal ara katmanlarını ayıklayarak
/// sadece gerçek fiziksel kartları (Ethernet, Wi-Fi) ve VPN adaptörlerini (Tailscale, WireGuard vb.) sunar.
/// </summary>
public static class NetworkInterfaceHelper
{
    public static List<NetworkInterfaceInfo> GetAvailableInterfaces()
    {
        var result = new List<NetworkInterfaceInfo>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in interfaces)
            {
                // 1. Loopback ve kullanılmayan tünelleri atla
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                // 2. Antivirüs, QoS, WFP ve sanal NDIS filtre sürücülerini ayıkla
                if (IsVirtualOrFilterDriver(nic)) continue;

                var ipProps = nic.GetIPProperties();
                var rawIpv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address.ToString();

                // 169.254.x.x (APIPA) adresleri bağlantı kopukken Windows'un atadığı geçersiz IP'lerdir
                bool isApipa = rawIpv4 != null && rawIpv4.StartsWith("169.254.");
                string? validIpv4 = isApipa ? null : rawIpv4;

                bool isVpn = IsLikelyVpn(nic.Name, nic.Description);
                bool isConnected = nic.OperationalStatus == OperationalStatus.Up && (!isApipa || isVpn);

                string prefix = isVpn ? "🔒 [VPN] " : "🌐 [Ağ] ";
                string status = isConnected ? "" : " (Bağlantı Yok)";
                string ipDisplay = !string.IsNullOrEmpty(validIpv4) ? $" - {validIpv4}" : "";

                // İsmi ve açıklamayı sadeleştir
                string cleanDesc = nic.Description;
                if (cleanDesc.StartsWith(nic.Name, StringComparison.OrdinalIgnoreCase))
                {
                    cleanDesc = cleanDesc.Substring(nic.Name.Length).Trim('-', ' ');
                }
                string namePart = string.IsNullOrWhiteSpace(cleanDesc)
                    ? nic.Name
                    : $"{nic.Name} ({cleanDesc})";

                string displayName = $"{prefix}{namePart}{ipDisplay}{status}";

                result.Add(new NetworkInterfaceInfo(
                    nic.Id,
                    nic.Name,
                    nic.Description,
                    displayName,
                    validIpv4,
                    isConnected,
                    isVpn
                ));
            }
        }
        catch { }

        // Öncelik Sıralaması:
        // 1. VPN adaptörleri en üstte
        // 2. Aktif (bağlı) Ethernet ve Wi-Fi kartları
        // 3. Bağlı olmayan kartlar en altta
        return result
            .OrderByDescending(x => x.IsVpnOrVirtual && x.IsUp)
            .ThenByDescending(x => x.IsUp)
            .ThenBy(x => x.Name)
            .ToList();
    }

    public static IPAddress? GetInterfaceIPv4(string? interfaceIdOrName)
    {
        if (string.IsNullOrWhiteSpace(interfaceIdOrName)) return null;

        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Id.Equals(interfaceIdOrName, StringComparison.OrdinalIgnoreCase)
                                  || n.Name.Equals(interfaceIdOrName, StringComparison.OrdinalIgnoreCase));

            if (nic != null && nic.OperationalStatus == OperationalStatus.Up)
            {
                var addr = nic.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address;

                if (addr != null && !addr.ToString().StartsWith("169.254."))
                {
                    return addr;
                }
            }
        }
        catch { }

        return null;
    }

    public static bool IsInterfaceActive(string? interfaceIdOrName)
    {
        if (string.IsNullOrWhiteSpace(interfaceIdOrName)) return true;

        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Id.Equals(interfaceIdOrName, StringComparison.OrdinalIgnoreCase)
                                  || n.Name.Equals(interfaceIdOrName, StringComparison.OrdinalIgnoreCase));

            return nic != null && nic.OperationalStatus == OperationalStatus.Up;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Antivirüs NDIS filtreleri, QoS, WFP ve sanal Wi-Fi Direct ara katmanlarını tespit eder.
    /// </summary>
    private static bool IsVirtualOrFilterDriver(NetworkInterface nic)
    {
        var text = (nic.Name + " " + nic.Description).ToLowerInvariant();

        return text.Contains("filter") ||
               text.Contains("ndis") ||
               text.Contains("lightweight") ||
               text.Contains("packet scheduler") ||
               text.Contains("qos") ||
               text.Contains("kernel debug") ||
               text.Contains("hata ayıklayıcı") ||
               text.Contains("wifi direct") ||
               text.Contains("wi-fi direct") ||
               text.Contains("wan miniport") ||
               text.Contains("teredo") ||
               text.Contains("isatap") ||
               text.Contains("6to4") ||
               text.Contains("ip-https") ||
               text.Contains("ras async") ||
               text.Contains("virtual adapter") ||
               text.Contains("kaspersky");
    }

    /// <summary>
    /// VPN ve şifreli tünel adaptörlerini algılar (Tailscale, WireGuard, OpenVPN vb.)
    /// </summary>
    private static bool IsLikelyVpn(string name, string desc)
    {
        var text = (name + " " + desc).ToLowerInvariant();
        return text.Contains("vpn") || text.Contains("wireguard") || text.Contains("tun") ||
               text.Contains("tap") || text.Contains("wintun") || text.Contains("openvpn") ||
               text.Contains("tailscale") || text.Contains("zerotier") || text.Contains("proton") ||
               text.Contains("mullvad") || text.Contains("nord");
    }
}
