using Microsoft.Extensions.Logging;

namespace FDownloader.Core.Engine.Writer;

/// <summary>
/// Gelen segment verilerini diske güvenli şekilde yazar.
/// Önceden yer ayırma (preallocation) + offset tabanlı yazma stratejisi kullanır.
/// </summary>
public sealed class FileWriter : IAsyncDisposable
{
    private readonly string _tempFilePath;
    private readonly ILogger<FileWriter> _logger;
    private FileStream? _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileWriter(string tempFilePath, ILogger<FileWriter> logger)
    {
        _tempFilePath = tempFilePath;
        _logger = logger;
    }

    /// <summary>
    /// Dosyayı oluşturur ve varsa belirtilen boyutta önceden yer ayırır.
    /// </summary>
    /// <param name="totalBytes">Toplam dosya boyutu. -1 ise yer ayırma yapılmaz.</param>
    public async Task InitializeAsync(long totalBytes, CancellationToken ct = default)
    {
        // Klasör yoksa oluştur
        string? dir = Path.GetDirectoryName(_tempFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _stream = new FileStream(
            _tempFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 65536,          // 64 KB buffer
            useAsync: true);

        // Önceden yer ayırma — parçalanmayı (fragmentation) azaltır
        if (totalBytes > 0)
        {
            try
            {
                // Dosyayı tam boyuta genişlet
                _stream.SetLength(totalBytes);
                _logger.LogDebug("Önceden yer ayrıldı: {Bytes} byte, {Path}", totalBytes, _tempFilePath);
            }
            catch (Exception ex)
            {
                // Yer ayırma başarısız olursa devam et (disk dolmuş olabilir)
                _logger.LogWarning(ex, "Önceden yer ayırma başarısız: {Path}", _tempFilePath);
                throw new IOException($"Dosya için yer ayrılamadı. Disk alanı yeterli mi? ({totalBytes:N0} byte gerekli)", ex);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Verilen byte dizisini dosyanın belirtilen offset'ine yazar.
    /// Thread-safe: Birden fazla SegmentWorker aynı anda çağırabilir.
    /// </summary>
    /// <param name="offset">Yazılacak konumun byte offseti.</param>
    /// <param name="data">Yazılacak veri tamponu.</param>
    /// <param name="length">Tamponda geçerli byte sayısı.</param>
    public async Task WriteAtAsync(long offset, byte[] data, int length, CancellationToken ct = default)
    {
        if (_stream is null) throw new InvalidOperationException("FileWriter başlatılmadı. InitializeAsync çağrılmadı.");

        await _writeLock.WaitAsync(ct);
        try
        {
            _stream.Seek(offset, SeekOrigin.Begin);
            await _stream.WriteAsync(data.AsMemory(0, length), ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Tamponu diske zorla flush eder. Checkpoint sırasında çağrılır.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_stream is not null)
            await _stream.FlushAsync(ct);
    }

    /// <summary>
    /// Geçici dosyayı hedef konuma atomik şekilde taşır (yeniden adlandır).
    /// Başarılı indirme sonunda çağrılır.
    /// </summary>
    /// <param name="finalPath">Kalıcı dosya yolu.</param>
    /// <param name="conflictPath">Eğer finalPath zaten varsa bu yola taşınır (null ise üzerine yaz).</param>
    public async Task CommitAsync(string finalPath, CancellationToken ct = default)
    {
        // Stream'i kapat
        if (_stream is not null)
        {
            await _stream.FlushAsync(ct);
            await _stream.DisposeAsync();
            _stream = null;
        }

        // Atomik taşıma (aynı disk üzerindeyse rename, farklı diskse copy+delete)
        try
        {
            File.Move(_tempFilePath, finalPath, overwrite: false);
            _logger.LogInformation("Dosya tamamlandı: {Path}", finalPath);
        }
        catch (IOException) when (File.Exists(finalPath))
        {
            throw new IOException($"Hedef dosya zaten mevcut: {finalPath}");
        }
    }

    /// <summary>
    /// Geçici dosyayı siler (iptal/hata durumunda).
    /// </summary>
    public void DeleteTemp()
    {
        try
        {
            if (File.Exists(_tempFilePath))
                File.Delete(_tempFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geçici dosya silinemedi: {Path}", _tempFilePath);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }
    }
}
