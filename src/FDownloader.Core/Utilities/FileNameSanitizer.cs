using System.Text.RegularExpressions;

namespace FDownloader.Core.Utilities;

/// <summary>
/// Dosya adlarını güvenli hale getirir.
/// Yol geçişi (path traversal), yasak karakterler ve Windows'a özel sorunları temizler.
/// </summary>
public static class FileNameSanitizer
{
    // Windows'ta dosya adında yasak olan karakterler
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    // Windows'ta ayrılmış (reserved) dosya isimleri — uzantıyla birlikte de yasak
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private const int MaxFileNameLength = 200; // Windows 260 karakter sınırının güvenli altı
    private const string FallbackName = "downloaded_file";

    /// <summary>
    /// Verilen dosya adını güvenli hale getirir.
    /// </summary>
    /// <param name="fileName">Ham dosya adı (URL'den veya Content-Disposition'dan).</param>
    /// <param name="fallbackExtension">Uzantı yoksa kullanılacak uzantı (örn. ".bin").</param>
    public static string Sanitize(string? fileName, string fallbackExtension = "")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return FallbackName + fallbackExtension;

        // URL decode (%20 gibi kodları çöz)
        try { fileName = Uri.UnescapeDataString(fileName); }
        catch { /* geçersiz encoding ise orijinali kullan */ }

        // Yol bileşenlerini at — sadece dosya adını al
        fileName = Path.GetFileName(fileName);

        // Yasak karakterleri alt çizgiyle değiştir
        var chars = fileName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidChars, chars[i]) >= 0)
                chars[i] = '_';
        }
        fileName = new string(chars);

        // Baştaki ve sondaki nokta/boşlukları temizle (Windows'ta sorun çıkarır)
        fileName = fileName.Trim('.', ' ');

        // Birden fazla ardışık boşluğu/alt çizgiyi temizle
        fileName = Regex.Replace(fileName, @"[\s_]{2,}", "_");

        // Boşluk kaldıysa fallback
        if (string.IsNullOrWhiteSpace(fileName))
            return FallbackName + fallbackExtension;

        // Windows rezerve isim kontrolü
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (WindowsReservedNames.Contains(nameWithoutExt))
            fileName = "_" + fileName;

        // Uzantı yoksa ekle
        if (string.IsNullOrEmpty(Path.GetExtension(fileName)) && !string.IsNullOrEmpty(fallbackExtension))
            fileName += fallbackExtension;

        // Maksimum uzunluk kontrolü
        if (fileName.Length > MaxFileNameLength)
        {
            string ext = Path.GetExtension(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            fileName = name[..(MaxFileNameLength - ext.Length)] + ext;
        }

        return fileName;
    }

    /// <summary>
    /// Content-Disposition başlığından dosya adını çıkarır.
    /// Örn: "attachment; filename=\"rapor.pdf\"" veya "attachment; filename*=UTF-8''rapor%20son.pdf"
    /// </summary>
    public static string? ExtractFromContentDisposition(string? contentDisposition)
    {
        if (string.IsNullOrWhiteSpace(contentDisposition)) return null;

        // RFC 6266 — filename*=UTF-8'' kodlaması (öncelikli)
        var match = Regex.Match(contentDisposition,
            @"filename\*\s*=\s*(?:UTF-8|utf-8)'[^']*'([^;\s]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            try { return Uri.UnescapeDataString(match.Groups[1].Value); }
            catch { /* devam */ }
        }

        // Klasik filename= (tırnaklı)
        match = Regex.Match(contentDisposition,
            @"filename\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        // Klasik filename= (tırnaksız)
        match = Regex.Match(contentDisposition,
            @"filename\s*=\s*([^;\s]+)", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    /// <summary>
    /// URL'nin son segmentinden dosya adını çıkarır.
    /// Sorgu parametrelerini ve fragment'ı atar.
    /// </summary>
    public static string? ExtractFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var uri = new Uri(url);
            string path = uri.AbsolutePath;
            string name = Path.GetFileName(path);
            return string.IsNullOrWhiteSpace(name) ? null : Uri.UnescapeDataString(name);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Hedef klasörde aynı isimde dosya varsa "(1)", "(2)" şeklinde numaralandırır.
    /// </summary>
    public static string ResolveConflict(string directory, string fileName)
    {
        string fullPath = Path.Combine(directory, fileName);
        if (!File.Exists(fullPath)) return fileName;

        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);

        int counter = 1;
        do
        {
            fileName = $"{nameWithoutExt} ({counter}){ext}";
            fullPath = Path.Combine(directory, fileName);
            counter++;
        }
        while (File.Exists(fullPath));

        return fileName;
    }
}
