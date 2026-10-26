namespace FDownloader.Core.Domain;

/// <summary>
/// Bir indirme görevinin yaşam döngüsü boyunca alabileceği durumlar.
/// </summary>
public enum DownloadStatus
{
    /// <summary>Kuyruğa eklendi, başlamayı bekliyor.</summary>
    Queued = 0,

    /// <summary>Sunucuya bağlanılıyor, probe aşaması.</summary>
    Connecting = 1,

    /// <summary>Aktif olarak indiriliyor.</summary>
    Downloading = 2,

    /// <summary>Kullanıcı tarafından duraklatıldı.</summary>
    Paused = 3,

    /// <summary>Parçalar birleştiriliyor (geçici dosyadan kalıcıya).</summary>
    Merging = 4,

    /// <summary>Hash/boyut doğrulaması yapılıyor.</summary>
    Verifying = 5,

    /// <summary>İndirme başarıyla tamamlandı.</summary>
    Completed = 6,

    /// <summary>Hata oluştu, yeniden deneniyor.</summary>
    Retrying = 7,

    /// <summary>Maksimum deneme sayısı aşıldı, başarısız.</summary>
    Failed = 8,

    /// <summary>Kullanıcı tarafından iptal edildi.</summary>
    Cancelled = 9
}
