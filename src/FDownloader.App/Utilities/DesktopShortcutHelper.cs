using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FDownloader.App.Utilities;

/// <summary>
/// Masaüstü ve Başlat Menüsündeki FDownloader kısayollarını (ve simgelerini)
/// güncel sürümün yürütülebilir dosyası ve ikonu ile eşitleyen yardımcı sınıf.
/// </summary>
public static class DesktopShortcutHelper
{
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;
    private const int SHCNE_UPDATEITEM = 0x00002000;
    private const int SHCNF_PATHW = 0x0005;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>
    /// Masaüstü ve Başlat menüsündeki kısayolları bulur, hedef ve simge yollarını güncel yürütülebilir dosyaya bağlar.
    /// Kısayol yoksa kullanıcı masaüstünde yeni bir kısayol oluşturur ve Windows simge önbelleğini yeniler.
    /// </summary>
    public static void UpdateDesktopShortcuts()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                exePath = Path.Combine(AppContext.BaseDirectory, "FDownloader.App.exe");
            }

            if (!File.Exists(exePath)) return;

            var appDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            var preferredIconPath = Path.Combine(appDir, "Assets", "logo.ico");
            var iconLocation = File.Exists(preferredIconPath) ? preferredIconPath : $"{exePath},0";

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            var userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            var userPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            var commonPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");

            var searchDirs = new[] { userDesktop, commonDesktop, userPrograms, commonPrograms };
            bool anyUpdated = false;

            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

                try
                {
                    var files = Directory.GetFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly);
                    foreach (var lnkFile in files)
                    {
                        var fileName = Path.GetFileName(lnkFile);
                        if (!fileName.Contains("FDownloader", StringComparison.OrdinalIgnoreCase))
                        {
                            // Dosya adı içermiyorsa hedefini kontrol et
                            try
                            {
                                dynamic scTest = shell.CreateShortcut(lnkFile);
                                string target = scTest.TargetPath ?? "";
                                if (!target.Contains("FDownloader", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        // Kısayolu güncelle
                        UpdateSingleShortcut(shell, lnkFile, exePath, appDir, iconLocation);
                        anyUpdated = true;
                    }
                }
                catch
                {
                    // İlgili dizin okunamadıysa devam et
                }
            }

            // Masaüstünde hiç kısayol bulunamadıysa yeni oluştur
            var defaultDesktopShortcut = Path.Combine(userDesktop, "FDownloader.lnk");
            if (!anyUpdated && !File.Exists(defaultDesktopShortcut))
            {
                try
                {
                    UpdateSingleShortcut(shell, defaultDesktopShortcut, exePath, appDir, iconLocation);
                    anyUpdated = true;
                }
                catch { }
            }

            // Windows Simge Önbelleğini (Icon Cache) ve Kabuğunu Yenile
            RefreshWindowsIconCache();
        }
        catch
        {
            // Kısayol güncelleme işlemi ana uygulama akışını asla engellememelidir
        }
    }

    private static void UpdateSingleShortcut(dynamic shell, string shortcutPath, string exePath, string workingDir, string iconLocation)
    {
        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.IconLocation = iconLocation;
            shortcut.Description = "FDownloader - Hızlı ve Akıllı İndirme Yöneticisi";
            shortcut.Save();

            // Dosya zaman damgasını güncelleyerek Explorer'ın simge önbelleğini tetikle
            File.SetLastWriteTimeUtc(shortcutPath, DateTime.UtcNow);

            // Tekil dosya için Explorer'a bildirim gönder
            IntPtr pPath = Marshal.StringToHGlobalUni(shortcutPath);
            try
            {
                SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pPath, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(pPath);
            }
        }
        catch { }
    }

    /// <summary>
    /// Windows Explorer simge önbelleğini ve dosya ilişkilendirmelerini anında yeniler.
    /// </summary>
    public static void RefreshWindowsIconCache()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }
}

