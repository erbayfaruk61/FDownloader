using FDownloader.Core.Utilities;
using Xunit;

namespace FDownloader.Core.Tests;

public class SpeedCalculatorTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1,0 KB")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(1048576, "1,0 MB")]
    [InlineData(1073741824, "1,00 GB")]
    public void FormatBytes_DogruBirimVeDegerUretir(long bytes, string expectedPrefix)
    {
        var result = SpeedCalculator.FormatBytes(bytes);
        // Kültüre göre ondalık ayıracı virgül veya nokta olabilir
        var normalized = result.Replace('.', ',');
        Assert.Equal(expectedPrefix, normalized);
    }

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(512, "512 B/s")]
    [InlineData(1024, "1,0 KB/s")]
    [InlineData(5242880, "5,0 MB/s")]
    [InlineData(2147483648, "2,00 GB/s")]
    public void FormatSpeed_DogruBirimVeHizUretir(double bps, string expectedPrefix)
    {
        var result = SpeedCalculator.FormatSpeed(bps);
        var normalized = result.Replace('.', ',');
        Assert.Equal(expectedPrefix, normalized);
    }

    [Fact]
    public void FormatEta_NullIseHesaplaniyorDoner()
    {
        var result = SpeedCalculator.FormatEta(null);
        Assert.Equal("Hesaplanıyor...", result);
    }

    [Fact]
    public void FormatEta_NegatifIseTamamlaniyorDoner()
    {
        var result = SpeedCalculator.FormatEta(TimeSpan.FromSeconds(-5));
        Assert.Equal("Tamamlanıyor...", result);
    }

    [Fact]
    public void FormatEta_SaniyeDakikaSaatBicimlendirir()
    {
        Assert.Equal("45sn", SpeedCalculator.FormatEta(TimeSpan.FromSeconds(45)));
        Assert.Equal("3dk 15sn", SpeedCalculator.FormatEta(TimeSpan.FromSeconds(195)));
        Assert.Equal("2s 10dk", SpeedCalculator.FormatEta(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(10))));
    }

    [Fact]
    public void SpeedCalculator_OrnekEklendigindeVeSifirlandigindaDogruCalisir()
    {
        var calc = new SpeedCalculator(windowSeconds: 3);
        Assert.Equal(0, calc.BytesPerSecond);
        Assert.Null(calc.GetEta(1000));

        calc.AddSample(1024);
        calc.AddSample(2048);

        // Hız 0 veya daha büyük olmalı
        Assert.True(calc.BytesPerSecond >= 0);

        calc.Reset();
        Assert.Equal(0, calc.BytesPerSecond);
        Assert.Null(calc.GetEta(5000));
    }
}
