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

    // Dinamik sunucu betiği uzantıları (Kullanıcı bunları dosya olarak değil, dinamik üretilen PDF/ZIP/XLS belgesi olarak indirir)
    private static readonly HashSet<string> DynamicScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aspx", ".asp", ".php", ".php3", ".php4", ".php5", ".phtml",
        ".jsp", ".jspx", ".do", ".action", ".ashx", ".asmx", ".cgi", ".pl"
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

        // Uzantı kontrolü ve dinamik betik uzantılarını gerçek dosya uzantısıyla değiştirme (.aspx -> .pdf gibi)
        string currentExt = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(currentExt) && !string.IsNullOrEmpty(fallbackExtension))
        {
            fileName += fallbackExtension;
        }
        else if (IsDynamicScriptExtension(currentExt) && !string.IsNullOrEmpty(fallbackExtension) && !IsDynamicScriptExtension(fallbackExtension))
        {
            fileName = Path.ChangeExtension(fileName, fallbackExtension);
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

    /// <summary>
    /// Belirtilen uzantının dinamik sunucu betiği (.aspx, .php, .jsp vb.) olup olmadığını doğrular.
    /// </summary>
    public static bool IsDynamicScriptExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var ext = extension.Trim();
        if (!ext.StartsWith('.')) ext = "." + ext;
        return DynamicScriptExtensions.Contains(ext);
    }

    /// <summary>
    /// Dosyanın ilk baytlarından (Magic Bytes) gerçek dosya türü uzantısını tespit eder.
    /// </summary>
    public static string? DetectExtensionFromMagicBytes(ReadOnlySpan<byte> header)
    {
        if (header.IsEmpty || header.Length < 4) return null;

        // PDF: %PDF (0x25, 0x50, 0x44, 0x46)
        if (header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
        {
            return ".pdf";
        }

        // ZIP / DOCX / XLSX / JAR: PK\x03\x04
        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
        {
            return ".zip";
        }

        // RAR: Rar!\x1A\x07 (v4 veya v5)
        if (header.Length >= 6 && header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21 && header[4] == 0x1A && header[5] == 0x07)
        {
            return ".rar";
        }

        // 7-Zip: 7z\xBC\xAF\x27\x1C
        if (header.Length >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
        {
            return ".7z";
        }

        // GZip: \x1F\x8B
        if (header.Length >= 2 && header[0] == 0x1F && header[1] == 0x8B)
        {
            return ".gz";
        }

        // JPEG: \xFF\xD8\xFF
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        // PNG: \x89PNG\r\n\x1a\n
        if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return ".png";
        }

        // GIF: GIF87a veya GIF89a
        if (header.Length >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
            (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61)
        {
            return ".gif";
        }

        return null;
    }

    /// <summary>
    /// İçeriğin HTML / Web giriş sayfası olup olmadığını tespit eder.
    /// </summary>
    public static bool IsHtmlContent(ReadOnlySpan<byte> header)
    {
        if (header.IsEmpty) return false;
        var len = Math.Min(header.Length, 1024);
        var text = Encoding.ASCII.GetString(header[..len]).TrimStart();
        return text.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("<head", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// İçerik verisine (Magic Bytes) veya MIME tipine göre dosya adının uzantısını doğrular ve düzeltir.
    /// Örn: "IstekBelgesi.aspx" ve %PDF -> "IstekBelgesi.pdf"
    /// </summary>
    public static string CorrectExtensionByContent(string fileName, ReadOnlySpan<byte> headerBytes, string? fallbackExt = null)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return fileName;

        var detectedMagicExt = DetectExtensionFromMagicBytes(headerBytes);
        var currentExt = Path.GetExtension(fileName);

        // 1. Magic byte kesin PDF/ZIP/RAR vb. gösteriyorsa ve mevcut uzantı dinamik betik (.aspx vb.) ya da genel/hatalı ise
        if (!string.IsNullOrEmpty(detectedMagicExt))
        {
            if (string.IsNullOrEmpty(currentExt) ||
                IsDynamicScriptExtension(currentExt) ||
                currentExt.Equals(".bin", StringComparison.OrdinalIgnoreCase) ||
                currentExt.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
                (!currentExt.Equals(detectedMagicExt, StringComparison.OrdinalIgnoreCase) && IsDynamicScriptExtension(currentExt)))
            {
                return Path.ChangeExtension(fileName, detectedMagicExt);
            }
        }

        // 2. Magic byte yoksa ama fallback uzantı varsa (.pdf gibi) ve mevcut uzantı dinamik betik ise
        if (!string.IsNullOrEmpty(fallbackExt) && IsDynamicScriptExtension(currentExt))
        {
            return Path.ChangeExtension(fileName, fallbackExt);
        }

        return fileName;
    }
}
