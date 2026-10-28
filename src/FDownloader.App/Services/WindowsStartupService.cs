using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace FDownloader.App.Services;

public static class WindowsStartupService
{
    private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApplicationRunKeyName = "FDownloader";
    private const string TrayArgument = "--tray";

    public static bool IsStartupEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, false);
            return key?.GetValue(ApplicationRunKeyName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool ConfigureStartup(bool enable, bool startMinimized = false)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, true);
            if (key == null) return false;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    exePath = Environment.GetCommandLineArgs().FirstOrDefault();
                }

                if (string.IsNullOrWhiteSpace(exePath)) return false;

                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var exeCandidate = Path.ChangeExtension(exePath, ".exe");
                    if (File.Exists(exeCandidate))
                    {
                        exePath = exeCandidate;
                    }
                }

                var commandValue = $"\"{exePath}\"";
                if (startMinimized)
                {
                    commandValue += $" {TrayArgument}";
                }

                key.SetValue(ApplicationRunKeyName, commandValue);
                return true;
            }
            else
            {
                key.DeleteValue(ApplicationRunKeyName, false);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}

