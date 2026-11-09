using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FDownloader.App.Services;
using FDownloader.Core.Config;

namespace FDownloader.App.Views;

public partial class ToastNotificationWindow : Window
{
    private static readonly List<ToastNotificationWindow> ActiveToasts = new();
    private static readonly object SyncLock = new();
    private const int MaxVisibleToasts = 5;

    private readonly string? _filePath;
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly CompletionSound _sound = CompletionSound.Notify;

    public ToastNotificationWindow()
    {
        InitializeComponent();
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            Close();
        };

        Closed += (_, _) =>
        {
            _autoCloseTimer.Stop();
            lock (SyncLock)
            {
                ActiveToasts.Remove(this);
            }
            RepositionAll();
        };
    }

    public ToastNotificationWindow(
        string title,
        string message,
        string? filePath,
        string icon = "✓",
        CompletionSound sound = CompletionSound.Notify,
        bool isArchive = false) : this()
    {
        _filePath = filePath;
        _sound = sound;

        TxtTitle.Text = title;
        TxtFileName.Text = message;
        TxtIcon.Text = icon;

        // İkon rengi ve arkaplanı
        if (icon == "💡")
        {
            BorderIconBg.Background = Avalonia.Media.Brush.Parse("#25F59E0B");
            TxtIcon.Foreground = Avalonia.Media.Brush.Parse("#F59E0B");
        }
        else if (icon == "ℹ️" || icon == "⚡")
        {
            BorderIconBg.Background = Avalonia.Media.Brush.Parse("#2538BDF8");
            TxtIcon.Foreground = Avalonia.Media.Brush.Parse("#38BDF8");
        }
        else if (icon == "⚠" || icon == "❌")
        {
            BorderIconBg.Background = Avalonia.Media.Brush.Parse("#25EF4444");
            TxtIcon.Foreground = Avalonia.Media.Brush.Parse("#EF4444");
        }
        else
        {
            BorderIconBg.Background = Avalonia.Media.Brush.Parse("#2510B981");
            TxtIcon.Foreground = Avalonia.Media.Brush.Parse("#10B981");
        }

        // Sadece geçerli bir indirme dosyası yolu varsa butonları göster
        bool hasValidPath = !string.IsNullOrWhiteSpace(filePath);
        PanelActions.IsVisible = hasValidPath;

        if (hasValidPath)
        {
            if (isArchive || Directory.Exists(filePath))
            {
                BtnOpenFile.IsVisible = false;
            }
            else
            {
                BtnOpenFile.IsVisible = true;
            }
        }

        Opened += (_, _) =>
        {
            lock (SyncLock)
            {
                if (!ActiveToasts.Contains(this))
                {
                    ActiveToasts.Add(this);
                }
                while (ActiveToasts.Count > MaxVisibleToasts)
                {
                    var oldest = ActiveToasts[0];
                    ActiveToasts.RemoveAt(0);
                    try { oldest.Close(); } catch { }
                }
            }

            RepositionAll();
            Dispatcher.UIThread.Post(RepositionAll, DispatcherPriority.Loaded);

            if (_sound != CompletionSound.Sessiz)
            {
                PlayCompletionSound();
            }
            _autoCloseTimer.Start();
        };
    }

    public ToastNotificationWindow(string fileName, string? filePath, CompletionSound sound = CompletionSound.Notify)
        : this("İndirme Tamamlandı!", fileName, filePath, "✓", sound, false)
    {
    }

    public static void RepositionAll()
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (SyncLock)
            {
                ActiveToasts.RemoveAll(t => !t.IsVisible);
                if (ActiveToasts.Count == 0) return;

                var firstToast = ActiveToasts.FirstOrDefault();
                var screen = firstToast?.Screens.Primary ?? firstToast?.Screens.All.FirstOrDefault();
                if (screen == null) return;

                var wa = screen.WorkingArea;
                double scale = screen.Scaling;
                int margin = (int)(20 * scale);
                int spacing = (int)(10 * scale);

                int currentBottom = wa.Bottom - margin;

                for (int i = 0; i < ActiveToasts.Count; i++)
                {
                    var toast = ActiveToasts[i];
                    try
                    {
                        toast.UpdateLayout();
                        double actualWidth = toast.Bounds.Width > 0 ? toast.Bounds.Width : toast.Width;
                        double actualHeight = toast.Bounds.Height > 0 ? toast.Bounds.Height : 95;
                        int winWidth = (int)(actualWidth * scale);
                        int winHeight = (int)(actualHeight * scale);

                        int x = wa.Right - winWidth - margin;
                        int y = currentBottom - winHeight;

                        toast.Position = new PixelPoint(x, y);
                        currentBottom = y - spacing;
                    }
                    catch { }
                }
            }
        }, DispatcherPriority.Render);
    }

    private void PlayCompletionSound()
    {
        SoundPlayerService.Play(_sound);
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        if (!string.IsNullOrEmpty(_filePath))
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{_filePath}\"",
                        UseShellExecute = false
                    });
                }
                else
                {
                    var dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{dir}\"",
                            UseShellExecute = false
                        });
                    }
                }
            }
            catch { }
        }
        Close();
    }

    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
            }
            catch { }
        }
        Close();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        Close();
    }
}
