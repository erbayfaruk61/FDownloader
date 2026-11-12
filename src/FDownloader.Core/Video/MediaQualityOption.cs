namespace FDownloader.Core.Video;

/// <summary>
/// Video ve ses indirmeleri için kullanıcı arayüzünde sunulan kalite/çözünürlük seçeneği.
/// </summary>
public sealed class MediaQualityOption
{
    /// <summary>Kullanıcıya gösterilen açıklayıcı metin (Örn: "🎬 Full HD (1080p)", "🎵 MP3 Yüksek Kalite (320 kbps)")</summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>yt-dlp format belirteci (Örn: "bestvideo[height<=1080]+bestaudio/best[height<=1080]/best")</summary>
    public string FormatSpecifier { get; set; } = string.Empty;

    /// <summary>Piksel yüksekliği (Örn: 2160, 1440, 1080, 720, 480, 360). Ses ise null.</summary>
    public int? Height { get; set; }

    /// <summary>Tahmini dosya boyutu (byte). Bilinmiyorsa null.</summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>Yalnızca ses seçeneği mi?</summary>
    public bool IsAudioOnly { get; set; }

    public override string ToString() => DisplayText;
}
