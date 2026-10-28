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

        if (isArchive || (filePath != null && Directory.Exists(filePath)))
        {
            BtnOpenFile.IsVisible = false;
        }

        Opened += (_, _) =>
        {
            PositionAtBottomRight();
            if (sound != CompletionSound.Sessiz)
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

    private void PositionAtBottomRight()
    {
        try
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen != null)
            {
                var wa = screen.WorkingArea;
                double scale = screen.Scaling;
                int winWidth = (int)(390 * scale);
                int winHeight = (int)(110 * scale);
                int margin = (int)(24 * scale);

                Position = new PixelPoint(wa.Right - winWidth - margin, wa.Bottom - winHeight - margin);
            }
        }
        catch { }
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
