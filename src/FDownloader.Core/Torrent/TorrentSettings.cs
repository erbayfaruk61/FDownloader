namespace FDownloader.Core.Torrent;

/// <summary>
/// BitTorrent motoru, protokol kuralları ve ağ davranışları için konfigürasyon modeli.
/// qBittorrent eşdeğeri ayarları merkezi olarak barındırır.
/// </summary>
public sealed class TorrentSettings
{
    public const int DefaultPort = 55123;
    public const int DefaultMaxConnections = 200;
    public const int DefaultMaxConnectionsPerTorrent = 60;
    public const double DefaultSeedRatio = 1.0;

    /// <summary>Gelen bağlantılar için dinleme portu (varsayılan: 55123).</summary>
    public int ListeningPort { get; set; } = DefaultPort;

    /// <summary>UPnP / NAT-PMP otomatik port yönlendirmeyi etkinleştirir.</summary>
    public bool EnableUpnp { get; set; } = true;

    /// <summary>DHT (Dağıtık Karma Tablosu - Trackersız eş bulma) desteği.</summary>
    public bool EnableDht { get; set; } = true;

    /// <summary>PeX (Eş Değişimi - Peer Exchange) desteği.</summary>
    public bool EnablePeX { get; set; } = true;

    /// <summary>LSD (Yerel Eş Bulma - Local Peer Discovery) desteği.</summary>
    public bool EnableLsd { get; set; } = true;

    /// <summary>
    /// Protokol şifreleme modu:
    /// 0: Tercih Et (RC4 ve PlainText izinli),
    /// 1: Zorunlu Kıl (Sadece şifreli bağlantılara izin ver),
    /// 2: Devre Dışı (Şifreleme kullanma)
    /// </summary>
    public int EncryptionMode { get; set; } = 0;

    /// <summary>Tüm torrentler için maksimum eş bağlantı sayısı.</summary>
    public int MaxConnectionsTotal { get; set; } = DefaultMaxConnections;

    /// <summary>Torrent başına maksimum eş bağlantı sayısı.</summary>
    public int MaxConnectionsPerTorrent { get; set; } = DefaultMaxConnectionsPerTorrent;

    /// <summary>Yeni eklenen video torrentleri için sıralı indirmeyi (önizleme) varsayılan yap.</summary>
    public bool EnableAutoSequentialForVideo { get; set; } = false;

    /// <summary>Hedef paylaşım oranı (Ratio). 0 ise sınır yok.</summary>
    public double SeedRatioLimit { get; set; } = DefaultSeedRatio;

    /// <summary>Hedef paylaşım oranına ulaşıldığında göndermeyi (seed) otomatik durdur.</summary>
    public bool StopSeedingWhenRatioReached { get; set; } = false;

    /// <summary>
    /// BitTorrent trafiğinin kilitleneceği ağ adaptörü Id'si veya adı (VPN Kill-Switch).
    /// Boş veya null ise tüm ağ arayüzleri kullanılır.
    /// </summary>
    public string? NetworkInterfaceId { get; set; }
}
