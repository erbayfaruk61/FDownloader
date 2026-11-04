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
    private readonly Security.IDataProtectionService _dataProtector;

    public DownloadRepository(Database db, ILogger<DownloadRepository> logger, Security.IDataProtectionService? dataProtector = null)
    {
        _db = db;
        _logger = logger;
        _dataProtector = dataProtector ?? new Security.SecureDataProtector();
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

    /// <summary>Bir görevin indirme adresini (URL) günceller (Link Refresh).</summary>
    public void UpdateTaskUrl(long taskId, string newUrl)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE DownloadTasks SET OriginalUrl = @Url, FinalUrl = @Url WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Url", newUrl);
        cmd.Parameters.AddWithValue("@Id", taskId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Belirli bir görevi Id'ye göre getirir (segmentleriyle birlikte).</summary>
    public DownloadTask? GetTask(long taskId)
    {
        DownloadTask? task = null;
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM DownloadTasks WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", taskId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                task = ReadTask(reader);
        }

        if (task != null)
        {
            using var segCmd = _db.CreateCommand();
            segCmd.CommandText = "SELECT * FROM Segments WHERE TaskId = @TaskId ORDER BY Idx;";
            segCmd.Parameters.AddWithValue("@TaskId", taskId);
            using var segReader = segCmd.ExecuteReader();
            while (segReader.Read())
            {
                task.Segments.Add(ReadSegment(segReader));
            }
        }

        return task;
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

    private DownloadTask ReadTask(SqliteDataReader r)
    {
        var rawCookies = r.IsDBNull(r.GetOrdinal("Cookies")) ? null : r.GetString(r.GetOrdinal("Cookies"));
        var decryptedCookies = !string.IsNullOrEmpty(rawCookies) ? _dataProtector.Unprotect(rawCookies) : null;

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
            Cookies = decryptedCookies,
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

    private void BindTask(SqliteCommand cmd, DownloadTask task)
    {
        string? protectedCookies = !string.IsNullOrEmpty(task.Cookies)
            ? _dataProtector.Protect(task.Cookies)
            : null;

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
        cmd.Parameters.AddWithValue("@Cookies", (object?)protectedCookies ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", task.CreatedAt.ToString("O"));
    }

    public string? GetSetting(string key)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @Key;";
        cmd.Parameters.AddWithValue("@Key", key);
        var result = cmd.ExecuteScalar()?.ToString();
        if (result == null) return null;

        // Eğer hassas bir ayar ise (Proxy şifresi, API anahtarı vb.) çöz
        if (IsSensitiveSettingKey(key))
        {
            return _dataProtector.Unprotect(result);
        }

        return result;
    }

    public void SetSetting(string key, string value)
    {
        string storeValue = value;
        if (IsSensitiveSettingKey(key))
        {
            storeValue = _dataProtector.Protect(value);
        }

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO Settings (Key, Value) VALUES (@Key, @Value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
        cmd.Parameters.AddWithValue("@Key", key);
        cmd.Parameters.AddWithValue("@Value", storeValue);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Kayıtlı tüm ayarları bir sözlük olarak döndürür (hassas veriler çözülmüş olarak).
    /// </summary>
    public Dictionary<string, string> GetAllSettings()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var val = reader.GetString(1);
            if (IsSensitiveSettingKey(key))
            {
                try
                {
                    val = _dataProtector.Unprotect(val);
                }
                catch
                {
                    // Şifre çözülemezse ham bırak
                }
            }
            settings[key] = val;
        }
        return settings;
    }

    /// <summary>
    /// Ayarlar tablosundaki tüm verileri temizler (varsayılana sıfırlama için).
    /// </summary>
    public void ClearAllSettings()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM Settings;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Birden fazla ayarı tek seferde kaydeder veya günceller.
    /// </summary>
    public void ImportSettings(IDictionary<string, string> settings)
    {
        if (settings == null) return;
        foreach (var kvp in settings)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Key))
            {
                SetSetting(kvp.Key, kvp.Value ?? string.Empty);
            }
        }
    }

    private static bool IsSensitiveSettingKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return key.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Credential", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("Proxy.Password", StringComparison.OrdinalIgnoreCase);
    }
}
