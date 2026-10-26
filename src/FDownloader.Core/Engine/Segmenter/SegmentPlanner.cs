using FDownloader.Core.Domain;

namespace FDownloader.Core.Engine.Segmenter;

/// <summary>
/// Dosyayı kaç parçaya bölmek gerektiğini ve her parçanın byte aralığını hesaplar.
/// </summary>
public static class SegmentPlanner
{
    /// <summary>
    /// Yeni bir görev için parça planı oluşturur.
    /// </summary>
    /// <param name="taskId">Ait olduğu görev Id'si.</param>
    /// <param name="totalBytes">Dosyanın toplam boyutu.</param>
    /// <param name="supportsRange">Sunucu Range desteği var mı?</param>
    /// <param name="requestedSegments">İstenen parça sayısı.</param>
    /// <param name="segmentThresholdBytes">Bu boyutun altındaki dosyalar tek bağlantıyla indirilir.</param>
    /// <returns>Segment listesi. Tek bağlantı durumunda tek elemanlı liste döner.</returns>
    public static List<Segment> Plan(
        long taskId,
        long totalBytes,
        bool supportsRange,
        int requestedSegments,
        long segmentThresholdBytes = 5 * 1024 * 1024)
    {
        // Tek bağlantı gereken durumlar:
        // 1. Sunucu Range desteklemiyor
        // 2. Boyut bilinmiyor (streaming)
        // 3. Dosya eşik altında
        if (!supportsRange || totalBytes <= 0 || totalBytes < segmentThresholdBytes || requestedSegments <= 1)
        {
            return new List<Segment>
            {
                new Segment
                {
                    TaskId = taskId,
                    Index = 0,
                    StartByte = 0,
                    EndByte = totalBytes > 0 ? totalBytes - 1 : long.MaxValue,
                    Status = SegmentStatus.Pending
                }
            };
        }

        // Parça sayısını sınırla (1-32 arası)
        int segCount = Math.Clamp(requestedSegments, 1, 32);

        // Her parçanın boyutu (tam sayı bölme, son parça kalan byte'ları alır)
        long segmentSize = totalBytes / segCount;
        var segments = new List<Segment>(segCount);

        for (int i = 0; i < segCount; i++)
        {
            long start = i * segmentSize;
            long end = (i == segCount - 1)
                ? totalBytes - 1          // Son parça kalan byte'ları alsın
                : (i + 1) * segmentSize - 1;

            segments.Add(new Segment
            {
                TaskId = taskId,
                Index = i,
                StartByte = start,
                EndByte = end,
                Status = SegmentStatus.Pending
            });
        }

        return segments;
    }

    /// <summary>
    /// Devam etme (resume) için mevcut segmentleri günceller.
    /// Zaten tamamlanan segmentler dokunulmadan bırakılır.
    /// </summary>
    public static IEnumerable<Segment> GetPendingSegments(IEnumerable<Segment> segments) =>
        segments.Where(s => s.Status != SegmentStatus.Completed);

    /// <summary>
    /// Dinamik bölme: En büyük tamamlanmamış segmenti ikiye böler.
    /// Diğer segmentler bittikten sonra tek bir büyük segment kaldıysa çağrılır.
    /// </summary>
    /// <returns>Yeni oluşturulan segment; bölme mümkün değilse null.</returns>
    public static Segment? SplitLargestActiveSegment(
        List<Segment> segments,
        long minSplitSizeBytes = 4 * 1024 * 1024) // 4 MB minimum
    {
        var candidate = segments
            .Where(s => s.Status == SegmentStatus.Downloading && s.RemainingBytes > minSplitSizeBytes * 2)
            .OrderByDescending(s => s.RemainingBytes)
            .FirstOrDefault();

        if (candidate is null) return null;

        long splitPoint = candidate.CurrentOffset + candidate.RemainingBytes / 2;

        // Mevcut segmentin bitiş noktasını güncelle
        long originalEnd = candidate.EndByte;
        candidate.EndByte = splitPoint - 1;

        // Yeni segment oluştur
        var newSegment = new Segment
        {
            TaskId = candidate.TaskId,
            Index = segments.Max(s => s.Index) + 1,
            StartByte = splitPoint,
            EndByte = originalEnd,
            Status = SegmentStatus.Pending
        };

        segments.Add(newSegment);
        return newSegment;
    }
}
