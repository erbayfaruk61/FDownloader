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
    /// Henüz indirilmemiş (DownloadedBytes == 0 ve Pending/Paused) ardışık komşu segmentleri birleştirir.
    /// Aşırı bölünmüş veya rate limit alan indirmelerin sunucu bağlantı yükünü azaltır.
    /// </summary>
    /// <returns>Birleştirme sonucu segment sayısı azaldıysa true.</returns>
    public static bool ConsolidatePendingSegments(List<Segment> segments, int targetMaxCount = 8)
    {
        lock (segments)
        {
            if (segments.Count <= targetMaxCount) return false;

            bool anyMerged = false;
            // Henüz başlanmamış veya 0 byte indirilmiş parçaları sırala
            var unstarted = segments
                .Where(s => s.Status != SegmentStatus.Completed && s.Status != SegmentStatus.Downloading && s.DownloadedBytes == 0)
                .OrderBy(s => s.StartByte)
                .ToList();

            for (int i = 0; i < unstarted.Count - 1 && segments.Count > targetMaxCount; i++)
            {
                var cur = unstarted[i];
                var nxt = unstarted[i + 1];

                // Ardışık bitişik iki parça mı?
                if (cur.EndByte + 1 == nxt.StartByte)
                {
                    cur.EndByte = nxt.EndByte;
                    segments.Remove(nxt);
                    unstarted.RemoveAt(i + 1);
                    anyMerged = true;
                    i--; // Bir sonrakiyle tekrar birleşebilir mi dene
                }
            }

            // Index numaralarını ardışık olarak yeniden düzenle
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].Index = i;
            }

            return anyMerged;
        }
    }

    /// <summary>
    /// Dinamik bölme: En büyük tamamlanmamış ve aktif indirilen segmenti ikiye böler.
    /// Boşta kalan (idle) worker'ların yardım etmesi (work-stealing) için çağrılır.
    /// </summary>
    /// <returns>Yeni oluşturulan segment; bölme mümkün değilse null.</returns>
    public static Segment? SplitLargestActiveSegment(
        List<Segment> segments,
        long minSplitSizeBytes = 10 * 1024 * 1024, // En az 10 MB
        int maxSegments = 16)
    {
        lock (segments)
        {
            // Segment sayısı sınırını kontrol et (aşırı parça bölünmesini engeller)
            if (segments.Count >= maxSegments) return null;

            var candidate = segments
                .Where(s => s.Status == SegmentStatus.Downloading && s.RemainingBytes >= minSplitSizeBytes * 2)
                .OrderByDescending(s => s.RemainingBytes)
                .FirstOrDefault();

            if (candidate is null) return null;

            long remaining = candidate.RemainingBytes;
            long half = remaining / 2;
            long splitPoint = candidate.CurrentOffset + half;
            long originalEnd = candidate.EndByte;

            if (splitPoint <= candidate.CurrentOffset || splitPoint > originalEnd)
            {
                return null;
            }

            // Mevcut segmentin bitiş noktasını küçült
            candidate.EndByte = splitPoint - 1;

            // Yeni segment oluştur (kalan ikinci yarıyı alır)
            int nextIndex = segments.Count > 0 ? segments.Max(s => s.Index) + 1 : 0;
            var newSegment = new Segment
            {
                TaskId = candidate.TaskId,
                Index = nextIndex,
                StartByte = splitPoint,
                EndByte = originalEnd,
                DownloadedBytes = 0,
                Status = SegmentStatus.Pending
            };

            segments.Add(newSegment);
            return newSegment;
        }
    }
}
