using System.IO;
using System.Runtime.InteropServices;
using FDownloader.Core.Config;

namespace FDownloader.App.Services;

/// <summary>
/// İndirme tamamlanma zil seslerini çalan servis.
/// Windows winmm.dll PlaySound API'sini kullanarak harici kütüphane gerektirmeden WAV dosyalarını çalar.
/// </summary>
public static class SoundPlayerService
{
    private const uint SND_FILENAME = 0x00020000;
    private const uint SND_ASYNC = 0x00000001;
    private const uint SND_NODEFAULT = 0x00000002;

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    private static readonly Dictionary<CompletionSound, string> SoundFiles = new()
    {
        { CompletionSound.Notify, @"C:\Windows\Media\notify.wav" },
        { CompletionSound.Chimes, @"C:\Windows\Media\chimes.wav" },
        { CompletionSound.Ding, @"C:\Windows\Media\ding.wav" },
        { CompletionSound.Tada, @"C:\Windows\Media\tada.wav" },
        { CompletionSound.Chord, @"C:\Windows\Media\chord.wav" }
    };

    public static void Play(CompletionSound sound)
    {
        if (sound == CompletionSound.Sessiz) return;

        try
        {
            if (OperatingSystem.IsWindows() && SoundFiles.TryGetValue(sound, out var filePath) && File.Exists(filePath))
            {
                PlaySound(filePath, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
                return;
            }

            // Fallback standart bildirim sesi
            if (OperatingSystem.IsWindows())
            {
                MessageBeep(0x00000040); // MB_ICONASTERISK
            }
        }
        catch
        {
            // Beklenmeyen ses hatalarında sessiz kal
        }
    }

    /// <summary>
    /// Bağlantı koptuğunda veya indirme hızı sıfıra düştüğünde sesli uyarı verir.
    /// </summary>
    public static void PlayWarning()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var warningPath = @"C:\Windows\Media\Windows Background.wav";
                if (File.Exists(warningPath))
                {
                    PlaySound(warningPath, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
                    return;
                }
                MessageBeep(0x00000030); // MB_ICONEXCLAMATION (Sarı ünlem/uyarı sesi)
            }
        }
        catch { }
    }
}
