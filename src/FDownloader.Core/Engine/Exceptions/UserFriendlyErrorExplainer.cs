using System.Net;

namespace FDownloader.Core.Engine.Exceptions;

/// <summary>
/// Sistem ve indirme hatalarını standart "Ne Oldu / Neden Oldu / Ne Yapabilirsin"
/// formatına dönüştüren açıklama motoru.
/// </summary>
public static class UserFriendlyErrorExplainer
{
    public static UserFriendlyError Explain(DownloadException ex)
    {
        var rawMessage = ex.Message;
        var statusCode = ex.StatusCode;

        return ex.Kind switch
        {
            DownloadErrorKind.PermanentNotFound => new UserFriendlyError
            {
                ErrorCode = "ERR_HTTP_404",
                WhatHappened = "İndirilmek istenen dosya sunucuda bulunamadı.",
                PossibleReason = "Dosya sunucudan silinmiş, taşınmış veya verilen indirme adresi hatalı/süresi dolmuş olabilir.",
                WhatYouCanDo = "İndirme bağlantısını tarayıcınızdan tekrar kontrol edin veya taze bir link alıp indirmeyi yenileyin.",
                TechnicalDetails = $"HTTP {statusCode}: {rawMessage}"
            },

            DownloadErrorKind.PermanentAuth => new UserFriendlyError
            {
                ErrorCode = statusCode == 401 ? "ERR_HTTP_401" : "ERR_HTTP_403",
                WhatHappened = "Sunucu indirme isteğini yetkisiz veya yasak olarak reddetti.",
                PossibleReason = "Siteye giriş yapılması (kullanıcı adı/şifre) gerekiyor olabilir, indirme oturum anahtarı/token'ın süresi dolmuş olabilir veya IP adresiniz engellenmiş olabilir.",
                WhatYouCanDo = "Ayarlar > Site Şifreleri menüsünden kullanıcı bilgilerinizi kaydedin veya tarayıcınızdan taze bir indirme linki kopyalayın.",
                TechnicalDetails = $"HTTP {statusCode}: {rawMessage}"
            },

            DownloadErrorKind.TooManyRequests => new UserFriendlyError
            {
                ErrorCode = "ERR_HTTP_429",
                WhatHappened = "Sunucu çok fazla istek yapıldığı gerekçesiyle indirmeyi geçici olarak durdurdu.",
                PossibleReason = "Sunucunun belirlediği istek/indirme sınırı aşıldı veya aynı anda çok fazla parça bağlantısı açıldı.",
                WhatYouCanDo = ex.RetryAfter.HasValue
                    ? $"Lütfen yaklaşık {Math.Ceiling(ex.RetryAfter.Value.TotalSeconds)} saniye bekleyin. Uygulama otomatik yeniden deneyecektir."
                    : "Lütfen birkaç dakika bekleyin veya Ayarlar'dan eşzamanlı parça sayısını düşürün.",
                TechnicalDetails = $"HTTP 429 Too Many Requests. Retry-After: {ex.RetryAfter}"
            },

            DownloadErrorKind.DiskFullOrPermission => rawMessage.Contains("disk", StringComparison.OrdinalIgnoreCase) || rawMessage.Contains("yer", StringComparison.OrdinalIgnoreCase)
                ? new UserFriendlyError
                {
                    ErrorCode = "ERR_DISK_FULL",
                    WhatHappened = "Hedef sürücüde indirmeyi tamamlayacak yeterli boş alan yok.",
                    PossibleReason = "Sabit diskiniz veya USB belleğiniz tamamen doldu.",
                    WhatYouCanDo = "Gereksiz dosyaları silerek hedef sürücüde yer açın veya indirme klasörünü başka bir sürücüye taşıyın.",
                    TechnicalDetails = rawMessage
                }
                : new UserFriendlyError
                {
                    ErrorCode = "ERR_FILE_PERMISSION",
                    WhatHappened = "Dosya yazma veya klasöre erişim izni engellendi.",
                    PossibleReason = "Hedef klasör salt okunur olabilir, harici disk çıkarılmış olabilir veya bir antivirüs programı dosyayı kilitlemiş olabilir.",
                    WhatYouCanDo = "Hedef klasörün yazılabilir olduğunu kontrol edin veya uygulamayı yönetici olarak başlatmayı deneyin.",
                    TechnicalDetails = rawMessage
                },

            DownloadErrorKind.DrmProtected => new UserFriendlyError
            {
                ErrorCode = "ERR_DRM_PROTECTED",
                WhatHappened = "Bu medya içeriği DRM (Dijital Hak Yönetimi) ile şifrelenmiştir.",
                PossibleReason = "İçerik sağlayıcısı Widevine, PlayReady veya FairPlay gibi telif hakkı koruma kilitleri kullanmaktadır.",
                WhatYouCanDo = "DRM korumalı yayınlar yasal ve teknik gerekçelerle indirilemez. İçeriği orijinal platformundan izleyebilirsiniz.",
                TechnicalDetails = rawMessage
            },

            DownloadErrorKind.RangeNotSupported => new UserFriendlyError
            {
                ErrorCode = "ERR_HTTP_416",
                WhatHappened = "Sunucu parçalı (Range) indirme ve duraklatma/devam etme özelliğini desteklemiyor.",
                PossibleReason = "Sunucu eski bir yapılandırmaya sahip olabilir veya geçici tek kullanımlık link sunuyor olabilir.",
                WhatYouCanDo = "İndirmeyi tek parçalı olarak baştan başlatmayı deneyin.",
                TechnicalDetails = rawMessage
            },

            _ => new UserFriendlyError
            {
                ErrorCode = "ERR_NETWORK_TRANSIENT",
                WhatHappened = "Ağ bağlantısında geçici bir kopma veya zaman aşımı yaşandı.",
                PossibleReason = "İnternet bağlantınız dalgalanmış, Wi-Fi kopmuş veya hedef web sunucusu yoğunluktan yanıt verememiş olabilir.",
                WhatYouCanDo = "İnternet bağlantınızı kontrol edin. FDownloader otomatik olarak yeniden bağlanmayı deneyecektir.",
                TechnicalDetails = rawMessage
            }
        };
    }

    public static UserFriendlyError Explain(Exception ex)
    {
        var classified = DownloadErrorClassifier.ClassifyException(ex);
        return Explain(classified);
    }
}

