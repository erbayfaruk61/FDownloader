namespace FDownloader.Core.Domain;

/// <summary>
/// İndirme görevi durum geçişlerini denetleyen ve geçersiz durum geçişlerini engelleyen durum makinesi.
/// </summary>
public static class DownloadStateMachine
{
    private static readonly Dictionary<DownloadStatus, HashSet<DownloadStatus>> AllowedTransitions = new()
    {
        // Kuyrukta bekleyen bir görev: Bağlanmaya (Connecting), doğrudan indirmeye (Downloading), duraklatmaya (Paused) veya iptal edilmeye geçebilir.
        [DownloadStatus.Queued] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Connecting,
            DownloadStatus.Downloading,
            DownloadStatus.Paused,
            DownloadStatus.Cancelled
        },

        // Bağlanıyor (Probe): İndirmeye başlayabilir, doğrudan tamamlanabilir (ör. 0 bayt dosya), duraklatılabilir, başarısız olabilir, yeniden denenebilir veya iptal edilebilir.
        [DownloadStatus.Connecting] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Downloading,
            DownloadStatus.Merging,
            DownloadStatus.Completed,
            DownloadStatus.Paused,
            DownloadStatus.Retrying,
            DownloadStatus.Failed,
            DownloadStatus.Cancelled
        },

        // İndiriliyor: Birleştirmeye (Merging), doğrulamaya (Verifying), tamamlanmaya, duraklatmaya, yeniden denemeye, başarısızlığa veya iptale geçebilir.
        [DownloadStatus.Downloading] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Merging,
            DownloadStatus.Verifying,
            DownloadStatus.Completed,
            DownloadStatus.Paused,
            DownloadStatus.Retrying,
            DownloadStatus.Failed,
            DownloadStatus.Cancelled
        },

        // Birleştiriliyor (Merging): Doğrulamaya (Verifying), tamamlanmaya, başarısızlığa veya iptale geçebilir.
        [DownloadStatus.Merging] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Verifying,
            DownloadStatus.Completed,
            DownloadStatus.Failed,
            DownloadStatus.Cancelled
        },

        // Doğrulanıyor (Verifying): Tamamlanmaya, başarısızlığa veya iptale geçebilir.
        [DownloadStatus.Verifying] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Completed,
            DownloadStatus.Failed,
            DownloadStatus.Cancelled
        },

        // Duraklatıldı (Paused): Kuyruğa dönebilir (Queued), yeniden bağlanabilir (Connecting), indirmeye başlayabilir (Downloading) veya iptal edilebilir.
        [DownloadStatus.Paused] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Queued,
            DownloadStatus.Connecting,
            DownloadStatus.Downloading,
            DownloadStatus.Cancelled
        },

        // Yeniden deneniyor (Retrying): Tekrar bağlanabilir (Connecting), indirmeye devam edebilir, duraklatılabilir, başarısız olabilir veya iptal edilebilir.
        [DownloadStatus.Retrying] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Connecting,
            DownloadStatus.Downloading,
            DownloadStatus.Paused,
            DownloadStatus.Failed,
            DownloadStatus.Cancelled
        },

        // Başarısız oldu (Failed): Kullanıcı yeniden başlattığında veya kuyruğa aldığında Queued, Connecting veya Downloading'e geçebilir; ya da Cancelled olabilir.
        [DownloadStatus.Failed] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Queued,
            DownloadStatus.Connecting,
            DownloadStatus.Downloading,
            DownloadStatus.Cancelled
        },

        // İptal edildi (Cancelled): Yeniden başlatılırsa Queued veya Connecting durumuna dönebilir.
        [DownloadStatus.Cancelled] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Queued,
            DownloadStatus.Connecting
        },

        // Tamamlandı (Completed): Terminal durumdur. Ancak kullanıcı "Yeniden İndir" derse Queued veya Connecting yapılabilir.
        [DownloadStatus.Completed] = new HashSet<DownloadStatus>
        {
            DownloadStatus.Queued,
            DownloadStatus.Connecting
        }
    };

    /// <summary>
    /// Belirtilen mevcut durumdan hedef duruma geçişin geçerli olup olmadığını kontrol eder.
    /// </summary>
    public static bool CanTransition(DownloadStatus current, DownloadStatus target)
    {
        if (current == target) return true; // Aynı duruma geçiş zararsızdır
        return AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(target);
    }

    /// <summary>
    /// Durum geçişini doğrular. Eğer geçiş kural dışı ise <see cref="InvalidOperationException"/> fırlatır.
    /// </summary>
    public static void ValidateTransition(DownloadStatus current, DownloadStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new InvalidOperationException($"Geçersiz durum geçişi: '{current}' durumundan '{target}' durumuna geçilemez.");
        }
    }
}
