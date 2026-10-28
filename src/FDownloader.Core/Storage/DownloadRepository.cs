using FDownloader.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Storage;

/// <summary>
/// DownloadTask CRUD işlemleri. Segment kayıtları da bu repository üzerinden yönetilir.
/// </summary>
public sealed class DownloadRepository
{
    private readonly Database _db;
    private readonly ILogger<DownloadRepository> _logger;

    public DownloadRepository(Database db, ILogger<DownloadRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── Görev İşlemleri ─────────────────────────────────────────────────

    /// <summary>Yeni görev ekler ve atanan Id'yi döner.</summary>
    public long AddTask(DownloadTask task)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO DownloadTasks
                (Name, OriginalUrl, FinalUrl, FileName, SaveDirectory, TempFilePath,
                 TotalBytes, DownloadedBytes, Category, Status, RetryCount,
                 SegmentCount, SupportsRange, SpeedLimitBytesPerSec, Priority,
                 ETag, LastModified, Referer, UserAgent, Cookies, CreatedAt)
            VALUES
                (@Name, @OriginalUrl, @FinalUrl, @FileName, @SaveDirectory, @TempFilePath,
                 @TotalBytes, @DownloadedBytes, @Category, @Status, @RetryCount,
                 @SegmentCount, @SupportsRange, @SpeedLimit, @Priority,
                 @ETag, @LastModified, @Referer, @UserAgent, @Cookies, @CreatedAt);
            SELECT last_insert_rowid();";

        BindTask(cmd, task);
        task.Id = Convert.ToInt64(cmd.ExecuteScalar());
        return task.Id;
    }

    /// <summary>Görevin durumunu ve ilerleme bilgilerini günceller.</summary>
    public void UpdateTaskStatus(DownloadTask task)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE DownloadTasks SET
                Status = @Status,
                DownloadedBytes = @DownloadedBytes,
                RetryCount = @RetryCount,
                LastErrorMessage = @Error,
                FinalUrl = @FinalUrl,
                FileName = @FileName,
                TempFilePath = @TempFilePath,
                TotalBytes = @TotalBytes,
                SupportsRange = @SupportsRange,
                ETag = @ETag,
                LastModified = @LastModified,
                StartedAt = @StartedAt,
                CompletedAt = @CompletedAt
            WHERE Id = @Id;";

