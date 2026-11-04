using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FDownloader.Core.Security;

/// <summary>
/// Çapraz platform hassas veri koruma arayüzü (Windows'ta DPAPI, diğer platformlarda AES-GCM makine anahtarı).
/// </summary>
public interface IDataProtectionService
{
    /// <summary>
    /// Düz metni şifreler ve Base64 formatında döner.
    /// </summary>
    string Protect(string plainText);

    /// <summary>
    /// Şifreli metni (Base64) çözer ve düz metin olarak döner.
    /// </summary>
    string Unprotect(string cipherText);

    /// <summary>
    /// Verilen metnin bu servis tarafından şifrelenmiş bir veri olup olmadığını ön ek ile doğrular.
    /// </summary>
    bool IsProtected(string? text);
}

/// <summary>
/// Windows DPAPI (Data Protection API) ve platform bağımsız AES-GCM yedekleme mekanizmasını kullanan güvenli veri koruyucu.
/// </summary>
public class SecureDataProtector : IDataProtectionService
{
    private const string EncryptedPrefix = "ENC::v1::";

    // Windows harici veya DPAPI başarısız olursa kullanılacak statik makine tuzlaması
    private static readonly byte[] FallbackSalt = Encoding.UTF8.GetBytes("FDownloader::MachineSecureSalt::v1");

    /// <inheritdoc />
    public bool IsProtected(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        // Zaten şifrelenmişse tekrar şifreleme
        if (IsProtected(plainText)) return plainText;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
#pragma warning disable CA1416 // Windows platform doğrulaması
                cipherBytes = ProtectedData.Protect(
                    plainBytes,
                    FallbackSalt,
                    DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
            }
            catch
            {
                cipherBytes = ProtectWithAesGcm(plainBytes);
            }
        }
        else
        {
            cipherBytes = ProtectWithAesGcm(plainBytes);
        }

        return EncryptedPrefix + Convert.ToBase64String(cipherBytes);
    }

    /// <inheritdoc />
    public string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        // Eğer şifrelenmemiş düz metinse (eski veritabanı kayıtları), olduğu gibi döner (Backward Compatibility)
        if (!IsProtected(cipherText))
        {
            return cipherText;
        }

        string rawBase64 = cipherText[EncryptedPrefix.Length..];
        byte[] cipherBytes = Convert.FromBase64String(rawBase64);
        byte[] plainBytes;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
#pragma warning disable CA1416 // Windows platform doğrulaması
                plainBytes = ProtectedData.Unprotect(
                    cipherBytes,
                    FallbackSalt,
                    DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
            }
            catch
            {
                plainBytes = UnprotectWithAesGcm(cipherBytes);
            }
        }
        else
        {
            plainBytes = UnprotectWithAesGcm(cipherBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] ProtectWithAesGcm(byte[] plainBytes)
    {
        byte[] key = DeriveMachineKey();
        byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
        byte[] cipherText = new byte[plainBytes.Length];

        using var aesGcm = new AesGcm(key, tag.Length);
        aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

        // Paketleme: Nonce + Tag + CipherText
        byte[] combined = new byte[nonce.Length + tag.Length + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherText, 0, combined, nonce.Length + tag.Length, cipherText.Length);

        return combined;
    }

    private static byte[] UnprotectWithAesGcm(byte[] combined)
    {
        byte[] key = DeriveMachineKey();
        int nonceSize = AesGcm.NonceByteSizes.MaxSize;
        int tagSize = AesGcm.TagByteSizes.MaxSize;

        if (combined.Length < nonceSize + tagSize)
        {
            throw new CryptographicException("Geçersiz şifreli veri uzunluğu.");
        }

        byte[] nonce = new byte[nonceSize];
        byte[] tag = new byte[tagSize];
        int cipherSize = combined.Length - nonceSize - tagSize;
        byte[] cipherText = new byte[cipherSize];

        Buffer.BlockCopy(combined, 0, nonce, 0, nonceSize);
        Buffer.BlockCopy(combined, nonceSize, tag, 0, tagSize);
        Buffer.BlockCopy(combined, nonceSize + tagSize, cipherText, 0, cipherSize);

        byte[] plainBytes = new byte[cipherSize];
        using var aesGcm = new AesGcm(key, tagSize);
        aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

        return plainBytes;
    }

    private static byte[] DeriveMachineKey()
    {
        // Makine ve kullanıcı adına özgü deterministik PBKDF2 türetimi
        string identity = Environment.MachineName + "::" + Environment.UserName + "::FDownloaderSecureStore";
        byte[] identityBytes = Encoding.UTF8.GetBytes(identity);

        using var kdf = new Rfc2898DeriveBytes(identityBytes, FallbackSalt, 50_000, HashAlgorithmName.SHA256);
        return kdf.GetBytes(32); // 256-bit AES anahtarı
    }
}

