using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FDownloader.App.ViewModels;

using Avalonia.Platform.Storage;

namespace FDownloader.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.RequestBrowseTorrentFile = BrowseTorrentFileAsync;
            }
        };
    }

    private async Task<string?> BrowseTorrentFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "BitTorrent Dosyası Seç (.torrent)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("BitTorrent Dosyaları (*.torrent)")
                {
                    Patterns = new[] { "*.torrent" }
                },
                new FilePickerFileType("Tüm Dosyalar (*.*)")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (files != null && files.Count > 0)
        {
            return files[0].Path.LocalPath;
        }

        return null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        try
        {
            if (e.DataTransfer == null) return;

            // 1. Sürüklenen dosyalar (.torrent veya diğer)
            var files = e.DataTransfer.TryGetFiles();
            if (files != null)
            {
                foreach (var file in files)
                {
                    var localPath = file.TryGetLocalPath() ?? file.Path.LocalPath;
                    if (!string.IsNullOrWhiteSpace(localPath))
                    {
                        await vm.HandleDroppedFileOrUrlAsync(localPath);
                    }
                }
                return;
            }

            // 2. Sürüklenen metin (magnet: veya http: bağlantıları)
            var text = e.DataTransfer.TryGetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                await vm.HandleDroppedFileOrUrlAsync(text.Trim());
            }
        }
        catch { }
    }

    public void BringToFront()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Show();
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var handle = TryGetPlatformHandle()?.Handle;
                    if (handle.HasValue && handle.Value != IntPtr.Zero)
                    {
                        ShowWindow(handle.Value, 9); // SW_RESTORE
                        SetForegroundWindow(handle.Value);
                    }
                }
                catch { }
            }
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not MainViewModel vm) return;

        // Diyalog açıksa Escape ile kapatılabilsin
        if (vm.IsDeleteConfirmDialogOpen)
        {
            if (e.Key == Key.Escape)
            {
                vm.CancelDeleteDialog();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Enter)
            {
                if (vm.DeleteMode == DeleteConfirmMode.ListOnlyPrompt)
                {
                    _ = vm.ConfirmDeleteListOnlyAsync();
                    e.Handled = true;
                    return;
                }
                else if (vm.DeleteMode == DeleteConfirmMode.DiskAndListPrompt)
                {
                    _ = vm.ConfirmDeleteWithFileAsync();
                    e.Handled = true;
                    return;
                }
                else if (vm.DeleteMode == DeleteConfirmMode.GeneralChoice)
                {
                    // Güvenli varsayılan: Yalnızca listeden kaldır, diskteki dosyayı asla silme!
                    _ = vm.ConfirmDeleteListOnlyAsync();
                    e.Handled = true;
                    return;
                }
            }
        }

        // Eğer kullanıcı bir metin kutusuna yazı yazıyorsa kısayolu tüketme
        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        // ✔️ Ctrl+A: Tümünü Seç
        if (e.Key == Key.A && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            vm.SelectAll();
            e.Handled = true;
            return;
        }

        // 🗑️ Delete ve Shift+Delete Kısayolları
        if (e.Key == Key.Delete)
        {
            bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (isShift)
            {
                // Shift + Delete: Diskten ve Listeden Kalıcı Olarak Silme Önerisi
                vm.RequestPermanentDelete();
                e.Handled = true;
            }
            else
            {
                // Delete: Yalnızca Listeden Kaldırma (Diskten Silmeden) Önerisi
                vm.RequestListOnlyDelete();
                e.Handled = true;
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm)
        {
            vm.RequestSelectItem += item =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (DownloadsListBox != null)
                    {
                        DownloadsListBox.Selection.Clear();
                        DownloadsListBox.SelectedItems?.Add(item);
                        DownloadsListBox.ScrollIntoView(item);
                        DownloadsListBox.Focus();
                    }
                });
            };

            vm.RequestSelectAll += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (DownloadsListBox != null)
                    {
                        DownloadsListBox.Selection.Clear();
                        DownloadsListBox.Selection.SelectAll();
                        DownloadsListBox.Focus();
                    }
                });
            };
        }
    }

    private void OnDownloadsListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectAll();
                e.Handled = true;
            }
        }
    }

    private void OnDownloadsListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            var source = e.Source as Visual;
            var listBoxItem = source?.FindAncestorOfType<ListBoxItem>();
            if (listBoxItem?.DataContext is DownloadItemViewModel clickedItem && DownloadsListBox != null)
            {
                if (DownloadsListBox.SelectedItems != null && !DownloadsListBox.SelectedItems.Contains(clickedItem))
                {
                    DownloadsListBox.Selection.Clear();
                    DownloadsListBox.SelectedItems.Add(clickedItem);
                }
            }
        }
    }

    private void OnDownloadsListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && DownloadsListBox?.SelectedItems != null)
        {
            vm.SyncSelectedItems(DownloadsListBox.SelectedItems);
        }
    }

    private void OnSpeedLimitButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OpenSpeedLimitSettings();
                e.Handled = true;
            }
        }
    }

    private void OnColumnThumbDragDelta(object? sender, VectorEventArgs e)
    {
        if (sender is Control thumb && thumb.Tag is string colName && DataContext is MainViewModel vm)
        {
            vm.AdjustColumnWidth(colName, e.Vector.X);
        }
    }

    private void OnColumnThumbDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control thumb && thumb.Tag is string colName && DataContext is MainViewModel vm)
        {
            vm.ResetSingleColumnWidth(colName);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}