        cmd.Parameters.AddWithValue("@Status", (int)task.Status);
        cmd.Parameters.AddWithValue("@DownloadedBytes", task.DownloadedBytes);
        cmd.Parameters.AddWithValue("@RetryCount", task.RetryCount);
        cmd.Parameters.AddWithValue("@Error", (object?)task.LastErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FinalUrl", task.FinalUrl);
        cmd.Parameters.AddWithValue("@FileName", task.FileName);
        cmd.Parameters.AddWithValue("@TempFilePath", task.TempFilePath);
        cmd.Parameters.AddWithValue("@TotalBytes", task.TotalBytes);
        cmd.Parameters.AddWithValue("@SupportsRange", task.SupportsRange ? 1 : 0);
        cmd.Parameters.AddWithValue("@ETag", (object?)task.ETag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastModified", (object?)task.LastModified ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StartedAt", task.StartedAt.HasValue ? task.StartedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAt", task.CompletedAt.HasValue ? task.CompletedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@Id", task.Id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Görevi siler (segmentler CASCADE ile otomatik silinir).</summary>
    public void DeleteTask(long taskId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM DownloadTasks WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", taskId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Belirli bir görevi Id'ye göre getirir.</summary>
    public DownloadTask? GetTask(long taskId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM DownloadTasks WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", taskId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadTask(reader);
        return null;
    }

    /// <summary>Tüm görevleri yükler (segment listesi dahil).</summary>
    public List<DownloadTask> GetAllTasks()
    {
        var tasks = new Dictionary<long, DownloadTask>();

        // Görevleri yükle
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM DownloadTasks ORDER BY CreatedAt DESC;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tasks[reader.GetInt64(reader.GetOrdinal("Id"))] = ReadTask(reader);

        // Segmentleri yükle
        using var segCmd = _db.CreateCommand();
        segCmd.CommandText = "SELECT * FROM Segments ORDER BY TaskId, Idx;";
        using var segReader = segCmd.ExecuteReader();
        while (segReader.Read())
        {
            var seg = ReadSegment(segReader);
            if (tasks.TryGetValue(seg.TaskId, out var task))
                task.Segments.Add(seg);
        }

        return tasks.Values.ToList();
    }

    // ─── Segment İşlemleri ────────────────────────────────────────────────

    /// <summary>Bir görevin tüm segmentlerini ekler.</summary>
    public void AddSegments(IEnumerable<Domain.Segment> segments)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = _db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            INSERT INTO Segments (TaskId, Idx, StartByte, EndByte, DownloadedBytes, Status, RetryCount)
            VALUES (@TaskId, @Idx, @Start, @End, @Downloaded, @Status, @Retry);
            SELECT last_insert_rowid();";

        foreach (var seg in segments)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@TaskId", seg.TaskId);
            cmd.Parameters.AddWithValue("@Idx", seg.Index);
            cmd.Parameters.AddWithValue("@Start", seg.StartByte);
            cmd.Parameters.AddWithValue("@End", seg.EndByte);
            cmd.Parameters.AddWithValue("@Downloaded", seg.DownloadedBytes);
            cmd.Parameters.AddWithValue("@Status", (int)seg.Status);
            cmd.Parameters.AddWithValue("@Retry", seg.RetryCount);
            seg.Id = Convert.ToInt64(cmd.ExecuteScalar());
        }

        tx.Commit();
    }

    /// <summary>Segment ilerleme checkpoint'i — toplu güncelleme (performans için).</summary>
    public void SaveSegmentProgress(IEnumerable<Domain.Segment> segments)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = _db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            UPDATE Segments
            SET DownloadedBytes = @Downloaded, Status = @Status, RetryCount = @Retry
            WHERE Id = @Id;";

        foreach (var seg in segments)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@Downloaded", seg.DownloadedBytes);
            cmd.Parameters.AddWithValue("@Status", (int)seg.Status);
            cmd.Parameters.AddWithValue("@Retry", seg.RetryCount);
            cmd.Parameters.AddWithValue("@Id", seg.Id);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    // ─── Mapper Yardımcıları ──────────────────────────────────────────────

    private static DownloadTask ReadTask(SqliteDataReader r)
    {
        return new DownloadTask
        {
            Id = r.GetInt64(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            OriginalUrl = r.GetString(r.GetOrdinal("OriginalUrl")),
            FinalUrl = r.GetString(r.GetOrdinal("FinalUrl")),
            FileName = r.GetString(r.GetOrdinal("FileName")),
            SaveDirectory = r.GetString(r.GetOrdinal("SaveDirectory")),
            TempFilePath = r.GetString(r.GetOrdinal("TempFilePath")),
            TotalBytes = r.GetInt64(r.GetOrdinal("TotalBytes")),
            DownloadedBytes = r.GetInt64(r.GetOrdinal("DownloadedBytes")),
            Category = (DownloadCategory)r.GetInt32(r.GetOrdinal("Category")),
            Status = (DownloadStatus)r.GetInt32(r.GetOrdinal("Status")),
            RetryCount = r.GetInt32(r.GetOrdinal("RetryCount")),
            LastErrorMessage = r.IsDBNull(r.GetOrdinal("LastErrorMessage")) ? null : r.GetString(r.GetOrdinal("LastErrorMessage")),
            SegmentCount = r.GetInt32(r.GetOrdinal("SegmentCount")),
            SupportsRange = r.GetInt32(r.GetOrdinal("SupportsRange")) == 1,
            SpeedLimitBytesPerSecond = r.GetInt64(r.GetOrdinal("SpeedLimitBytesPerSec")),
            Priority = r.GetInt32(r.GetOrdinal("Priority")),
            ETag = r.IsDBNull(r.GetOrdinal("ETag")) ? null : r.GetString(r.GetOrdinal("ETag")),
            LastModified = r.IsDBNull(r.GetOrdinal("LastModified")) ? null : r.GetString(r.GetOrdinal("LastModified")),
            Referer = r.IsDBNull(r.GetOrdinal("Referer")) ? null : r.GetString(r.GetOrdinal("Referer")),
            UserAgent = r.IsDBNull(r.GetOrdinal("UserAgent")) ? null : r.GetString(r.GetOrdinal("UserAgent")),
            Cookies = r.IsDBNull(r.GetOrdinal("Cookies")) ? null : r.GetString(r.GetOrdinal("Cookies")),
            CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt")))
        };
    }

    private static Domain.Segment ReadSegment(SqliteDataReader r)
    {
        return new Domain.Segment
        {
            Id = r.GetInt64(r.GetOrdinal("Id")),
            TaskId = r.GetInt64(r.GetOrdinal("TaskId")),
            Index = r.GetInt32(r.GetOrdinal("Idx")),
            StartByte = r.GetInt64(r.GetOrdinal("StartByte")),
            EndByte = r.GetInt64(r.GetOrdinal("EndByte")),
            DownloadedBytes = r.GetInt64(r.GetOrdinal("DownloadedBytes")),
            Status = (SegmentStatus)r.GetInt32(r.GetOrdinal("Status")),
            RetryCount = r.GetInt32(r.GetOrdinal("RetryCount"))
        };
    }

    private static void BindTask(SqliteCommand cmd, DownloadTask task)
    {
        cmd.Parameters.AddWithValue("@Name", task.Name);
        cmd.Parameters.AddWithValue("@OriginalUrl", task.OriginalUrl);
        cmd.Parameters.AddWithValue("@FinalUrl", task.FinalUrl);
        cmd.Parameters.AddWithValue("@FileName", task.FileName);
        cmd.Parameters.AddWithValue("@SaveDirectory", task.SaveDirectory);
        cmd.Parameters.AddWithValue("@TempFilePath", task.TempFilePath);
        cmd.Parameters.AddWithValue("@TotalBytes", task.TotalBytes);
        cmd.Parameters.AddWithValue("@DownloadedBytes", task.DownloadedBytes);
        cmd.Parameters.AddWithValue("@Category", (int)task.Category);
        cmd.Parameters.AddWithValue("@Status", (int)task.Status);
        cmd.Parameters.AddWithValue("@RetryCount", task.RetryCount);
        cmd.Parameters.AddWithValue("@SegmentCount", task.SegmentCount);
        cmd.Parameters.AddWithValue("@SupportsRange", task.SupportsRange ? 1 : 0);
        cmd.Parameters.AddWithValue("@SpeedLimit", task.SpeedLimitBytesPerSecond);
        cmd.Parameters.AddWithValue("@Priority", task.Priority);
        cmd.Parameters.AddWithValue("@ETag", (object?)task.ETag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastModified", (object?)task.LastModified ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Referer", (object?)task.Referer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UserAgent", (object?)task.UserAgent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Cookies", (object?)task.Cookies ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", task.CreatedAt.ToString("O"));
    }

    public string? GetSetting(string key)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @Key;";
        cmd.Parameters.AddWithValue("@Key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }

    public void SetSetting(string key, string value)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO Settings (Key, Value) VALUES (@Key, @Value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
        cmd.Parameters.AddWithValue("@Key", key);
        cmd.Parameters.AddWithValue("@Value", value);
        cmd.ExecuteNonQuery();
    }
}
