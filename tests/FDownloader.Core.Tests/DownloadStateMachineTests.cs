using FDownloader.Core.Domain;
using Xunit;

namespace FDownloader.Core.Tests;

public class DownloadStateMachineTests
{
    [Theory]
    [InlineData(DownloadStatus.Queued, DownloadStatus.Connecting, true)]
    [InlineData(DownloadStatus.Queued, DownloadStatus.Downloading, true)]
    [InlineData(DownloadStatus.Queued, DownloadStatus.Paused, true)]
    [InlineData(DownloadStatus.Queued, DownloadStatus.Cancelled, true)]
    [InlineData(DownloadStatus.Queued, DownloadStatus.Completed, false)]
    [InlineData(DownloadStatus.Queued, DownloadStatus.Merging, false)]
    [InlineData(DownloadStatus.Connecting, DownloadStatus.Downloading, true)]
    [InlineData(DownloadStatus.Connecting, DownloadStatus.Paused, true)]
    [InlineData(DownloadStatus.Connecting, DownloadStatus.Failed, true)]
    [InlineData(DownloadStatus.Connecting, DownloadStatus.Cancelled, true)]
    [InlineData(DownloadStatus.Downloading, DownloadStatus.Paused, true)]
    [InlineData(DownloadStatus.Downloading, DownloadStatus.Merging, true)]
    [InlineData(DownloadStatus.Downloading, DownloadStatus.Completed, true)]
    [InlineData(DownloadStatus.Downloading, DownloadStatus.Failed, true)]
    [InlineData(DownloadStatus.Downloading, DownloadStatus.Cancelled, true)]
    [InlineData(DownloadStatus.Downloading, DownloadStatus.Queued, false)]
    [InlineData(DownloadStatus.Paused, DownloadStatus.Downloading, true)]
    [InlineData(DownloadStatus.Paused, DownloadStatus.Connecting, true)]
    [InlineData(DownloadStatus.Paused, DownloadStatus.Queued, true)]
    [InlineData(DownloadStatus.Paused, DownloadStatus.Cancelled, true)]
    [InlineData(DownloadStatus.Paused, DownloadStatus.Completed, false)]
    [InlineData(DownloadStatus.Failed, DownloadStatus.Queued, true)]
    [InlineData(DownloadStatus.Failed, DownloadStatus.Connecting, true)]
    [InlineData(DownloadStatus.Failed, DownloadStatus.Downloading, true)]
    [InlineData(DownloadStatus.Completed, DownloadStatus.Downloading, false)]
    [InlineData(DownloadStatus.Completed, DownloadStatus.Paused, false)]
    [InlineData(DownloadStatus.Completed, DownloadStatus.Queued, true)]
    public void CanTransition_ValidatesAllowedAndDisallowedTransitions(DownloadStatus current, DownloadStatus target, bool expected)
    {
        bool actual = DownloadStateMachine.CanTransition(current, target);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DownloadTask_StatusSetter_ThrowsOnInvalidTransition()
    {
        var task = new DownloadTask();
        Assert.Equal(DownloadStatus.Queued, task.Status);

        // Queued -> Completed doğrudan geçişi geçersizdir
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            task.Status = DownloadStatus.Completed;
        });

        Assert.Contains("Geçersiz durum geçişi", ex.Message);
        Assert.Equal(DownloadStatus.Queued, task.Status);
    }

    [Fact]
    public void DownloadTask_StatusSetter_SucceedsOnValidLifecycle()
    {
        var task = new DownloadTask();
        Assert.Equal(DownloadStatus.Queued, task.Status);

        task.Status = DownloadStatus.Connecting;
        Assert.Equal(DownloadStatus.Connecting, task.Status);

        task.Status = DownloadStatus.Downloading;
        Assert.Equal(DownloadStatus.Downloading, task.Status);

        task.Status = DownloadStatus.Paused;
        Assert.Equal(DownloadStatus.Paused, task.Status);

        task.Status = DownloadStatus.Downloading;
        Assert.Equal(DownloadStatus.Downloading, task.Status);

        task.Status = DownloadStatus.Merging;
        Assert.Equal(DownloadStatus.Merging, task.Status);

        task.Status = DownloadStatus.Completed;
        Assert.Equal(DownloadStatus.Completed, task.Status);
    }

    [Fact]
    public void DownloadTask_SetStatusDirect_BypassesStateMachineValidation()
    {
        var task = new DownloadTask();
        Assert.Equal(DownloadStatus.Queued, task.Status);

        // Veritabanından yükleme gibi durumlar için doğrudan durum atama
        task.SetStatusDirect(DownloadStatus.Completed);
        Assert.Equal(DownloadStatus.Completed, task.Status);
    }
}
