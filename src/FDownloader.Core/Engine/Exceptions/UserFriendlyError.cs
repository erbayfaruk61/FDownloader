namespace FDownloader.Core.Engine.Exceptions;

/// <summary>
/// Kullanıcı dostu hata açıklama modeli.
/// "Ne oldu / Neden olmuş olabilir / Ne yapabilirsin" yapısını ve teknik ayrıntıyı taşır.
/// </summary>
public sealed class UserFriendlyError
{
    public string ErrorCode { get; set; } = "ERR_UNKNOWN";
    public string WhatHappened { get; set; } = string.Empty;
    public string PossibleReason { get; set; } = string.Empty;
    public string WhatYouCanDo { get; set; } = string.Empty;
    public string TechnicalDetails { get; set; } = string.Empty;

    public string ToFormattedSummary()
    {
        return $"[{ErrorCode}] {WhatHappened}\n\n• Neden: {PossibleReason}\n• Çözüm: {WhatYouCanDo}";
    }

    public override string ToString() => ToFormattedSummary();
}

