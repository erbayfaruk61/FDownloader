using FDownloader.Core.Engine.Exceptions;
using System.Net;
using Xunit;

namespace FDownloader.Core.Tests;

public class UserFriendlyErrorTests
{
    [Fact]
    public void Explain_NotFound_Returns_404Details()
    {
        var ex = new DownloadException(DownloadErrorKind.PermanentNotFound, "Not Found", 404);
        var explanation = UserFriendlyErrorExplainer.Explain(ex);

        Assert.Equal("ERR_HTTP_404", explanation.ErrorCode);
        Assert.Contains("bulunamadı", explanation.WhatHappened);
        Assert.Contains("silinmiş", explanation.PossibleReason);
        Assert.Contains("taze bir link", explanation.WhatYouCanDo);
    }

    [Fact]
    public void Explain_AuthError_Returns_401Or403Details()
    {
        var ex401 = new DownloadException(DownloadErrorKind.PermanentAuth, "Unauthorized", 401);
        var expl401 = UserFriendlyErrorExplainer.Explain(ex401);
        Assert.Equal("ERR_HTTP_401", expl401.ErrorCode);
        Assert.Contains("yetkisiz", expl401.WhatHappened);

        var ex403 = new DownloadException(DownloadErrorKind.PermanentAuth, "Forbidden", 403);
        var expl403 = UserFriendlyErrorExplainer.Explain(ex403);
        Assert.Equal("ERR_HTTP_403", expl403.ErrorCode);
        Assert.Contains("yetkisiz veya yasak", expl403.WhatHappened);
    }

    [Fact]
    public void Explain_RateLimit_Returns_429WithRetrySeconds()
    {
        var ex = new DownloadException(
            DownloadErrorKind.TooManyRequests,
            "Too Many Requests",
            429,
            retryAfter: TimeSpan.FromSeconds(45));

        var explanation = UserFriendlyErrorExplainer.Explain(ex);

        Assert.Equal("ERR_HTTP_429", explanation.ErrorCode);
        Assert.Contains("çok fazla istek", explanation.WhatHappened);
        Assert.Contains("45 saniye", explanation.WhatYouCanDo);
    }

    [Fact]
    public void Explain_DiskFull_Returns_DiskFullResolution()
    {
        var ioEx = new IOException("There is not enough space on the disk.");
        var explanation = UserFriendlyErrorExplainer.Explain(ioEx);

        Assert.Equal("ERR_DISK_FULL", explanation.ErrorCode);
        Assert.Contains("yeterli boş alan yok", explanation.WhatHappened);
        Assert.Contains("yer açın", explanation.WhatYouCanDo);
    }

    [Fact]
    public void Explain_DrmProtected_Returns_DrmDetails()
    {
        var drmEx = new InvalidOperationException("Encrypted DRM Widevine video stream detected.");
        var explanation = UserFriendlyErrorExplainer.Explain(drmEx);

        Assert.Equal("ERR_DRM_PROTECTED", explanation.ErrorCode);
        Assert.Contains("DRM", explanation.WhatHappened);
        Assert.Contains("yasal ve teknik gerekçelerle indirilemez", explanation.WhatYouCanDo);
    }

    [Fact]
    public void Explain_TransientNetwork_Returns_RetryDetails()
    {
        var netEx = new TimeoutException("The connection timed out.");
        var explanation = UserFriendlyErrorExplainer.Explain(netEx);

        Assert.Equal("ERR_NETWORK_TRANSIENT", explanation.ErrorCode);
        Assert.Contains("zaman aşımı", explanation.WhatHappened);
        Assert.Contains("otomatik olarak yeniden bağlanmayı deneyecektir", explanation.WhatYouCanDo);
    }
}

