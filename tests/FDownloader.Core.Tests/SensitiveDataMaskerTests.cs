using FDownloader.Core.Logging;
using Xunit;

namespace FDownloader.Core.Tests;

public class SensitiveDataMaskerTests
{
    [Fact]
    public void MaskMessage_MasksBearerAndBasicAuth()
    {
        var input = "Request failed with Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 and Authorization: Basic YWRtaW46c2VjcmV0";
        var masked = SensitiveDataMasker.MaskMessage(input);

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", masked);
        Assert.DoesNotContain("YWRtaW46c2VjcmV0", masked);
        Assert.Contains("Authorization: Bearer [REDACTED]", masked);
        Assert.Contains("Authorization: Basic [REDACTED]", masked);
    }

    [Fact]
    public void MaskMessage_MasksCookies()
    {
        var input = "Headers received: Cookie: session_token=super_secret_cookie_value_123; user_id=42";
        var masked = SensitiveDataMasker.MaskMessage(input);

        Assert.DoesNotContain("super_secret_cookie_value_123", masked);
        Assert.Contains("Cookie: [REDACTED]", masked);
    }

    [Fact]
    public void MaskMessage_MasksUrlTokensAndKeys()
    {
        var input = "Navigating to https://example.com/api/v1/download?token=secretToken123&api_key=AIzaSySecretApiKey&file=video.mp4";
        var masked = SensitiveDataMasker.MaskMessage(input);

        Assert.DoesNotContain("secretToken123", masked);
        Assert.DoesNotContain("AIzaSySecretApiKey", masked);
        Assert.Contains("token=[REDACTED]", masked);
        Assert.Contains("api_key=[REDACTED]", masked);
        Assert.Contains("file=video.mp4", masked); // Zararsız parametre korunur
    }
}
