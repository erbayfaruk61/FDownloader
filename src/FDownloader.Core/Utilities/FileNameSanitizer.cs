using System.Text;
using System.Text.RegularExpressions;

namespace FDownloader.Core.Utilities;

/// <summary>
/// Dosya çakışma durumunda uygulanacak politika.
/// </summary>
public enum FileConflictPolicy
{
    RenameAuto,  // (1), (2) şeklinde otomatik numaralandır
    Overwrite,   // Mevcut dosyanın üzerine yaz
    Skip         // Dosyayı atla / indirme yapma
}

/// <summary>
/// Dosya adlarını güvenli hale getirir.
/// Yol geçişi (path traversal), yasak karakterler, Windows rezerve adları ve Unicode normalleştirmesini yönetir.
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

        // URL decode (%20, %2e%2e%2f gibi kodları çöz)
        try { fileName = Uri.UnescapeDataString(fileName); }
        catch { /* geçersiz encoding ise orijinali kullan */ }

        // Unicode normalleştirme (Form C - composite karakterleri birleştir)
        try { fileName = fileName.Normalize(NormalizationForm.FormC); }
        catch { }

        // Null byte ve kontrol karakterlerini kaldır
        fileName = new string(fileName.Where(c => !char.IsControl(c) && c != '\0').ToArray());

        // Ters eğik çizgileri düz çizgiye çevir ve yol bileşenlerini tamamen at
        fileName = fileName.Replace('\\', '/');
        fileName = Path.GetFileName(fileName);

        // Yasak karakterleri alt çizgiyle değiştir
        var chars = fileName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidChars, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
                chars[i] = '_';
        }
        fileName = new string(chars);

        // Baştaki ve sondaki nokta, alt çizgi ve boşlukları temizle (Windows'ta sorun çıkarır)
        fileName = fileName.Trim('.', ' ', '_');

        // Birden fazla ardışık boşluğu/alt çizgiyi temizle
        fileName = Regex.Replace(fileName, @"[\s_]{2,}", "_");

        // Temizleme sonrası boş kaldıysa fallback
        if (string.IsNullOrWhiteSpace(fileName))
            return FallbackName + fallbackExtension;

        // Windows rezerve isim kontrolü (CON, NUL, PRN vb. hem yalın hem de con.txt, con.tar.gz gibi ilk segment bazında)
        string baseName = fileName.Split('.')[0];
        if (WindowsReservedNames.Contains(baseName))
        {
            fileName = "_" + fileName;
        }

        // Uzantı yoksa fallbackExtension ekle
        if (string.IsNullOrEmpty(Path.GetExtension(fileName)) && !string.IsNullOrEmpty(fallbackExtension))
        {
            fileName += fallbackExtension;
        }

        // Maksimum uzunluk kontrolü (uzantıyı koruyarak kırp)
        if (fileName.Length > MaxFileNameLength)
        {
            string ext = Path.GetExtension(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            int takeLength = Math.Max(1, MaxFileNameLength - ext.Length);
            if (takeLength < name.Length)
            {
                name = name[..takeLength];
            }
            fileName = name + ext;
        }

        return fileName;
    }

    /// <summary>
    /// Hedef dosya yolunun hedef dizin sınırları içinde kaldığını doğrular (Path Traversal önleyici).
    /// </summary>
    public static bool IsSafePath(string baseDirectory, string candidateFileName, out string safeFullPath)
    {
        string sanitized = Sanitize(candidateFileName);
        string fullBase = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string combined = Path.GetFullPath(Path.Combine(fullBase, sanitized));

        safeFullPath = combined;

        // Hedef dizin sınırları içinde mi?
        return combined.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
    /// URL'nin son segmentinden veya sorgu parametrelerinden dosya adını çıkarır.
    /// Öncelik: Uzantılı Path Segmenti -> Sorgu Parametreleri (?file=..., ?filename=...) -> Uzantısız Path Segmenti
    /// </summary>
    public static string? ExtractFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var uri = new Uri(url);
            string path = uri.AbsolutePath;
            string name = Path.GetFileName(path);

            // Eğer path'teki dosya adı bir uzantıya sahipse doğrudan kabul et (örn: /files/setup.exe)
            if (!string.IsNullOrWhiteSpace(name) && Path.HasExtension(name))
            {
                return Uri.UnescapeDataString(name);
            }

            // Path uzantısız ise veya boşsa önce sorgu parametrelerini ara (?file=..., ?filename=..., ?name=...)
            if (!string.IsNullOrWhiteSpace(uri.Query))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                foreach (var key in new[] { "filename", "file", "name", "download", "title" })
                {
                    var val = queryParams[key];
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        var candidate = Path.GetFileName(val.Trim());
                        if (!string.IsNullOrWhiteSpace(candidate))
                            return Uri.UnescapeDataString(candidate);
                    }
                }
            }

            // Sorgu parametresinde de yoksa, uzantısız path adını dön
            if (!string.IsNullOrWhiteSpace(name))
            {
                return Uri.UnescapeDataString(name);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Hedef klasörde aynı isimde dosya varsa çakışma politikasına göre çözer.
    /// </summary>
    public static string ResolveConflict(
        string directory,
        string fileName,
        FileConflictPolicy policy = FileConflictPolicy.RenameAuto)
    {
        string fullPath = Path.Combine(directory, fileName);
        if (!File.Exists(fullPath)) return fileName;

        if (policy == FileConflictPolicy.Overwrite || policy == FileConflictPolicy.Skip)
        {
            return fileName;
        }

        // RenameAuto politikası
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
