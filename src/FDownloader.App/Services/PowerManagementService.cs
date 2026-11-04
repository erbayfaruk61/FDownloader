using System.Runtime.InteropServices;

namespace FDownloader.App.Services;

/// <summary>
/// İndirme devam ederken Windows'un uyku veya bekleme moduna girmesini engelleyen servis.
/// Windows SetThreadExecutionState Win32 API'sini kullanır.
/// </summary>
public static class PowerManagementService
{
    [Flags]
    public enum ExecutionState : uint
    {
        EsSystemRequired = 0x00000001,
        EsDisplayRequired = 0x00000002,
        EsContinuous = 0x80000000,
        EsAwaymodeRequired = 0x00000040
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    private static bool _isSleepPrevented = false;
    private static readonly object _syncLock = new();

    /// <summary>
    /// Uyku modunun engellenip engellenmediği.
    /// </summary>
    public static bool IsSleepPrevented
    {
        get
        {
            lock (_syncLock)
            {
                return _isSleepPrevented;
            }
        }
    }

    /// <summary>
    /// Sistemin uyku veya askıya alma moduna geçmesini engeller (aktif indirme sırasında çağrılır).
    /// </summary>
    public static void PreventSleep()
    {
        if (!OperatingSystem.IsWindows()) return;

        lock (_syncLock)
        {
            if (!_isSleepPrevented)
            {
                // ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED
                var result = SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired | ExecutionState.EsAwaymodeRequired);
                if (result != 0)
                {
                    _isSleepPrevented = true;
                }
            }
        }
    }

    /// <summary>
    /// Uyku engellemesini kaldırır, sistemin normal güç politikasına dönmesini sağlar.
    /// </summary>
    public static void AllowSleep()
    {
        if (!OperatingSystem.IsWindows()) return;

        lock (_syncLock)
        {
            if (_isSleepPrevented)
            {
                // Yalnızca ES_CONTINUOUS geçilerek kilit serbest bırakılır
                SetThreadExecutionState(ExecutionState.EsContinuous);
                _isSleepPrevented = false;
            }
        }
    }
}

