using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FDownloader.App.ViewModels;

namespace FDownloader.App.Views;

public partial class UpdateToastNotificationWindow : Window
{
    private static UpdateToastNotificationWindow? _activeToast;
    private static readonly object SyncLock = new();

    public UpdateToastNotificationWindow()
    {
        InitializeComponent();

        Closed += (_, _) =>
        {
            lock (SyncLock)
            {
                if (_activeToast == this)
                {
                    _activeToast = null;
                }
            }
        };

        Opened += (_, _) =>
        {
            Reposition();
            Dispatcher.UIThread.Post(Reposition, DispatcherPriority.Loaded);
        };
    }

    public static void ShowToast(MainViewModel viewModel)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (SyncLock)
            {
                if (_activeToast != null && _activeToast.IsVisible)
                {
                    _activeToast.DataContext = viewModel;
                    _activeToast.Activate();
                    _activeToast.Reposition();
                    return;
                }

                _activeToast = new UpdateToastNotificationWindow
                {
                    DataContext = viewModel
                };
                _activeToast.Show();
            }
        });
    }

    public void Reposition()
    {
        try
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen == null) return;

            var wa = screen.WorkingArea;
            double scale = screen.Scaling;
            int margin = (int)(20 * scale);

            UpdateLayout();
            double actualWidth = Bounds.Width > 0 ? Bounds.Width : Width;
            double actualHeight = Bounds.Height > 0 ? Bounds.Height : 340;
            int winWidth = (int)(actualWidth * scale);
            int winHeight = (int)(actualHeight * scale);

            int x = wa.Right - winWidth - margin;
            int y = wa.Bottom - winHeight - margin;

            Position = new PixelPoint(x, y);
        }
        catch { }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
