using System.Net;
using System.Net.Sockets;

namespace FDownloader.Core.Engine.Exceptions;

public enum DownloadErrorKind
{
    Transient,            // Ağ kopması, timeout, 500, 502, 503, 504 -> Üstel backoff ile dene
    TooManyRequests,      // 429 -> Retry-After süresine uy
    PermanentNotFound,    // 404, 410 -> Yeniden deneme, direkt başarısız
    PermanentAuth,        // 401, 403 -> Kimlik hatası, süresi dolmuş URL
    DiskFullOrPermission, // Disk dolu veya erişim engeli -> Duraklat
    RangeNotSupported,    // 416 veya beklenmeyen 200 -> Range desteklenmiyor
    DrmProtected          // DRM / Dijital Hak Yönetimi ile şifrelenmiş içerik -> İndirme denenmez
}

public sealed class DownloadException : Exception
{
    public DownloadErrorKind Kind { get; }
    public TimeSpan? RetryAfter { get; }
    public int? StatusCode { get; }

    public DownloadException(
        DownloadErrorKind kind,
        string message,
        int? statusCode = null,
        TimeSpan? retryAfter = null,
        Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }
}

public static class DownloadErrorClassifier
{
    public static DownloadException ClassifyResponse(HttpResponseMessage response)
    {
        int statusCode = (int)response.StatusCode;

        switch (response.StatusCode)
        {
            case (HttpStatusCode)429:
            {
                TimeSpan? retryAfter = null;
                if (response.Headers.RetryAfter != null)
                {
                    if (response.Headers.RetryAfter.Delta.HasValue)
                    {
                        retryAfter = response.Headers.RetryAfter.Delta.Value;
                    }
                    else if (response.Headers.RetryAfter.Date.HasValue)
                    {
                        var diff = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                        if (diff > TimeSpan.Zero) retryAfter = diff;
                    }
                }

                return new DownloadException(
                    DownloadErrorKind.TooManyRequests,
                    "Çok fazla istek yapıldı (429). Sunucu istekleri sınırlandırdı.",
                    statusCode,
                    retryAfter);
            }

            case HttpStatusCode.NotFound:
            case HttpStatusCode.Gone:
                return new DownloadException(
                    DownloadErrorKind.PermanentNotFound,
                    $"Dosya sunucuda bulunamadı veya silinmiş ({statusCode}).",
                    statusCode);

            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                return new DownloadException(
                    DownloadErrorKind.PermanentAuth,
                    $"Erişim engellendi veya indirme bağlantısının süresi doldu ({statusCode}).",
                    statusCode);

            case HttpStatusCode.RequestedRangeNotSatisfiable:
                return new DownloadException(
                    DownloadErrorKind.RangeNotSupported,
                    "Sunucu istenen parça aralığını desteklemiyor (416).",
                    statusCode);

            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                return new DownloadException(
                    DownloadErrorKind.Transient,
                    $"Geçici sunucu hatası ({statusCode}).",
                    statusCode);

            default:
                if (!response.IsSuccessStatusCode)
                {
                    return new DownloadException(
                        DownloadErrorKind.Transient,
                        $"Sunucu beklenmeyen bir yanıt döndü ({statusCode}).",
                        statusCode);
                }

                return new DownloadException(DownloadErrorKind.Transient, "Bilinmeyen durum", statusCode);
        }
    }

    public static bool IsDrmProtectedMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return message.Contains("DRM", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Widevine", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("PlayReady", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("FairPlay", StringComparison.OrdinalIgnoreCase) ||
               (message.Contains("protected", StringComparison.OrdinalIgnoreCase) && message.Contains("stream", StringComparison.OrdinalIgnoreCase));
    }

    public static DownloadException ClassifyException(Exception ex)
    {
        if (ex is DownloadException de) return de;

        if (IsDrmProtectedMessage(ex.Message))
        {
            return new DownloadException(
                DownloadErrorKind.DrmProtected,
                "Bu içerik DRM (Dijital Hak Yönetimi) ile korunmaktadır ve indirilemez.",
                inner: ex);
        }

        if (ex is UnauthorizedAccessException)
        {
            return new DownloadException(
                DownloadErrorKind.DiskFullOrPermission,
                "Yazma izni reddedildi. Dosyaya veya klasöre erişim engellendi.",
                inner: ex);
        }

        if (ex is DirectoryNotFoundException or DriveNotFoundException)
        {
            return new DownloadException(
                DownloadErrorKind.DiskFullOrPermission,
                $"Hedef klasör veya sürücü bulunamadı ya da çıkarıldı: {ex.Message}",
                inner: ex);
        }

        if (ex is IOException ioEx)
        {
            // Ağ soketinin kapatılması veya HTTP aktarım akışının kesilmesi bir ağ hatasıdır (Transient)
            if (ioEx.InnerException is SocketException ||
                ioEx.Message.Contains("transport connection", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("unexpected EOF", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("connection was closed", StringComparison.OrdinalIgnoreCase))
            {
                return new DownloadException(
                    DownloadErrorKind.Transient,
                    "Ağ bağlantısı veri aktarımı sırasında kesildi.",
                    inner: ex);
            }

            // Disk dolu kontrolü (HRESULT 0x80070070 = ERROR_DISK_FULL)
            const int HrDiskFull = unchecked((int)0x80070070);
            if (ioEx.HResult == HrDiskFull || ioEx.Message.Contains("disk", StringComparison.OrdinalIgnoreCase))
            {
                return new DownloadException(
                    DownloadErrorKind.DiskFullOrPermission,
                    "Yetersiz disk alanı. Lütfen diskte yer açın.",
                    inner: ex);
            }

            return new DownloadException(
                DownloadErrorKind.DiskFullOrPermission,
                $"Dosya yazma hatası: {ioEx.Message}",
                inner: ex);
        }

        if (ex is TimeoutException or SocketException)
        {
            return new DownloadException(
                DownloadErrorKind.Transient,
                "Bağlantı zaman aşımına uğradı veya ağ bağlantısı koptu.",
                inner: ex);
        }

        if (ex is HttpRequestException)
        {
            return new DownloadException(
                DownloadErrorKind.Transient,
                $"Ağ isteği hatası: {ex.Message}",
                inner: ex);
        }

        return new DownloadException(
            DownloadErrorKind.Transient,
            $"Beklenmeyen indirme hatası: {ex.Message}",
            inner: ex);
    }
}
