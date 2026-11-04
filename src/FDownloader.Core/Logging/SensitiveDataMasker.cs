using System.Text.RegularExpressions;

namespace FDownloader.Core.Logging;

/// <summary>
/// Log mesajlarında yer alan hassas verileri (URL token/query parametreleri, Authorization başlıkları, Cookie bilgileri)
/// maskeleyen yardımcı sınıf (P1-05).
/// </summary>
public static partial class SensitiveDataMasker
{
    private const string Mask = "[REDACTED]";

    // Authorization: Bearer xxxxx veya Basic xxxxx
    private static readonly Regex AuthHeaderRegex = new(
        @"(Authorization\s*[:=]\s*(?:Bearer|Basic)\s+)[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Cookie: name=value
    private static readonly Regex CookieRegex = new(
        @"(Cookie\s*[:=]\s*)[^;\r\n]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // URL token / key / secret / password query params: ?token=xxx, &api_key=xxx, &password=xxx vb.
    private static readonly Regex UrlTokenRegex = new(
        @"([?&](?:token|access_token|api_key|apikey|secret|password|pwd|auth|sig)=)[^&\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Windows kullanıcı klasör yolu: C:\Users\<Username>\ veya /home/<username>/
    private static readonly Regex WindowsUserPathRegex = new(
        @"([A-Za-z]:\\Users\\)[^\\]+(\\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnixUserPathRegex = new(
        @"(/home/)[^/]+(/)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Verilen log veya hata metnini tarayarak hassas kısımları (şifreler, çerezler, token'lar ve kullanıcı profil yolları) maskeler.
    /// </summary>
    public static string MaskMessage(string? message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;

        var result = AuthHeaderRegex.Replace(message, $"${{1}}{Mask}");
        result = CookieRegex.Replace(result, $"${{1}}{Mask}");
        result = UrlTokenRegex.Replace(result, $"${{1}}{Mask}");
        result = WindowsUserPathRegex.Replace(result, $"${{1}}[USER]${{2}}");
        result = UnixUserPathRegex.Replace(result, $"${{1}}[USER]${{2}}");

        return result;
    }
}
