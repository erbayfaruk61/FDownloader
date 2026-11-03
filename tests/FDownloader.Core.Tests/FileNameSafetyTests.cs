using FDownloader.Core.Utilities;
using Xunit;

namespace FDownloader.Core.Tests;

public sealed class FileNameSafetyTests : IDisposable
{
    private readonly string _tempDir;

    public FileNameSafetyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FDownloader_FileNameSafety_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Theory]
    // 1-5: Path Traversal varyasyonları
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\cmd.exe")]
    [InlineData("....//....//evil.sh")]
    [InlineData("%2e%2e%2fmalware.exe")]
    [InlineData("..%2f..%2fautoexec.bat")]
    [InlineData("/root/.ssh/id_rsa")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    // 8-19: Windows ayrılmış / rezerve cihaz adları
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM5")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT9")]
    [InlineData("con.txt")]
    [InlineData("nul.tar.gz")]
    [InlineData("aux.mp4")]
    [InlineData("prn.bin")]
    // 20-25: Yasak Windows karakterleri (< > : " / \ | ? *)
    [InlineData("file<name>.txt")]
    [InlineData("bad|pipe.doc")]
    [InlineData("col:on.jpg")]
    [InlineData("star*wild.png")]
    [InlineData("quote\"file.mp3")]
    [InlineData("quest?ion.zip")]
    // 26-29: Sondaki geçersiz nokta ve boşluklar
    [InlineData("secret.txt.")]
    [InlineData("image.png ")]
    [InlineData("bad...")]
    [InlineData("folder   .")]
    // 30-33: Kontrol karakterleri ve null byte
    [InlineData("evil\0.exe")]
    [InlineData("bad\nname.txt")]
    [InlineData("tab\tname.pdf")]
    [InlineData("\r\ninjection.sh")]
    public void MaliciousFileNameList_SanitizesSafelyAndStaysInsideBaseDirectory(string maliciousInput)
    {
        string sanitized = FileNameSanitizer.Sanitize(maliciousInput);

        Assert.False(string.IsNullOrWhiteSpace(sanitized), "Temizlenen dosya adı boş olamaz");
        Assert.DoesNotContain("/", sanitized);
        Assert.DoesNotContain("\\", sanitized);
        Assert.DoesNotContain("..", sanitized);

        // Windows yasak karakter kontrolü
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            Assert.DoesNotContain(c, sanitized);
        }

        // Path safety kontrolü: Asla hedef klasör dışına çıkamaz!
        bool isSafe = FileNameSanitizer.IsSafePath(_tempDir, maliciousInput, out string safeFullPath);
        Assert.True(isSafe, $"Hedef klasör dışına çıkış engellenemedi: {maliciousInput} -> {safeFullPath}");
        Assert.StartsWith(_tempDir, safeFullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtremelyLongFileName_IsTruncatedPreservingExtension()
    {
        string veryLongName = new string('a', 300) + ".tar.gz";
        string sanitized = FileNameSanitizer.Sanitize(veryLongName);

        Assert.True(sanitized.Length <= 200);
        Assert.EndsWith(".gz", sanitized);
    }

    [Fact]
    public void ConflictPolicy_RenameAuto_IncrementsCorrectly()
    {
        string baseFile = "dosya.txt";
        string full1 = Path.Combine(_tempDir, baseFile);
        File.WriteAllText(full1, "test 1");

        string resolved1 = FileNameSanitizer.ResolveConflict(_tempDir, baseFile, FileConflictPolicy.RenameAuto);
        Assert.Equal("dosya (1).txt", resolved1);

        string full2 = Path.Combine(_tempDir, resolved1);
        File.WriteAllText(full2, "test 2");

        string resolved2 = FileNameSanitizer.ResolveConflict(_tempDir, baseFile, FileConflictPolicy.RenameAuto);
        Assert.Equal("dosya (2).txt", resolved2);
    }

    [Fact]
    public void ConflictPolicy_OverwriteAndSkip_KeepOriginalName()
    {
        string baseFile = "mevcut.pdf";
        string full = Path.Combine(_tempDir, baseFile);
        File.WriteAllText(full, "pdf content");

        string overwriteResult = FileNameSanitizer.ResolveConflict(_tempDir, baseFile, FileConflictPolicy.Overwrite);
        Assert.Equal(baseFile, overwriteResult);

        string skipResult = FileNameSanitizer.ResolveConflict(_tempDir, baseFile, FileConflictPolicy.Skip);
        Assert.Equal(baseFile, skipResult);
    }
}

