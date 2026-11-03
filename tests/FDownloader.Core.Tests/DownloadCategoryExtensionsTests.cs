using FDownloader.Core.Domain;
using Xunit;

namespace FDownloader.Core.Tests;

public class DownloadCategoryExtensionsTests
{
    [Theory]
    [InlineData("video.mp4", DownloadCategory.Video)]
    [InlineData("film.mkv", DownloadCategory.Video)]
    [InlineData("sarki.mp3", DownloadCategory.Music)]
    [InlineData("album.flac", DownloadCategory.Music)]
    [InlineData("rapor.pdf", DownloadCategory.Document)]
    [InlineData("belge.docx", DownloadCategory.Document)]
    [InlineData("arsiv.zip", DownloadCategory.Archive)]
    [InlineData("dosyalar.7z", DownloadCategory.Archive)]
    [InlineData("kurulum.exe", DownloadCategory.Program)]
    [InlineData("resim.png", DownloadCategory.Image)]
    [InlineData("fotograf.jpg", DownloadCategory.Image)]
    [InlineData("dagitim.torrent", DownloadCategory.Torrent)]
    [InlineData("bilinmeyen.xyz123", DownloadCategory.Other)]
    public void FromFileName_UzantidanKategoriDogruAlgilar(string fileName, DownloadCategory expected)
    {
        var category = DownloadCategoryExtensions.FromFileName(fileName);
        Assert.Equal(expected, category);
    }

    [Theory]
    [InlineData(".mp4", DownloadCategory.Video)]
    [InlineData(".mp3", DownloadCategory.Music)]
    [InlineData(".pdf", DownloadCategory.Document)]
    [InlineData(".zip", DownloadCategory.Archive)]
    [InlineData(".exe", DownloadCategory.Program)]
    [InlineData(".png", DownloadCategory.Image)]
    [InlineData(".torrent", DownloadCategory.Torrent)]
    [InlineData("", DownloadCategory.Other)]
    [InlineData(null, DownloadCategory.Other)]
    public void FromExtension_UzantidanKategoriDogruAlgilar(string? ext, DownloadCategory expected)
    {
        var category = DownloadCategoryExtensions.FromExtension(ext);
        Assert.Equal(expected, category);
    }
}
