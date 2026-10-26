namespace FDownloader.Core.Domain;

/// <summary>
/// Dosya türüne göre kategori. Otomatik sınıflandırma ve klasör yönlendirme için kullanılır.
/// </summary>
public enum DownloadCategory
{
    Other = 0,
    Video = 1,
    Music = 2,
    Document = 3,
    Archive = 4,
    Program = 5,
    Image = 6,
    Torrent = 7
}

public static class DownloadCategoryExtensions
{
    private static readonly Dictionary<string, DownloadCategory> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Video
        [".mp4"] = DownloadCategory.Video, [".mkv"] = DownloadCategory.Video,
        [".avi"] = DownloadCategory.Video, [".mov"] = DownloadCategory.Video,
        [".wmv"] = DownloadCategory.Video, [".flv"] = DownloadCategory.Video,
        [".webm"] = DownloadCategory.Video, [".m4v"] = DownloadCategory.Video,
        [".ts"] = DownloadCategory.Video, [".mts"] = DownloadCategory.Video,
        [".m2ts"] = DownloadCategory.Video, [".3gp"] = DownloadCategory.Video,
        [".3g2"] = DownloadCategory.Video, [".vob"] = DownloadCategory.Video,
        [".ogv"] = DownloadCategory.Video, [".divx"] = DownloadCategory.Video,
        [".xvid"] = DownloadCategory.Video, [".asf"] = DownloadCategory.Video,
        [".f4v"] = DownloadCategory.Video, [".rmvb"] = DownloadCategory.Video,
        [".rm"] = DownloadCategory.Video,

        // Müzik
        [".mp3"] = DownloadCategory.Music, [".flac"] = DownloadCategory.Music,
        [".aac"] = DownloadCategory.Music, [".ogg"] = DownloadCategory.Music,
        [".wav"] = DownloadCategory.Music, [".m4a"] = DownloadCategory.Music,
        [".opus"] = DownloadCategory.Music, [".wma"] = DownloadCategory.Music,
        [".alac"] = DownloadCategory.Music, [".aiff"] = DownloadCategory.Music,
        [".ape"] = DownloadCategory.Music, [".mid"] = DownloadCategory.Music,
        [".midi"] = DownloadCategory.Music, [".ac3"] = DownloadCategory.Music,
        [".dts"] = DownloadCategory.Music, [".mka"] = DownloadCategory.Music,

        // Belge
        [".pdf"] = DownloadCategory.Document, [".doc"] = DownloadCategory.Document,
        [".docx"] = DownloadCategory.Document, [".xls"] = DownloadCategory.Document,
        [".xlsx"] = DownloadCategory.Document, [".ppt"] = DownloadCategory.Document,
        [".pptx"] = DownloadCategory.Document, [".txt"] = DownloadCategory.Document,
        [".epub"] = DownloadCategory.Document, [".mobi"] = DownloadCategory.Document,
        [".azw3"] = DownloadCategory.Document, [".djvu"] = DownloadCategory.Document,
        [".cbr"] = DownloadCategory.Document, [".cbz"] = DownloadCategory.Document,
        [".fb2"] = DownloadCategory.Document, [".odt"] = DownloadCategory.Document,
        [".ods"] = DownloadCategory.Document, [".odp"] = DownloadCategory.Document,
        [".rtf"] = DownloadCategory.Document, [".csv"] = DownloadCategory.Document,

        // Arşiv
        [".zip"] = DownloadCategory.Archive, [".rar"] = DownloadCategory.Archive,
        [".7z"] = DownloadCategory.Archive, [".tar"] = DownloadCategory.Archive,
        [".gz"] = DownloadCategory.Archive, [".bz2"] = DownloadCategory.Archive,
        [".xz"] = DownloadCategory.Archive, [".iso"] = DownloadCategory.Archive,
        [".zst"] = DownloadCategory.Archive, [".zstd"] = DownloadCategory.Archive,
        [".tgz"] = DownloadCategory.Archive, [".tbz2"] = DownloadCategory.Archive,
        [".txz"] = DownloadCategory.Archive, [".cab"] = DownloadCategory.Archive,
        [".wim"] = DownloadCategory.Archive, [".swm"] = DownloadCategory.Archive,
        [".esd"] = DownloadCategory.Archive, [".lz"] = DownloadCategory.Archive,
        [".lzma"] = DownloadCategory.Archive, [".lzh"] = DownloadCategory.Archive,
        [".arj"] = DownloadCategory.Archive, [".ace"] = DownloadCategory.Archive,
        [".cpio"] = DownloadCategory.Archive, [".z"] = DownloadCategory.Archive,
        [".001"] = DownloadCategory.Archive, [".002"] = DownloadCategory.Archive,
        [".003"] = DownloadCategory.Archive, [".r00"] = DownloadCategory.Archive,
        [".r01"] = DownloadCategory.Archive,

        // Program & İmaj & Paket
        [".exe"] = DownloadCategory.Program, [".msi"] = DownloadCategory.Program,
        [".msix"] = DownloadCategory.Program, [".appx"] = DownloadCategory.Program,
        [".appxbundle"] = DownloadCategory.Program, [".msixbundle"] = DownloadCategory.Program,
        [".apk"] = DownloadCategory.Program, [".xapk"] = DownloadCategory.Program,
        [".apkm"] = DownloadCategory.Program, [".ipa"] = DownloadCategory.Program,
        [".dmg"] = DownloadCategory.Program, [".pkg"] = DownloadCategory.Program,
        [".deb"] = DownloadCategory.Program, [".rpm"] = DownloadCategory.Program,
        [".appimage"] = DownloadCategory.Program, [".flatpakref"] = DownloadCategory.Program,
        [".snap"] = DownloadCategory.Program, [".jar"] = DownloadCategory.Program,
        [".war"] = DownloadCategory.Program, [".img"] = DownloadCategory.Program,
        [".bin"] = DownloadCategory.Program, [".vhd"] = DownloadCategory.Program,
        [".vhdx"] = DownloadCategory.Program, [".vmdk"] = DownloadCategory.Program,
        [".vdi"] = DownloadCategory.Program, [".qcow2"] = DownloadCategory.Program,
        [".ova"] = DownloadCategory.Program, [".ovf"] = DownloadCategory.Program,

        // Görsel
        [".jpg"] = DownloadCategory.Image, [".jpeg"] = DownloadCategory.Image,
        [".png"] = DownloadCategory.Image, [".gif"] = DownloadCategory.Image,
        [".webp"] = DownloadCategory.Image, [".svg"] = DownloadCategory.Image,
        [".bmp"] = DownloadCategory.Image, [".tiff"] = DownloadCategory.Image,
        [".ico"] = DownloadCategory.Image, [".heic"] = DownloadCategory.Image,
        [".raw"] = DownloadCategory.Image, [".psd"] = DownloadCategory.Image,

        // Torrent & Diğer Veriler
        [".torrent"] = DownloadCategory.Torrent,
        [".sqlite"] = DownloadCategory.Document, [".db"] = DownloadCategory.Document,
        [".sql"] = DownloadCategory.Document, [".bak"] = DownloadCategory.Archive
    };

    public static DownloadCategory FromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return DownloadCategory.Other;
        return ExtensionMap.TryGetValue(extension, out var cat) ? cat : DownloadCategory.Other;
    }

    public static DownloadCategory FromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return DownloadCategory.Other;
        return FromExtension(Path.GetExtension(fileName));
    }
}
