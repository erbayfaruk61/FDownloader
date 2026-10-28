using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Storage;

/// <summary>
/// SQLite veritabanı bağlantısını ve şema migration'ını yönetir.
/// Uygulama genelinde tek bir Database instance kullanılır (singleton).
/// </summary>
public sealed class Database : IDisposable
{
    private readonly string _dbPath;
    private readonly ILogger<Database> _logger;
    private SqliteConnection? _connection;

    // Şema versiyonu — ileride alan eklenince migration için
    private const int CurrentSchemaVersion = 1;

    public Database(string dbPath, ILogger<Database> logger)
    {
        _dbPath = dbPath;
        _logger = logger;
    }

    /// <summary>
    /// Veritabanına bağlanır ve şemayı güncel sürüme getirir.
    /// </summary>
    public void Initialize()
    {
        string? dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={_dbPath};Cache=Shared;");
        _connection.Open();

        // WAL modu: yazma sırasında okuma performansını artırır, çökmeye dayanıklı
        ExecuteNonQuery("PRAGMA journal_mode=WAL;");
        ExecuteNonQuery("PRAGMA synchronous=NORMAL;");
        ExecuteNonQuery("PRAGMA foreign_keys=ON;");

        MigrateSchema();
        _logger.LogInformation("Veritabanı başlatıldı: {Path}", _dbPath);
    }

    /// <summary>Yeni bir komut oluşturur.</summary>
    public SqliteCommand CreateCommand() =>
        _connection?.CreateCommand() ?? throw new InvalidOperationException("Veritabanı başlatılmadı.");

    /// <summary>Transaction başlatır.</summary>
    public SqliteTransaction BeginTransaction() =>
        _connection?.BeginTransaction() ?? throw new InvalidOperationException("Veritabanı başlatılmadı.");

    private void MigrateSchema()
    {
        int version = GetSchemaVersion();
        _logger.LogDebug("Şema versiyonu: {Version}", version);

        if (version < 1) ApplyMigration_V1();

        // Gelecekteki migrationlar buraya eklenir:
        // if (version < 2) ApplyMigration_V2();
    }

    private int GetSchemaVersion()
    {
        // user_version PRAGMA ile şema versiyonu saklanır
        using var cmd = CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void SetSchemaVersion(int version)
    {
        ExecuteNonQuery($"PRAGMA user_version = {version};");
    }

    private void ApplyMigration_V1()
    {
        _logger.LogInformation("Şema V1 oluşturuluyor...");

        // DownloadTasks tablosu
        ExecuteNonQuery(@"
            CREATE TABLE IF NOT EXISTS DownloadTasks (
                Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
                Name                    TEXT NOT NULL,
                OriginalUrl             TEXT NOT NULL,
                FinalUrl                TEXT NOT NULL DEFAULT '',
                FileName                TEXT NOT NULL DEFAULT '',
                SaveDirectory           TEXT NOT NULL DEFAULT '',
                TempFilePath            TEXT NOT NULL DEFAULT '',
                TotalBytes              INTEGER NOT NULL DEFAULT -1,
                DownloadedBytes         INTEGER NOT NULL DEFAULT 0,
                Category                INTEGER NOT NULL DEFAULT 0,
                Status                  INTEGER NOT NULL DEFAULT 0,
                RetryCount              INTEGER NOT NULL DEFAULT 0,
                LastErrorMessage        TEXT,
                SegmentCount            INTEGER NOT NULL DEFAULT 8,
                SupportsRange           INTEGER NOT NULL DEFAULT 0,
                SpeedLimitBytesPerSec   INTEGER NOT NULL DEFAULT 0,
                Priority                INTEGER NOT NULL DEFAULT 100,
                ETag                    TEXT,
                LastModified            TEXT,
                Referer                 TEXT,
                UserAgent               TEXT,
                Cookies                 TEXT,
                CreatedAt               TEXT NOT NULL,
                StartedAt               TEXT,
                CompletedAt             TEXT
            );
        ");

        // Segments tablosu
        ExecuteNonQuery(@"
            CREATE TABLE IF NOT EXISTS Segments (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                TaskId          INTEGER NOT NULL REFERENCES DownloadTasks(Id) ON DELETE CASCADE,
                Idx             INTEGER NOT NULL,
                StartByte       INTEGER NOT NULL,
                EndByte         INTEGER NOT NULL,
                DownloadedBytes INTEGER NOT NULL DEFAULT 0,
                Status          INTEGER NOT NULL DEFAULT 0,
                RetryCount      INTEGER NOT NULL DEFAULT 0
            );
        ");

        // Ayarlar tablosu (key-value)
        ExecuteNonQuery(@"
            CREATE TABLE IF NOT EXISTS Settings (
                Key     TEXT PRIMARY KEY,
                Value   TEXT NOT NULL
            );
        ");

        // İndeksler
        ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_tasks_status ON DownloadTasks(Status);");
        ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_segments_taskid ON Segments(TaskId);");

        SetSchemaVersion(1);
        _logger.LogInformation("Şema V1 oluşturuldu.");
    }

    private void ExecuteNonQuery(string sql)
    {
        using var cmd = CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
