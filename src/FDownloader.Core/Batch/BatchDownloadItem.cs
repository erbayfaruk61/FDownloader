namespace FDownloader.Core.Batch;

/// <summary>
/// Toplu indirme ayrıştırma sonucunda üretilen tekil indirme öğesi.
/// </summary>
public sealed class BatchDownloadItem
{
    public string Url { get; set; } = string.Empty;
    public string? SuggestedFileName { get; set; }
    public string? DestinationPath { get; set; }
    public int Index { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsValid => string.IsNullOrEmpty(ErrorMessage) && !string.IsNullOrWhiteSpace(Url);

    public override string ToString() => IsValid ? $"{Index}: {Url}" : $"{Index}: [Geçersiz] {Url} - {ErrorMessage}";
}

